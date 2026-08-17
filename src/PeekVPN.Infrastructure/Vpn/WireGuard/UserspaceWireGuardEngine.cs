using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace PeekVPN.Infrastructure.Vpn.WireGuard;

/// <summary>
/// Initiator-only userspace WireGuard (Noise_IKpsk2) that pumps packets between a TUN and a UDP socket.
/// </summary>
internal sealed class UserspaceWireGuardEngine : IAsyncDisposable
{
    private const int InitiationSize = 148;
    private const int ResponseSize = 92;
    private const int TransportHeaderSize = 16;
    private const int Mac1OffsetInitiation = 116;
    private const int Mac1OffsetResponse = 60;
    private const byte MessageInitiation = 1;
    private const byte MessageResponse = 2;
    private const byte MessageCookie = 3;
    private const byte MessageTransport = 4;

    private readonly byte[] _staticPrivate;
    private readonly byte[] _staticPublic;
    private readonly byte[] _peerPublic;
    private readonly byte[] _presharedKey;
    private readonly IPEndPoint _endpoint;
    private readonly int _keepaliveSeconds;
    private readonly ITunPacketIO _tun;
    private readonly ILogger _logger;

    private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    private readonly object _sessionLock = new();
    private readonly CancellationTokenSource _lifetime = new();

    private HandshakeState? _handshake;
    private SessionKeys? _session;
    private Task? _udpLoop;
    private Task? _tunLoop;
    private Task? _timerLoop;
    private bool _disposed;

    public UserspaceWireGuardEngine(
        byte[] staticPrivate,
        byte[] peerPublic,
        byte[]? presharedKey,
        IPEndPoint endpoint,
        int? keepaliveSeconds,
        ITunPacketIO tun,
        ILogger logger)
    {
        _staticPrivate = staticPrivate;
        _staticPublic = WireGuardCrypto.PublicFromPrivate(staticPrivate);
        _peerPublic = peerPublic;
        _presharedKey = presharedKey ?? new byte[32];
        _endpoint = endpoint;
        _keepaliveSeconds = keepaliveSeconds is > 0 ? keepaliveSeconds.Value : 0;
        _tun = tun;
        _logger = logger;
        _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        await HandshakeAsync(linked.Token).ConfigureAwait(false);

        _udpLoop = Task.Run(() => ReceiveUdpLoopAsync(_lifetime.Token), CancellationToken.None);
        _tunLoop = Task.Run(() => TunLoopAsync(_lifetime.Token), CancellationToken.None);
        _timerLoop = Task.Run(() => TimerLoopAsync(_lifetime.Token), CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();
        _socket.Dispose();

        try
        {
            await Task.WhenAll(
                _udpLoop ?? Task.CompletedTask,
                _tunLoop ?? Task.CompletedTask,
                _timerLoop ?? Task.CompletedTask).ConfigureAwait(false);
        }
        catch
        {
            // Loops observe cancellation.
        }

        _lifetime.Dispose();
    }

    private async Task HandshakeAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (!cancellationToken.IsCancellationRequested)
        {
            var remaining = deadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                throw new TimeoutException("WireGuard handshake timed out.");
            }

            var initiation = CreateInitiation();
            await _socket.SendToAsync(initiation, SocketFlags.None, _endpoint, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Sent WireGuard handshake initiation to {Endpoint}.", _endpoint);

            var buffer = new byte[2048];
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5) + TimeSpan.FromMilliseconds(Random.Shared.Next(0, 333)));

