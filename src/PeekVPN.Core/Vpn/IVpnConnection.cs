namespace PeekVPN.Core.Vpn;

/// <summary>
/// A concrete VPN protocol implementation (WireGuard, K2, UDP, etc.).
/// </summary>
public interface IVpnConnection : IAsyncDisposable
{
    string Protocol { get; }

    Task EstablishAsync(VpnConnectionRequest request, CancellationToken cancellationToken = default);

    Task TeardownAsync(CancellationToken cancellationToken = default);
}
