using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

/// <summary>
/// Linux-specific implementations of the VPN platform building blocks.
/// </summary>
public sealed class LinuxPlatformNetworkServices : IPlatformNetworkServices
{
    private readonly ILoggerFactory _loggerFactory;

    public LinuxPlatformNetworkServices(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        RoutingManager = new LinuxIpRoutingManager(loggerFactory.CreateLogger<LinuxIpRoutingManager>());
        FirewallManager = new LinuxFirewallManager(loggerFactory.CreateLogger<LinuxFirewallManager>());
        DnsManager = new LinuxDnsManager(
            LinuxWireGuardTunnel.DefaultInterfaceName,
            loggerFactory.CreateLogger<LinuxDnsManager>());
    }

    public IRoutingManager RoutingManager { get; }

    public IFirewallManager FirewallManager { get; }

    public IDnsManager DnsManager { get; }

    public IWireGuardTunnel CreateWireGuardTunnel()
        => new LinuxWireGuardTunnel(_loggerFactory.CreateLogger<LinuxWireGuardTunnel>());

    public Task<ITunnelAdapter> CreateTunAdapterAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ITunnelAdapter>(new LinuxTunAdapter(name));
    }
}
