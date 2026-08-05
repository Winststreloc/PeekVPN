namespace PeekVPN.Core.Vpn;

/// <summary>
/// Result of an attempt to establish or tear down a VPN connection.
/// </summary>
public sealed record ConnectionResult(bool IsSuccessful, string? ErrorMessage)
{
    public static ConnectionResult Ok() => new(true, null);

    public static ConnectionResult Failed(string errorMessage) => new(false, errorMessage);
}
