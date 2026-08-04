using CommunityToolkit.Mvvm.ComponentModel;
using PeekVPN.App.Localization;
using PeekVPN.App.Services;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Presents map-specific session context and leaves server pins to a future map overlay.
/// </summary>
public sealed partial class MapViewModel : SessionObserverViewModel
{
    private readonly IServerLookup? _serverLookup;
    private VpnSessionSnapshot _snapshot = new(VpnConnectionState.Disconnected, null, null);

    public MapViewModel(IVpnSession session, IServerLookup serverLookup)
        : base(session)
    {
        _serverLookup = serverLookup;
        OnSessionSnapshotChanged(Session.Snapshot);
    }

    public string Title => Strings.MapTitle;

    [ObservableProperty]
    public partial ServerDisplayMetadata? ActiveServer { get; private set; }

    protected override void OnSessionSnapshotChanged(VpnSessionSnapshot snapshot)
    {
        var serverLookup = _serverLookup;
        if (serverLookup is null)
        {
            return;
        }

        _snapshot = snapshot;
        ActiveServer = snapshot.ActiveServerId is not null
            && serverLookup.TryGetById(snapshot.ActiveServerId, out var server)
            ? server
            : null;

        if (snapshot.ActiveServerId is not null && ActiveServer is null)
        {
            _ = ResolveActiveServerAsync(snapshot, serverLookup);
        }
    }

    private async Task ResolveActiveServerAsync(VpnSessionSnapshot snapshot, IServerLookup serverLookup)
    {
        try
        {
            var server = await serverLookup.FindByIdAsync(snapshot.ActiveServerId!);
            if (_snapshot == snapshot)
            {
                ActiveServer = server;
            }
        }
        catch
        {
            // The map remains usable without optional server display metadata.
        }
    }
}
