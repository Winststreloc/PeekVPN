namespace PeekVPN.Core.Vpn;

/// <summary>
/// Manages OS routing table entries for the VPN connection.
/// </summary>
public interface IRoutingManager
{
    Task AddRouteAsync(Route route, CancellationToken cancellationToken = default);

    Task RemoveRouteAsync(Route route, CancellationToken cancellationToken = default);

    Task FlushInterfaceRoutesAsync(string interfaceName, CancellationToken cancellationToken = default);
}
