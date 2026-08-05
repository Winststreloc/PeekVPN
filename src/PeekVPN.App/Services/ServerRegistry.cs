using PeekVPN.App.Helpers;
using PeekVPN.Core.Abstractions;

namespace PeekVPN.App.Services;

/// <summary>
/// Loads server metadata once for the App layer and makes it available by ID.
/// </summary>
public sealed class ServerRegistry(IServerCatalog serverCatalog) : IServerLookup
{
    private readonly object _loadLock = new();
    private Task<IReadOnlyList<ServerDisplayMetadata>>? _loadTask;
    private IReadOnlyDictionary<string, ServerDisplayMetadata> _serversById =
        new Dictionary<string, ServerDisplayMetadata>(StringComparer.Ordinal);

    public async Task<IReadOnlyList<ServerDisplayMetadata>> GetServersAsync(
        CancellationToken cancellationToken = default)
    {
        Task<IReadOnlyList<ServerDisplayMetadata>> loadTask;
        lock (_loadLock)
        {
            loadTask = _loadTask ??= LoadServersAsync();
        }

        try
        {
            return await loadTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch when (loadTask.IsFaulted)
        {
            // Do not permanently cache a failed catalog request; the next caller may retry.
            lock (_loadLock)
            {
                if (ReferenceEquals(_loadTask, loadTask))
                {
                    _loadTask = null;
                }
            }

            throw;
        }
    }

    public async Task<ServerDisplayMetadata?> FindByIdAsync(
        string serverId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        await GetServersAsync(cancellationToken).ConfigureAwait(false);
        return TryGetById(serverId, out var server) ? server : null;
    }

    public bool TryGetById(string serverId, out ServerDisplayMetadata? server)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        return _serversById.TryGetValue(serverId, out server);
    }

    private async Task<IReadOnlyList<ServerDisplayMetadata>> LoadServersAsync()
    {
        var servers = await serverCatalog.GetServersAsync().ConfigureAwait(false);
        var metadata = servers
            .Select(server => new ServerDisplayMetadata(
                server.Id,
                server.City,
                server.Country,
                server.CountryCode,
                server.LatencyMs,
                server.DisplayName,
                CountryFlagAssets.GetUri(server.CountryCode),
                server.Latitude,
                server.Longitude))
            .ToArray();

        _serversById = metadata.ToDictionary(server => server.Id, StringComparer.Ordinal);
        return metadata;
    }
}