            try
            {
                var result = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), timeoutCts.Token)
                    .ConfigureAwait(false);

                if (TryConsumeHandshakeResponse(buffer.AsSpan(0, result.ReceivedBytes)))
                {
                    SendKeepalive();
                    _logger.LogInformation("WireGuard handshake completed with {Endpoint}.", _endpoint);
                    return;
                }

                _logger.LogDebug("Ignored {ByteCount}-byte UDP packet during handshake.", result.ReceivedBytes);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogDebug("Handshake response wait timed out; retrying.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private byte[] CreateInitiation()
    {
        var chainingKey = WireGuardCrypto.Hash(WireGuardCrypto.Construction);
        var hash = WireGuardCrypto.ConcatHash(WireGuardCrypto.ConcatHash(chainingKey, WireGuardCrypto.Identifier), _peerPublic);

        var ephemeralPrivate = WireGuardCrypto.GeneratePrivateKey();
        var ephemeralPublic = WireGuardCrypto.PublicFromPrivate(ephemeralPrivate);
        uint senderIndex;
        lock (_sessionLock)
        {
            senderIndex = (uint)Random.Shared.Next(1, int.MaxValue);
            _handshake = new HandshakeState(chainingKey, hash, ephemeralPrivate, senderIndex);
        }

        hash = WireGuardCrypto.ConcatHash(_handshake.Hash, ephemeralPublic);
        var temp = WireGuardCrypto.Hmac(_handshake.ChainingKey, ephemeralPublic);
        chainingKey = WireGuardCrypto.Hmac(temp, [0x01]);

        temp = WireGuardCrypto.Hmac(chainingKey, WireGuardCrypto.Dh(ephemeralPrivate, _peerPublic));
        chainingKey = WireGuardCrypto.Hmac(temp, [0x01]);
        var key = WireGuardCrypto.Hmac(temp, Concat(chainingKey, [0x02]));

        var encryptedStatic = WireGuardCrypto.AeadEncrypt(key, 0, _staticPublic, hash);
        hash = WireGuardCrypto.ConcatHash(hash, encryptedStatic);

        temp = WireGuardCrypto.Hmac(chainingKey, WireGuardCrypto.Dh(_staticPrivate, _peerPublic));
        chainingKey = WireGuardCrypto.Hmac(temp, [0x01]);
        key = WireGuardCrypto.Hmac(temp, Concat(chainingKey, [0x02]));

        var encryptedTimestamp = WireGuardCrypto.AeadEncrypt(key, 0, WireGuardCrypto.Tai64nNow(), hash);
        hash = WireGuardCrypto.ConcatHash(hash, encryptedTimestamp);

        var msg = new byte[InitiationSize];
        msg[0] = MessageInitiation;
        BinaryPrimitives.WriteUInt32LittleEndian(msg.AsSpan(4), senderIndex);
        ephemeralPublic.CopyTo(msg.AsSpan(8));
        encryptedStatic.CopyTo(msg.AsSpan(40));
        encryptedTimestamp.CopyTo(msg.AsSpan(88));

        var mac1Key = WireGuardCrypto.Hash(Concat(WireGuardCrypto.LabelMac1, _peerPublic));
        WireGuardCrypto.Mac16(mac1Key, msg.AsSpan(0, Mac1OffsetInitiation)).CopyTo(msg.AsSpan(Mac1OffsetInitiation));

        lock (_sessionLock)
        {
            _handshake = new HandshakeState(chainingKey, hash, ephemeralPrivate, senderIndex);
        }

        return msg;
    }

    private bool TryConsumeHandshakeResponse(ReadOnlySpan<byte> packet)
    {
        if (packet.Length == 0)
        {
            return false;
        }

        if (packet[0] == MessageCookie)
        {
            _logger.LogWarning("Received a WireGuard cookie reply; the peer is under load. Retrying without cookies.");
            return false;
        }

        if (packet.Length != ResponseSize || packet[0] != MessageResponse)
        {
            return false;
        }

        HandshakeState handshake;
        lock (_sessionLock)
        {
            if (_handshake is null)
            {
                return false;
            }

            handshake = _handshake;
        }

        var receiverIndex = BinaryPrimitives.ReadUInt32LittleEndian(packet[8..12]);
        if (receiverIndex != handshake.SenderIndex)
        {
            return false;
        }

        var mac1Key = WireGuardCrypto.Hash(Concat(WireGuardCrypto.LabelMac1, _staticPublic));
        var expectedMac1 = WireGuardCrypto.Mac16(mac1Key, packet[..Mac1OffsetResponse]);
        if (!CryptographicEquals(expectedMac1, packet.Slice(Mac1OffsetResponse, 16)))
        {
            return false;
        }

        var peerEphemeral = packet.Slice(12, 32).ToArray();
        var hash = WireGuardCrypto.ConcatHash(handshake.Hash, peerEphemeral);

        var temp = WireGuardCrypto.Hmac(handshake.ChainingKey, peerEphemeral);
        var chainingKey = WireGuardCrypto.Hmac(temp, [0x01]);

        temp = WireGuardCrypto.Hmac(chainingKey, WireGuardCrypto.Dh(handshake.EphemeralPrivate, peerEphemeral));
        chainingKey = WireGuardCrypto.Hmac(temp, [0x01]);

        temp = WireGuardCrypto.Hmac(chainingKey, WireGuardCrypto.Dh(handshake.EphemeralPrivate, _peerPublic));
        chainingKey = WireGuardCrypto.Hmac(temp, [0x01]);

        temp = WireGuardCrypto.Hmac(chainingKey, _presharedKey);
        chainingKey = WireGuardCrypto.Hmac(temp, [0x01]);
        var temp2 = WireGuardCrypto.Hmac(temp, Concat(chainingKey, [0x02]));
        var key = WireGuardCrypto.Hmac(temp, Concat(temp2, [0x03]));
        hash = WireGuardCrypto.ConcatHash(hash, temp2);

        var encryptedNothing = packet.Slice(44, 16);
        if (!WireGuardCrypto.TryAeadDecrypt(key, 0, encryptedNothing, hash, out var nothing) || nothing.Length != 0)
        {
            return false;
        }

        hash = WireGuardCrypto.ConcatHash(hash, encryptedNothing);

        temp = WireGuardCrypto.Hmac(chainingKey, []);
        var sendingKey = WireGuardCrypto.Hmac(temp, [0x01]);
        var receivingKey = WireGuardCrypto.Hmac(temp, Concat(sendingKey, [0x02]));
        var peerSenderIndex = BinaryPrimitives.ReadUInt32LittleEndian(packet[4..8]);

        lock (_sessionLock)
        {
            _session = new SessionKeys(sendingKey, receivingKey, peerSenderIndex, handshake.SenderIndex);
            _handshake = null;
        }

        return true;
    }

    private async Task ReceiveUdpLoopAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[65535];
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await _socket.ReceiveFromAsync(buffer, SocketFlags.None, new IPEndPoint(IPAddress.Any, 0), cancellationToken)
                    .ConfigureAwait(false);
                HandleIncoming(buffer.AsSpan(0, result.ReceivedBytes));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "WireGuard UDP receive failed.");
            }
        }
    }

    private async Task TunLoopAsync(CancellationToken cancellationToken)
    {
        var packet = new byte[65535];
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!_tun.TryRead(packet, out var length))
                {
                    await _tun.WaitForReadableAsync(cancellationToken).ConfigureAwait(false);
                    continue;
                }

                SendTransport(packet.AsSpan(0, length));
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "WireGuard TUN read failed.");
            }
        }
    }

    private async Task TimerLoopAsync(CancellationToken cancellationToken)
    {
        if (_keepaliveSeconds <= 0)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_keepaliveSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                SendKeepalive();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void HandleIncoming(ReadOnlySpan<byte> packet)
    {
        if (packet.Length < 1)
        {
            return;
        }

        if (packet[0] == MessageResponse)
        {
            TryConsumeHandshakeResponse(packet);
            return;
        }

        if (packet[0] != MessageTransport || packet.Length < TransportHeaderSize + 16)
        {
            return;
        }

        SessionKeys session;
        lock (_sessionLock)
        {
            if (_session is null)
            {
                return;
            }

            session = _session;
        }

        var receiver = BinaryPrimitives.ReadUInt32LittleEndian(packet[4..8]);
        if (receiver != session.LocalIndex)
        {
            return;
        }

        var counter = BinaryPrimitives.ReadUInt64LittleEndian(packet[8..16]);
        if (!session.Replay.TryAccept(counter))
        {
            return;
        }

        if (!WireGuardCrypto.TryAeadDecrypt(session.ReceivingKey, counter, packet[TransportHeaderSize..], [], out var inner))
        {
            return;
        }

        if (inner.Length > 0)
        {
            _tun.TryWrite(inner);
        }

        session.LastReceivedUtc = DateTime.UtcNow;
    }

    private void SendKeepalive() => SendTransport([]);

    private void SendTransport(ReadOnlySpan<byte> inner)
    {
        SessionKeys session;
        ulong counter;
        lock (_sessionLock)
        {
            if (_session is null)
            {
                return;
            }

            session = _session;
            counter = session.SendingCounter++;
        }

        var padded = Pad16(inner);
        var encrypted = WireGuardCrypto.AeadEncrypt(session.SendingKey, counter, padded, []);
        var msg = new byte[TransportHeaderSize + encrypted.Length];
        msg[0] = MessageTransport;
        BinaryPrimitives.WriteUInt32LittleEndian(msg.AsSpan(4), session.RemoteIndex);
        BinaryPrimitives.WriteUInt64LittleEndian(msg.AsSpan(8), counter);
        encrypted.CopyTo(msg.AsSpan(TransportHeaderSize));

        try
        {
            _socket.SendTo(msg, SocketFlags.None, _endpoint);
            session.LastSentUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WireGuard UDP send failed.");
        }
    }

    private static byte[] Pad16(ReadOnlySpan<byte> inner)
    {
        if (inner.Length == 0)
        {
            return [];
        }

        var paddedLength = (inner.Length + 15) & ~15;
        var padded = new byte[paddedLength];
        inner.CopyTo(padded);
        return padded;
    }

    private static byte[] Concat(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var result = new byte[a.Length + b.Length];
        a.CopyTo(result);
        b.CopyTo(result.AsSpan(a.Length));
        return result;
    }

    private static bool CryptographicEquals(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        => a.Length == b.Length && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);

    private sealed record HandshakeState(byte[] ChainingKey, byte[] Hash, byte[] EphemeralPrivate, uint SenderIndex);

    private sealed class SessionKeys(byte[] sendingKey, byte[] receivingKey, uint remoteIndex, uint localIndex)
    {
        public byte[] SendingKey { get; } = sendingKey;
        public byte[] ReceivingKey { get; } = receivingKey;
        public uint RemoteIndex { get; } = remoteIndex;
        public uint LocalIndex { get; } = localIndex;
        public ulong SendingCounter;
        public ReplayWindow Replay { get; } = new();
        public DateTime LastSentUtc { get; set; } = DateTime.UtcNow;
        public DateTime LastReceivedUtc { get; set; } = DateTime.UtcNow;
    }
}

