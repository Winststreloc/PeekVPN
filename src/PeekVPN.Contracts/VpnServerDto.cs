namespace PeekVPN.Contracts;

public sealed record VpnServerDto(
    string Id,
    string City,
    string Country,
    string CountryCode,
    int LatencyMs,
    string DisplayName,
    double Latitude,
    double Longitude);
