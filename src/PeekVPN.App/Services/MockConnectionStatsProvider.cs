using PeekVPN.App.Localization;
using PeekVPN.Core.State;

namespace PeekVPN.App.Services;

/// <summary>
/// Temporary UI-only telemetry. This does not inspect the network or VPN tunnel.
/// </summary>
public sealed class MockConnectionStatsProvider : IConnectionStatsProvider
{
    public ConnectionStatsSnapshot GetStats(
        VpnSessionSnapshot sessionSnapshot,
        ServerDisplayMetadata? server,
        int weeklyConnectionCount)
    {
        var isTunnelActive = sessionSnapshot.State is VpnConnectionState.Connecting
            or VpnConnectionState.Connected
            or VpnConnectionState.Paused
            or VpnConnectionState.Disconnecting;

        if (!isTunnelActive)
        {
            return new ConnectionStatsSnapshot(
                weeklyConnectionCount,
                Strings.StatsPlaceholder,
                Strings.StatsPlaceholder);
        }

        var pingMs = server?.LatencyMs ?? 42;
        var downloadMbps = Math.Max(12, 120 - pingMs / 2);
        var uploadMbps = Math.Max(8, 55 - pingMs / 4);

        return new ConnectionStatsSnapshot(
            weeklyConnectionCount,
            string.Format(Strings.LatencyFormat, pingMs),
            string.Format(Strings.StatsSpeedValue, $"{downloadMbps} Mbps", $"{uploadMbps} Mbps"));
    }
}
