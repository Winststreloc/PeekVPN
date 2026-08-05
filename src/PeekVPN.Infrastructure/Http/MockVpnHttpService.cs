using PeekVPN.Contracts;
using Microsoft.Extensions.Logging;

namespace PeekVPN.Infrastructure.Http;

public sealed class MockVpnHttpService(ILogger<MockVpnHttpService> logger) : IVpnApiClient
{
    private static readonly IReadOnlyList<VpnServerDto> Servers =
    [
        new("us-ny-42", "New York", "United States", "US", 24, "New York #42", 40.7128, -74.0060),
        new("uk-lon-12", "London", "United Kingdom", "GB", 86, "London #12", 51.5072, -0.1276),
        new("jp-tyo-05", "Tokyo", "Japan", "JP", 142, "Tokyo #05", 35.6762, 139.6503),
        new("de-fra-08", "Frankfurt", "Germany", "DE", 54, "Frankfurt #08", 50.1109, 8.6821),
        new("nl-ams-03", "Amsterdam", "Netherlands", "NL", 61, "Amsterdam #03", 52.3676, 4.9041),
        new("sg-sgp-11", "Singapore", "Singapore", "SG", 178, "Singapore #11", 1.3521, 103.8198),
        new("ca-tor-07", "Toronto", "Canada", "CA", 38, "Toronto #07", 43.6532, -79.3832),
        new("au-syd-02", "Sydney", "Australia", "AU", 210, "Sydney #02", -33.8688, 151.2093)
    ];

    private static readonly WireGuardConfig MockConfig = new("""
        [Interface]
        PrivateKey = sLd0GOQ6uDna0efwpQKEdP7Ljs2rxMjN0XtDWSbluk4=
        Address = 10.8.0.2/32
        DNS = 1.1.1.1, 1.0.0.1

        [Peer]
        PublicKey = Y5XJGHaOZeVbOcRWMe/A41DUrQ0pn1IwbMWJdik5rGY=
        Endpoint = 104.171.128.186:51820
        AllowedIPs = 0.0.0.0/0
        PersistentKeepalive = 25
        """);

    private readonly Random _random = new();

    public async Task<IReadOnlyList<VpnServerDto>> GetCitiesAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Mock VPN API is returning the server catalog.");
        await DelayAsync(350, 700, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Mock VPN API returned {ServerCount} servers.", Servers.Count);
        return Servers;
    }

    public async Task<WireGuardConfig?> GetCredentialsAsync(string serverId, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Mock VPN API is returning credentials for server {ServerId}.", serverId);
        await DelayAsync(100, 250, cancellationToken).ConfigureAwait(false);

        var server = Servers.FirstOrDefault(s => s.Id == serverId);
        return server is null ? null : MockConfig;
    }

    public async Task<ConnectResponse> ConnectAsync(
        ConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ServerId);

        logger.LogInformation("Mock VPN API received a connect request for server {ServerId}.", request.ServerId);
        await DelayAsync(900, 1500, cancellationToken).ConfigureAwait(false);

        var server = Servers.FirstOrDefault(s => s.Id == request.ServerId);
        if (server is null)
        {
            logger.LogWarning("Mock VPN API could not find requested server {ServerId}.", request.ServerId);
            return new ConnectResponse(false, null, "Server not found.");
        }

        // Occasional failure so the UI can exercise the error path.
        if (_random.NextDouble() < 0.05)
        {
            logger.LogWarning("Mock VPN API simulated a connection failure for server {ServerId}.", request.ServerId);
            return new ConnectResponse(false, null, "Unable to reach the selected server.");
        }

        logger.LogInformation("Mock VPN API accepted the connection request for server {ServerId}.", server.Id);
        return new ConnectResponse(true, server.Id, null, MockConfig);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Mock VPN API received a disconnect request.");
        await DelayAsync(700, 1200, cancellationToken).ConfigureAwait(false);
        logger.LogDebug("Mock VPN API completed the disconnect request.");
    }

    private static Task DelayAsync(int minMs, int maxMs, CancellationToken cancellationToken)
    {
        var delay = Random.Shared.Next(minMs, maxMs + 1);
        return Task.Delay(delay, cancellationToken);
    }
}
