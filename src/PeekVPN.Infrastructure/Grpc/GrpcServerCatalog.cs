using PeekVPN.Contracts;
using PeekVPN.Contracts.Grpc;
using PeekVPN.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace PeekVPN.Infrastructure.Grpc;

/// <summary>
/// Client-side <see cref="IServerCatalog"/> that fetches the server list from the background service.
/// </summary>
public sealed class GrpcServerCatalog(
    VpnService.VpnServiceClient client,
    ILogger<GrpcServerCatalog> logger) : IServerCatalog
{
    public async Task<IReadOnlyList<VpnServerDto>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Requesting VPN server catalog from the background service.");
        try
        {
            var response = await client
                .GetServersAsync(new GetServersRequest(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var servers = response.Servers
                .Select(server => new VpnServerDto(
                    server.Id,
                    server.City,
                    server.Country,
                    server.CountryCode,
                    server.LatencyMs,
                    server.DisplayName,
                    server.Latitude,
                    server.Longitude))
                .ToArray();
            logger.LogInformation("Received {ServerCount} VPN servers from the background service.", servers.Length);
            return servers;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("Background-service server catalog request was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to request VPN server catalog from the background service.");
            throw;
        }
    }
}
