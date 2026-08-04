using PeekVPN.Core.State;

namespace PeekVPN.App.Services;

/// <summary>
/// Supplies presentation-ready connection telemetry for the stats feature.
/// Replace the mock implementation when tunnel telemetry becomes available.
/// </summary>
public interface IConnectionStatsProvider
{
    ConnectionStatsSnapshot GetStats(
        VpnSessionSnapshot sessionSnapshot,
        ServerDisplayMetadata? server,
        int weeklyConnectionCount);
}

/// <summary>
/// Values displayed by <c>StatsSummaryView</c>.
/// </summary>
public sealed record ConnectionStatsSnapshot(
    int WeeklyConnectionCount,
    string PingValue,
    string SpeedValue);
