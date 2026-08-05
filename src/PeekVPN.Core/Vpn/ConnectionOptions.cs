namespace PeekVPN.Core.Vpn;

/// <summary>
/// Options that control how the VPN connection is established.
/// </summary>
public sealed record ConnectionOptions(
    bool KillSwitch = false,
    bool SplitTunnel = false,
    IReadOnlyList<string>? AllowedIps = null)
{
    public IReadOnlyList<string> AllowedIps { get; init; } = AllowedIps ?? new[] { "0.0.0.0/0" };
}
