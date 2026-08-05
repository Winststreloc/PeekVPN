namespace PeekVPN.Contracts;

public sealed record ConnectResponse(
    bool Success,
    string? ServerId,
    string? ErrorMessage,
    WireGuardConfig? Config = null);
