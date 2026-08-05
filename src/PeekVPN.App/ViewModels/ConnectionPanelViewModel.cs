using CommunityToolkit.Mvvm.ComponentModel;
using PeekVPN.App.Services;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Selects the presentation model for the current session state and resolves its server metadata.
/// </summary>
public sealed partial class ConnectionPanelViewModel : SessionObserverViewModel
{
    private IServerLookup? _serverLookup;
    private readonly IVpnConnectionRequestFactory _requestFactory;
    private int _presentationVersion;

    [ObservableProperty]
    private ConnectionStateViewModelBase? _activeState;

    [ObservableProperty]
    private string? _selectedServerId;

    public ConnectionPanelViewModel(
        IVpnSession session,
        IServerLookup serverLookup,
        IVpnConnectionRequestFactory requestFactory)
        : base(session)
    {
        _serverLookup = serverLookup;
        _requestFactory = requestFactory;
        _ = RefreshPresenterAsync(session.Snapshot);
    }

    partial void OnSelectedServerIdChanged(string? value) => _ = RefreshPresenterAsync(Session.Snapshot);

    protected override void OnSessionSnapshotChanged(VpnSessionSnapshot snapshot)
    {
        // Present immediately, then replace it once the async registry lookup finishes.
        ActiveState = CreatePresenter(snapshot, null);
        _ = RefreshPresenterAsync(snapshot);
    }

    private async Task RefreshPresenterAsync(VpnSessionSnapshot snapshot)
    {
        var serverLookup = _serverLookup;
        if (serverLookup is null)
        {
            return;
        }

        var version = Interlocked.Increment(ref _presentationVersion);
        ServerDisplayMetadata? server = null;

        if (!string.IsNullOrWhiteSpace(snapshot.ActiveServerId))
        {
            server = await serverLookup.FindByIdAsync(snapshot.ActiveServerId);
        }
        else if (!string.IsNullOrWhiteSpace(SelectedServerId))
        {
            server = await serverLookup.FindByIdAsync(SelectedServerId);
        }
        else if (snapshot.State is VpnConnectionState.Disconnected)
        {
            server = (await serverLookup.GetServersAsync()).FirstOrDefault();
            if (server is not null)
            {
                SelectedServerId = server.Id;
                return;
            }
        }

        if (version == Volatile.Read(ref _presentationVersion))
        {
            ActiveState = CreatePresenter(snapshot, server);
        }
    }

    private ConnectionStateViewModelBase CreatePresenter(
        VpnSessionSnapshot snapshot,
        ServerDisplayMetadata? server) =>
        snapshot.State switch
        {
            VpnConnectionState.Disconnected => new DisconnectedStateViewModel(Session, _requestFactory, snapshot, server),
            VpnConnectionState.Connecting => new ConnectingStateViewModel(Session, snapshot, server),
            VpnConnectionState.Connected => new ConnectedStateViewModel(Session, snapshot, server),
            VpnConnectionState.Paused => new PausedStateViewModel(Session, snapshot, server),
            VpnConnectionState.Disconnecting => new DisconnectingStateViewModel(Session, snapshot, server),
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.State, "Unknown VPN state.")
        };
}
