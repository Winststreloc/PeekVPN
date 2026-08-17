using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using PeekVPN.Infrastructure.Vpn.WireGuard;

namespace PeekVPN.Infrastructure.Vpn.Platform;

[SupportedOSPlatform("windows")]
internal sealed unsafe class WintunSession : ITunPacketIO, IDisposable
{
    private readonly IntPtr _session;
    private readonly NativeWaitHandle _readEvent;
    private bool _disposed;

    public WintunSession(IntPtr session)
    {
        if (session == IntPtr.Zero)
        {
            throw new InvalidOperationException($"WintunStartSession failed: {Marshal.GetLastPInvokeErrorMessage()}");
        }

        _session = session;
        _readEvent = new NativeWaitHandle(WintunNative.GetReadWaitEvent(session));
    }

    public bool TryRead(Span<byte> buffer, out int length)
    {
        length = 0;
        uint size;
        var packet = WintunNative.ReceivePacket(_session, out size);
        if (packet is null)
        {
            return false;
        }

        try
        {
            length = (int)size;
            if (length > buffer.Length)
            {
                return false;
            }

            new ReadOnlySpan<byte>(packet, length).CopyTo(buffer);
            return true;
        }
        finally
        {
            WintunNative.ReleaseReceivePacket(_session, packet);
        }
    }

    public bool TryWrite(ReadOnlySpan<byte> packet)
    {
        var destination = WintunNative.AllocateSendPacket(_session, (uint)packet.Length);
        if (destination is null)
        {
            return false;
        }

        packet.CopyTo(new Span<byte>(destination, packet.Length));
        WintunNative.SendPacket(_session, destination);
        return true;
    }

    public Task WaitForReadableAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled(cancellationToken);
        }

        return Task.Run(() =>
        {
            WaitHandle.WaitAny([_readEvent, cancellationToken.WaitHandle]);
            cancellationToken.ThrowIfCancellationRequested();
        }, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _readEvent.Dispose();
        WintunNative.EndSession(_session);
    }

    private sealed class NativeWaitHandle : WaitHandle
    {
        public NativeWaitHandle(IntPtr nativeEvent)
        {
            SafeWaitHandle = new SafeWaitHandle(nativeEvent, ownsHandle: false);
        }
    }
}
