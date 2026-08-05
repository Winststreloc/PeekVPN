using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeekVPN.App.Localization;
using PeekVPN.App.Services;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Presents map server markers and keeps their session state in sync with the VPN session.
/// </summary>
public sealed partial class MapViewModel : SessionObserverViewModel
{
    private readonly IServerLookup? _serverLookup;
    private readonly IVpnConnectionRequestFactory _requestFactory;
    private VpnSessionSnapshot _snapshot = new(VpnConnectionState.Disconnected, null, null);

    public MapViewModel(
        IVpnSession session,
        IServerLookup serverLookup,
        IVpnConnectionRequestFactory requestFactory)
        : base(session)
    {
        _serverLookup = serverLookup;
        _requestFactory = requestFactory;
        OnSessionSnapshotChanged(Session.Snapshot);
        _ = LoadMarkersAsync(serverLookup);
    }

    public string Title => Strings.MapTitle;

    public ObservableCollection<MapMarkerViewModel> Markers { get; } = [];

    [ObservableProperty]
    public partial ServerDisplayMetadata? ActiveServer { get; private set; }

    /// <summary>Changes only when a new active server needs to be brought into view.</summary>
    [ObservableProperty]
    public partial MapMarkerViewModel? FocusTarget { get; private set; }

    protected override void OnSessionSnapshotChanged(VpnSessionSnapshot snapshot)
    {
        var serverLookup = _serverLookup;
        if (serverLookup is null)
        {
            return;
        }

        _snapshot = snapshot;
        var activeServer = snapshot.ActiveServerId is not null
            && serverLookup.TryGetById(snapshot.ActiveServerId, out var server)
            ? server
            : null;
        var activeServerChanged = !string.Equals(ActiveServer?.Id, activeServer?.Id, StringComparison.Ordinal);
        ActiveServer = activeServer;
        SyncMarkerStates(snapshot);

        if (snapshot.ActiveServerId is not null && ActiveServer is null)
        {
            _ = ResolveActiveServerAsync(snapshot, serverLookup);
        }
        else if (activeServerChanged && activeServer is not null)
        {
            FocusTarget = Markers.FirstOrDefault(marker => marker.Id == activeServer.Id);
        }
    }

    private async Task ResolveActiveServerAsync(VpnSessionSnapshot snapshot, IServerLookup serverLookup)
    {
        try
        {
            var server = await serverLookup.FindByIdAsync(snapshot.ActiveServerId!);
            if (_snapshot == snapshot)
            {
                var activeServerChanged = !string.Equals(ActiveServer?.Id, server?.Id, StringComparison.Ordinal);
                ActiveServer = server;
                SyncMarkerStates(snapshot);
                if (activeServerChanged && server is not null)
                {
                    FocusTarget = Markers.FirstOrDefault(marker => marker.Id == server.Id);
                }
            }
        }
        catch
        {
            // The map remains usable without optional server display metadata.
        }
    }

    private async Task LoadMarkersAsync(IServerLookup serverLookup)
    {
        try
        {
            var servers = await serverLookup.GetServersAsync();
            Markers.Clear();
            foreach (var server in servers)
            {
                Markers.Add(new MapMarkerViewModel(server));
            }

            SyncMarkerStates(_snapshot);
            if (ActiveServer is not null)
            {
                FocusTarget = Markers.FirstOrDefault(marker => marker.Id == ActiveServer.Id);
            }
        }
        catch
        {
            // The base map remains available when the optional server catalog cannot be loaded.
        }
    }

    /// <summary>Applies the same connect/switch behavior as a server-list row for a clicked map marker.</summary>
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ActivateMarkerAsync(MapMarkerViewModel? marker)
    {
        if (marker is null)
        {
            return;
        }

        // Set this before any awaited session work. The control also starts a local focus
        // transition for repeated marker clicks where the property value is unchanged.
        FocusTarget = marker;

        if (Session.Snapshot.State is VpnConnectionState.Connecting or VpnConnectionState.Disconnecting)
        {
            return;
        }

        if (Session.Snapshot.State is VpnConnectionState.Connected or VpnConnectionState.Paused)
        {
            await Session.DisconnectAsync();
        }

        if (Session.Snapshot.State is VpnConnectionState.Disconnected)
        {
            var request = await _requestFactory.CreateAsync(marker.Id).ConfigureAwait(false);
            await Session.ConnectAsync(request).ConfigureAwait(false);
        }
    }

    private void SyncMarkerStates(VpnSessionSnapshot snapshot)
    {
        if (Markers is null)
        {
            return;
        }

        foreach (var marker in Markers)
        {
            marker.ConnectionState = marker.Id == snapshot.ActiveServerId
                ? snapshot.State
                : VpnConnectionState.Disconnected;
        }
    }
}
