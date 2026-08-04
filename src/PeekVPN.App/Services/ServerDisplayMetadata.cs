namespace PeekVPN.App.Services;

/// <summary>
/// Presentation-ready metadata for a VPN server.
/// </summary>
public sealed record ServerDisplayMetadata(
    string Id,
    string City,
    string Country,
    string CountryCode,
    int LatencyMs,
    string DisplayName,
    Uri? FlagUri);
