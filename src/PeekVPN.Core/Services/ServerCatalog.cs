using PeekVPN.Contracts;
using PeekVPN.Core.Abstractions;

namespace PeekVPN.Core.Services;

public sealed class ServerCatalog(IVpnApiClient apiClient) : IServerCatalog
{
    public Task<IReadOnlyList<VpnServerDto>> GetServersAsync(CancellationToken cancellationToken = default)
        => apiClient.GetCitiesAsync(cancellationToken);
}
