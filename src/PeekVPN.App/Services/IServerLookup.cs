namespace PeekVPN.App.Services;

/// <summary>
/// Provides the shared, cached server metadata used by App-layer features.
/// </summary>
public interface IServerLookup
{
    Task<IReadOnlyList<ServerDisplayMetadata>> GetServersAsync(
        CancellationToken cancellationToken = default);

    Task<ServerDisplayMetadata?> FindByIdAsync(
        string serverId,
        CancellationToken cancellationToken = default);

    bool TryGetById(string serverId, out ServerDisplayMetadata? server);
}
