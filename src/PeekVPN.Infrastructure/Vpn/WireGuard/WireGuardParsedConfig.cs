namespace PeekVPN.Infrastructure.Vpn.WireGuard;

internal sealed record WireGuardParsedConfig(
    IReadOnlyList<string> Addresses,
    IReadOnlyList<string> DnsServers,
    IReadOnlyList<string> AllowedIps,
    string? Endpoint,
    int? PersistentKeepalive,
    byte[]? PrivateKey = null,
    byte[]? PeerPublicKey = null,
    byte[]? PresharedKey = null);
