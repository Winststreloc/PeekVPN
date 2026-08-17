namespace PeekVPN.Core.Vpn;

/// <summary>
/// Manages OS routing table entries for the VPN connection.
/// </summary>
public interface IRoutingManager
{
    Task AddRouteAsync(Route route, CancellationToken cancellationToken = default);

    Task RemoveRouteAsync(Route route, CancellationToken cancellationToken = default);

    Task FlushInterfaceRoutesAsync(string interfaceName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pins the current physical route to <paramref name="hostIp"/> so later default-route
    /// changes through the tunnel cannot create a routing loop to the VPN endpoint.
    /// </summary>
    Task PreserveHostRouteAsync(string hostIp, CancellationToken cancellationToken = default);
}
