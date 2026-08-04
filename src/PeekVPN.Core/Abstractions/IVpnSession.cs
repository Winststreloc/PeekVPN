using PeekVPN.Core.State;

namespace PeekVPN.Core.Abstractions;

public interface IVpnSession
{
    VpnSessionSnapshot Snapshot { get; }

    event EventHandler<VpnSessionSnapshot>? StateChanged;

    Task ConnectAsync(string serverId, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancels an in-progress connect and returns the session to Disconnected.</summary>
    void CancelConnect();

    bool Pause();

    bool Resume();
}
