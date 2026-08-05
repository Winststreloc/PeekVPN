namespace PeekVPN.Core.Vpn;

/// <summary>
/// Factory for platform-specific VPN building blocks.
/// </summary>
public interface IPlatformNetworkServices
{
    /// <summary>
    /// Creates a generic TUN-style adapter. Used by userspace protocols.
    /// </summary>
    Task<ITunnelAdapter> CreateTunAdapterAsync(string name, CancellationToken cancellationToken = default);

    IRoutingManager RoutingManager { get; }

    IFirewallManager FirewallManager { get; }

    IDnsManager DnsManager { get; }
}
