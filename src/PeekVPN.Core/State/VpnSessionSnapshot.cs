namespace PeekVPN.Core.State;

public sealed record VpnSessionSnapshot(
    VpnConnectionState State,
    string? ActiveServerId,
    string? LastError,
    PeekVPN.Contracts.WireGuardConfig? Config = null);