internal sealed class ReplayWindow
{
    private const int Bits = 1024;
    private ulong _highest;
    private readonly ulong[] _bitmap = new ulong[Bits / 64];

    public bool TryAccept(ulong counter)
    {
        if (counter > _highest)
        {
            var shift = counter - _highest;
            if (shift >= Bits)
            {
                Array.Clear(_bitmap);
            }
            else
            {
                Shift((int)shift);
            }

            _highest = counter;
            Set(counter);
            return true;
        }

        var age = _highest - counter;
        if (age >= Bits)
        {
            return false;
        }

        if (IsSet(counter))
        {
            return false;
        }

        Set(counter);
        return true;
    }

    private void Shift(int bits)
    {
        var wordShift = bits / 64;
        var bitShift = bits % 64;
        if (wordShift > 0)
        {
            Array.Copy(_bitmap, wordShift, _bitmap, 0, _bitmap.Length - wordShift);
            Array.Clear(_bitmap, _bitmap.Length - wordShift, wordShift);
        }

        if (bitShift == 0)
        {
            return;
        }

        ulong carry = 0;
        for (var i = _bitmap.Length - 1; i >= 0; i--)
        {
            var nextCarry = _bitmap[i] << (64 - bitShift);
            _bitmap[i] = (_bitmap[i] >> bitShift) | carry;
            carry = nextCarry;
        }
    }

    private bool IsSet(ulong counter)
    {
        var offset = _highest - counter;
        var word = offset / 64;
        var bit = offset % 64;
        return (_bitmap[word] & (1UL << (int)bit)) != 0;
    }

    private void Set(ulong counter)
    {
        var offset = _highest - counter;
        var word = offset / 64;
        var bit = offset % 64;
        _bitmap[word] |= 1UL << (int)bit;
    }
}

internal interface ITunPacketIO
{
    bool TryRead(Span<byte> buffer, out int length);

    bool TryWrite(ReadOnlySpan<byte> packet);

    Task WaitForReadableAsync(CancellationToken cancellationToken);
}
