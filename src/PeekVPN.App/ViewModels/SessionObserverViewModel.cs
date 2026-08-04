using Avalonia.Threading;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Safely observes VPN session snapshots from presentation view-models.
/// </summary>
public abstract class SessionObserverViewModel : ViewModelBase, IDisposable
{
    private readonly IVpnSession _session;
    private int _isDisposed;

    protected SessionObserverViewModel(IVpnSession session)
    {
        _session = session;
        _session.StateChanged += OnSessionStateChanged;
        DispatchSnapshot(_session.Snapshot);
    }

    protected IVpnSession Session => _session;

    protected abstract void OnSessionSnapshotChanged(VpnSessionSnapshot snapshot);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        _session.StateChanged -= OnSessionStateChanged;
        GC.SuppressFinalize(this);
    }

    private void OnSessionStateChanged(object? sender, VpnSessionSnapshot snapshot) =>
        DispatchSnapshot(snapshot);

    private void DispatchSnapshot(VpnSessionSnapshot snapshot)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            ApplySnapshotIfActive(snapshot);
            return;
        }

        Dispatcher.UIThread.Post(() => ApplySnapshotIfActive(snapshot));
    }

    private void ApplySnapshotIfActive(VpnSessionSnapshot snapshot)
    {
        if (Volatile.Read(ref _isDisposed) == 0)
        {
            OnSessionSnapshotChanged(snapshot);
        }
    }
}
