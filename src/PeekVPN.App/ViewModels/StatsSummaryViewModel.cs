using CommunityToolkit.Mvvm.ComponentModel;
using PeekVPN.App.Localization;
using PeekVPN.App.Services;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Presents summary telemetry from session snapshots without owning VPN transitions.
/// </summary>
public sealed partial class StatsSummaryViewModel : SessionObserverViewModel
{
    private readonly IConnectionStatsProvider? _statsProvider;
    private readonly IServerLookup? _serverLookup;
    private VpnSessionSnapshot _previousSnapshot = new(VpnConnectionState.Disconnected, null, null);
    private VpnSessionSnapshot _snapshot = new(VpnConnectionState.Disconnected, null, null);
    private int _weeklyConnectionCount = 4;

    public StatsSummaryViewModel(
        IVpnSession session,
        IServerLookup serverLookup,
        IConnectionStatsProvider statsProvider)
        : base(session)
    {
        _serverLookup = serverLookup;
        _statsProvider = statsProvider;
        OnSessionSnapshotChanged(Session.Snapshot);
    }

    public string Title => Strings.StatsSummaryTitle;
    public string WeeklyConnectionsLabel => Strings.StatsWeeklyConnections;
    public string PingLabel => Strings.StatsPing;
    public string SpeedLabel => Strings.StatsSpeed;

    [ObservableProperty]
    public partial string WeeklyConnectionsValue { get; private set; } =
        string.Format(Strings.StatsWeeklyConnectionsValue, 4);

    [ObservableProperty]
    public partial string PingValue { get; private set; } = Strings.StatsPlaceholder;

    [ObservableProperty]
    public partial string SpeedValue { get; private set; } = Strings.StatsPlaceholder;

    protected override void OnSessionSnapshotChanged(VpnSessionSnapshot snapshot)
    {
        var serverLookup = _serverLookup;
        var statsProvider = _statsProvider;
        if (serverLookup is null || statsProvider is null)
        {
            return;
        }

        if (_previousSnapshot.State is VpnConnectionState.Connecting
            && snapshot.State is VpnConnectionState.Connected
            && snapshot.ActiveServerId is not null)
        {
            _weeklyConnectionCount++;
        }

        _previousSnapshot = snapshot;
        _snapshot = snapshot;

        var server = snapshot.ActiveServerId is not null
            && serverLookup.TryGetById(snapshot.ActiveServerId, out var cachedServer)
            ? cachedServer
            : null;

        ApplyStats(snapshot, server, statsProvider);

        if (snapshot.ActiveServerId is not null && server is null)
        {
            _ = RefreshServerStatsAsync(snapshot, serverLookup, statsProvider);
        }
    }

    private async Task RefreshServerStatsAsync(
        VpnSessionSnapshot snapshot,
        IServerLookup serverLookup,
        IConnectionStatsProvider statsProvider)
    {
        try
        {
            var server = await serverLookup.FindByIdAsync(snapshot.ActiveServerId!);
            if (_snapshot == snapshot)
            {
                ApplyStats(snapshot, server, statsProvider);
            }
        }
        catch
        {
            // Preserve the fallback telemetry when server metadata cannot be loaded.
        }
    }

    private void ApplyStats(
        VpnSessionSnapshot snapshot,
        ServerDisplayMetadata? server,
        IConnectionStatsProvider statsProvider)
    {
        var stats = statsProvider.GetStats(snapshot, server, _weeklyConnectionCount);
        WeeklyConnectionsValue = string.Format(
            Strings.StatsWeeklyConnectionsValue,
            stats.WeeklyConnectionCount);
        PingValue = stats.PingValue;
        SpeedValue = stats.SpeedValue;
    }
}
