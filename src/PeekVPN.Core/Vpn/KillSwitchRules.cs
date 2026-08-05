namespace PeekVPN.Core.Vpn;

/// <summary>
/// Rules used by the firewall manager to block traffic outside the VPN tunnel.
/// </summary>
public sealed record KillSwitchRules(
    IReadOnlyList<string> AllowedInterfaces,
    IReadOnlyList<string> AllowedEndpoints);
