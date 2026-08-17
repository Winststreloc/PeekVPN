using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

[SupportedOSPlatform("windows")]
public sealed class WindowsPlatformNetworkServices : IPlatformNetworkServices
{
    private readonly ILoggerFactory _loggerFactory;

    public WindowsPlatformNetworkServices(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        RoutingManager = new WindowsIpRoutingManager(loggerFactory.CreateLogger<WindowsIpRoutingManager>());
        FirewallManager = new WindowsFirewallManager(loggerFactory.CreateLogger<WindowsFirewallManager>());
        DnsManager = new WindowsDnsManager(
            WindowsWireGuardTunnel.DefaultInterfaceName,
            loggerFactory.CreateLogger<WindowsDnsManager>());
    }

    public IRoutingManager RoutingManager { get; }

    public IFirewallManager FirewallManager { get; }

    public IDnsManager DnsManager { get; }

    public IWireGuardTunnel CreateWireGuardTunnel()
        => new WindowsWireGuardTunnel(_loggerFactory);

    public Task<ITunnelAdapter> CreateTunAdapterAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ITunnelAdapter>(new WindowsTunAdapter(name));
    }
}
