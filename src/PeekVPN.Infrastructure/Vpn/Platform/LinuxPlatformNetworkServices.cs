using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

/// <summary>
/// Linux-specific implementations of the VPN platform building blocks.
/// </summary>
public sealed class LinuxPlatformNetworkServices : IPlatformNetworkServices
{
    private const string InterfaceName = "peekvpn0";

    public LinuxPlatformNetworkServices(ILoggerFactory loggerFactory)
    {
        RoutingManager = new LinuxIpRoutingManager(loggerFactory.CreateLogger<LinuxIpRoutingManager>());
        FirewallManager = new LinuxFirewallManager(loggerFactory.CreateLogger<LinuxFirewallManager>());
        DnsManager = new LinuxDnsManager(InterfaceName, loggerFactory.CreateLogger<LinuxDnsManager>());
    }

    public IRoutingManager RoutingManager { get; }

    public IFirewallManager FirewallManager { get; }

    public IDnsManager DnsManager { get; }

    public Task<ITunnelAdapter> CreateTunAdapterAsync(string name, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<ITunnelAdapter>(new LinuxTunAdapter(name));
    }
}
