namespace PeekVPN.Core.Vpn;

/// <summary>
/// Configuration applied to a virtual network adapter.
/// </summary>
public sealed record AdapterConfiguration(
    IReadOnlyList<string> Addresses,
    IReadOnlyList<string>? DnsServers = null,
    int? Mtu = null);
