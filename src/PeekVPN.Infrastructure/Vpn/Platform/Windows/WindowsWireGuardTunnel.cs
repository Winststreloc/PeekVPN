using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;
using PeekVPN.Infrastructure.Vpn.WireGuard;

namespace PeekVPN.Infrastructure.Vpn.Platform;

[SupportedOSPlatform("windows")]
internal sealed class WindowsWireGuardTunnel : IWireGuardTunnel
{
    public const string DefaultInterfaceName = "PeekVPN";

    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<WindowsWireGuardTunnel> _logger;

    private WindowsTunAdapter? _adapter;
    private WintunSession? _session;
    private UserspaceWireGuardEngine? _engine;
    private WireGuardParsedConfig? _parsed;
    private bool _disposed;

    public WindowsWireGuardTunnel(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<WindowsWireGuardTunnel>();
    }

    public string InterfaceName => DefaultInterfaceName;

    public Task CreateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        WintunNative.EnsureLoaded();
        _adapter = new WindowsTunAdapter(InterfaceName);
        var version = WintunNative.GetRunningDriverVersion();
        _logger.LogInformation(
            "Created Wintun adapter {InterfaceName} (driver {Major}.{Minor}).",
            InterfaceName,
            (version >> 16) & 0xff,
            version & 0xff);
        return Task.CompletedTask;
    }

    public Task ApplyConfigurationAsync(string configText, CancellationToken cancellationToken = default)
    {
        var parsed = WireGuardConfigParser.Parse(configText);
        if (parsed.PrivateKey is null || parsed.PeerPublicKey is null)
        {
            throw new InvalidOperationException("Windows WireGuard requires Interface.PrivateKey and Peer.PublicKey.");
        }

        if (parsed.Endpoint is null || !WireGuardEndpoint.TryParse(parsed.Endpoint, out _, out _))
        {
            throw new InvalidOperationException("Windows WireGuard requires a valid Peer.Endpoint.");
        }

        _parsed = parsed;
        return Task.CompletedTask;
    }

    public async Task ConfigureAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
    {
        if (_adapter is null)
        {
            throw new InvalidOperationException("Wintun adapter has not been created.");
        }

        await _adapter.ConfigureAsync(new AdapterConfiguration(addresses, Mtu: 1420), cancellationToken)
            .ConfigureAwait(false);

        await ShellHelper.RunAsync(
            "netsh",
            $"interface ipv4 set interface \"{InterfaceName}\" metric=1",
            cancellationToken).ConfigureAwait(false);
    }

    public async Task BringUpAsync(CancellationToken cancellationToken = default)
    {
        if (_adapter is null || _parsed is null)
        {
            throw new InvalidOperationException("Wintun adapter is not configured.");
        }

        if (!WireGuardEndpoint.TryParse(_parsed.Endpoint!, out var host, out var port))
        {
            throw new InvalidOperationException($"Invalid WireGuard endpoint '{_parsed.Endpoint}'.");
        }

        var endpointAddress = IPAddress.TryParse(host, out var parsedIp)
            ? parsedIp
            : (await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false))
                .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork)
                ?? throw new InvalidOperationException($"Could not resolve WireGuard endpoint '{host}'.");

        var sessionHandle = WintunNative.StartSession(_adapter.AdapterHandle, WintunNative.DefaultRingCapacity);
        _session = new WintunSession(sessionHandle);

        _engine = new UserspaceWireGuardEngine(
            _parsed.PrivateKey!,
            _parsed.PeerPublicKey!,
            _parsed.PresharedKey,
            new IPEndPoint(endpointAddress, port),
            _parsed.PersistentKeepalive,
            _session,
            _loggerFactory.CreateLogger<UserspaceWireGuardEngine>());

        _logger.LogInformation("Starting userspace WireGuard over Wintun toward {Host}:{Port}.", host, port);
        await _engine.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (_engine is not null)
        {
            await _engine.DisposeAsync().ConfigureAwait(false);
            _engine = null;
        }

        _session?.Dispose();
        _session = null;
        _adapter?.DisposeAdapter();
        _adapter = null;
        _parsed = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DeleteAsync(CancellationToken.None).ConfigureAwait(false);
    }
}
