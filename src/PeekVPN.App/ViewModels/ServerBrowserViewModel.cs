using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeekVPN.App.Localization;
using PeekVPN.App.Services;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Owns the server catalog presentation, filtering, recents, selection, and row state.
/// </summary>
public partial class ServerBrowserViewModel : SessionObserverViewModel
{
    private const int MaxRecentServers = 3;

    private readonly IServerLookup _serverLookup;
    private readonly IVpnConnectionRequestFactory _requestFactory;
    private readonly List<ServerItemViewModel> _allServers = [];
    private readonly List<string> _recentServerIds = [];
    private VpnConnectionState _previousState;

    public ServerBrowserViewModel(
        IVpnSession session,
        IServerLookup serverLookup,
        IVpnConnectionRequestFactory requestFactory)
        : base(session)
    {
        _serverLookup = serverLookup;
        _requestFactory = requestFactory;
        _previousState = session.Snapshot.State;
        _ = LoadServersAsync();
    }

    public ObservableCollection<ServerItemViewModel> RecentServers { get; } = [];

    public ObservableCollection<ServerItemViewModel> FilteredServers { get; } = [];

    public string RecentConnectionsTitle => Strings.RecentConnectionsTitle;

    public string AllServersTitle => Strings.AllServersTitle;

    public string SearchServersWatermark => Strings.SearchServersWatermark;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = Strings.LoadingServers;

    [ObservableProperty]
    public partial string RecentEmptyMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ServerItemViewModel? SelectedServer { get; set; }

    partial void OnSearchQueryChanged(string value) => RefreshServerLists();

    // This remains concurrent so row selection remains responsive while a connection changes state.
    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task ConnectServerAsync(ServerItemViewModel? server)
    {
        if (server is null)
        {
            return;
        }

        SelectServer(server);

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
            var request = await _requestFactory.CreateAsync(server.Id).ConfigureAwait(false);
            await Session.ConnectAsync(request).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private void SelectServer(ServerItemViewModel? server)
    {
        if (server is null)
        {
            return;
        }

        foreach (var item in _allServers)
        {
            item.IsSelected = item.Id == server.Id;
        }

        SelectedServer = server;
    }

    protected override void OnSessionSnapshotChanged(VpnSessionSnapshot snapshot)
    {
        // SessionObserverViewModel dispatches the initial snapshot from its base constructor.
        // Derived field initializers have not run at that point.
        if (_allServers is null)
        {
            return;
        }

        if (_previousState is VpnConnectionState.Connecting
            && snapshot.State is VpnConnectionState.Connected
            && snapshot.ActiveServerId is not null)
        {
            RememberRecent(snapshot.ActiveServerId);
            RefreshServerLists();
        }

        _previousState = snapshot.State;
        SyncServerConnectionStates(snapshot);
    }

    private async Task LoadServersAsync()
    {
        try
        {
            StatusMessage = Strings.LoadingServers;
            var servers = await _serverLookup.GetServersAsync();

            _allServers.Clear();
            _allServers.AddRange(servers.Select(server => new ServerItemViewModel(server, ConnectServerCommand)));

            foreach (var server in _allServers.Take(MaxRecentServers))
            {
                RememberRecent(server.Id);
            }

            RefreshServerLists();
            SyncServerConnectionStates(Session.Snapshot);
            StatusMessage = string.Empty;

            if (SelectedServer is null && RecentServers.Count > 0)
            {
                SelectServer(RecentServers[0]);
            }
        }
        catch
        {
            StatusMessage = Strings.ErrorLoadServers;
        }
    }

    private void RememberRecent(string serverId)
    {
        _recentServerIds.Remove(serverId);
        _recentServerIds.Insert(0, serverId);
        if (_recentServerIds.Count > MaxRecentServers)
        {
            _recentServerIds.RemoveRange(MaxRecentServers, _recentServerIds.Count - MaxRecentServers);
        }
    }

    private void RefreshServerLists()
    {
        IEnumerable<ServerItemViewModel> filtered = _allServers;
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            filtered = _allServers.Where(server => MatchesSearch(server, SearchQuery));
        }

        RecentServers.Clear();
        foreach (var id in _recentServerIds)
        {
            var server = filtered.FirstOrDefault(candidate => candidate.Id == id);
            if (server is not null)
            {
                RecentServers.Add(server);
            }
        }

        RecentEmptyMessage = RecentServers.Count == 0 ? Strings.EmptyRecent : string.Empty;

        FilteredServers.Clear();
        foreach (var server in filtered)
        {
            FilteredServers.Add(server);
        }
    }

    internal static bool MatchesSearch(ServerItemViewModel server, string query)
    {
        var trimmedQuery = query.Trim();
        return server.Country.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)
            || server.City.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)
            || server.CityLabel.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase)
            || server.CountryCode.Contains(trimmedQuery, StringComparison.OrdinalIgnoreCase);
    }

    private void SyncServerConnectionStates(VpnSessionSnapshot snapshot)
    {
        foreach (var server in _allServers)
        {
            server.ConnectionState = server.Id == snapshot.ActiveServerId
                ? snapshot.State
                : VpnConnectionState.Disconnected;
        }
    }
}
