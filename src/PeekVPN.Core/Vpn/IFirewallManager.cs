namespace PeekVPN.Core.Vpn;

/// <summary>
/// Manages OS firewall rules, primarily for the VPN kill switch.
/// </summary>
public interface IFirewallManager
{
    Task EnableKillSwitchAsync(KillSwitchRules rules, CancellationToken cancellationToken = default);

    Task DisableKillSwitchAsync(CancellationToken cancellationToken = default);
}
