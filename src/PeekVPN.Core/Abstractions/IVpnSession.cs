using PeekVPN.Core.State;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Core.Abstractions;

public interface IVpnSession
{
    VpnSessionSnapshot Snapshot { get; }

    event EventHandler<VpnSessionSnapshot>? StateChanged;

    Task ConnectAsync(VpnConnectionRequest request, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    /// <summary>Cancels an in-progress connect and returns the session to Disconnected.</summary>
    void CancelConnect();

    bool Pause();

    bool Resume();
}
