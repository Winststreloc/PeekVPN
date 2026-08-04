using PeekVPN.Contracts;

namespace PeekVPN.Infrastructure.Http;

public sealed class MockVpnHttpService : IVpnApiClient
{
    private static readonly IReadOnlyList<VpnServerDto> Servers =
    [
        new("us-ny-42", "New York", "United States", "US", 24, "New York #42"),
        new("uk-lon-12", "London", "United Kingdom", "GB", 86, "London #12"),
        new("jp-tyo-05", "Tokyo", "Japan", "JP", 142, "Tokyo #05"),
        new("de-fra-08", "Frankfurt", "Germany", "DE", 54, "Frankfurt #08"),
        new("nl-ams-03", "Amsterdam", "Netherlands", "NL", 61, "Amsterdam #03"),
        new("sg-sgp-11", "Singapore", "Singapore", "SG", 178, "Singapore #11"),
        new("ca-tor-07", "Toronto", "Canada", "CA", 38, "Toronto #07"),
        new("au-syd-02", "Sydney", "Australia", "AU", 210, "Sydney #02")
    ];

    private readonly Random _random = new();

    public async Task<IReadOnlyList<VpnServerDto>> GetCitiesAsync(CancellationToken cancellationToken = default)
    {
        await DelayAsync(350, 700, cancellationToken).ConfigureAwait(false);
        return Servers;
    }

    public async Task<ConnectResponse> ConnectAsync(
        ConnectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ServerId);

        await DelayAsync(900, 1500, cancellationToken).ConfigureAwait(false);

        var server = Servers.FirstOrDefault(s => s.Id == request.ServerId);
        if (server is null)
        {
            return new ConnectResponse(false, null, "Server not found.");
        }

        // Occasional failure so the UI can exercise the error path.
        if (_random.NextDouble() < 0.05)
        {
            return new ConnectResponse(false, null, "Unable to reach the selected server.");
        }

        return new ConnectResponse(true, server.Id, null);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await DelayAsync(700, 1200, cancellationToken).ConfigureAwait(false);
    }

    private static Task DelayAsync(int minMs, int maxMs, CancellationToken cancellationToken)
    {
        var delay = Random.Shared.Next(minMs, maxMs + 1);
        return Task.Delay(delay, cancellationToken);
    }
}
