namespace PeekVPN.Contracts;

/// <summary>
/// WireGuard configuration returned by the upstream VPN API when a connection is established.
/// The raw string can be written directly to a <c>wg-quick</c>-compatible file.
/// </summary>
public sealed record WireGuardConfig(string RawConfig);
