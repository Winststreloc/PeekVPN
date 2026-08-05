using PeekVPN.Contracts;
using PeekVPN.Core.Abstractions;
using Microsoft.Extensions.Logging;

namespace PeekVPN.Core.Services;

public sealed class ServerCatalog(IVpnApiClient apiClient, ILogger<ServerCatalog> logger) : IServerCatalog
{
    public async Task<IReadOnlyList<VpnServerDto>> GetServersAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Fetching VPN server catalog.");
        try
        {
            var servers = await apiClient.GetCitiesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Fetched {ServerCount} VPN servers.", servers.Count);
            return servers;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogInformation("VPN server catalog fetch was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch VPN server catalog.");
            throw;
        }
    }
}
