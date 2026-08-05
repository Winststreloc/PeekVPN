namespace PeekVPN.Core.Vpn;

/// <summary>
/// High-level orchestrator that establishes and tears down VPN connections.
/// It selects the protocol factory and delegates platform-specific work to the platform services.
/// </summary>
public interface IVpnConnectionOrchestrator
{
    Task<ConnectionResult> ConnectAsync(VpnConnectionRequest request, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
