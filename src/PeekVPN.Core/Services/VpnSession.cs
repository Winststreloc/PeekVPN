using PeekVPN.Contracts;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;

namespace PeekVPN.Core.Services;

public sealed class VpnSession(IVpnApiClient apiClient) : IVpnSession, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _connectCtsLock = new();
    private CancellationTokenSource? _connectCts;
    private VpnSessionSnapshot _snapshot = new(VpnConnectionState.Disconnected, null, null);

    public VpnSessionSnapshot Snapshot => Volatile.Read(ref _snapshot!);

    public event EventHandler<VpnSessionSnapshot>? StateChanged;

    public async Task ConnectAsync(string serverId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);

        CancellationTokenSource? linkedCts = null;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_snapshot.State is not VpnConnectionState.Disconnected)
            {
                return;
            }

            linkedCts = ReplaceConnectCts(cancellationToken);
            SetSnapshot(new VpnSessionSnapshot(VpnConnectionState.Connecting, serverId, null));
        }
        finally
        {
            _gate.Release();
        }

        if (linkedCts is null)
        {
            return;
        }

        try
        {
            var response = await apiClient
                .ConnectAsync(new ConnectRequest(serverId), linkedCts.Token)
                .ConfigureAwait(false);

            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_snapshot.State is not VpnConnectionState.Connecting)
                {
                    return;
                }

                if (response.Success)
                {
                    SetSnapshot(new VpnSessionSnapshot(
                        VpnConnectionState.Connected,
                        response.ServerId ?? serverId,
                        null));
                }
                else
                {
                    SetSnapshot(new VpnSessionSnapshot(
                        VpnConnectionState.Disconnected,
                        null,
                        response.ErrorMessage ?? "Connection failed."));
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_snapshot.State is VpnConnectionState.Connecting)
                {
                    SetSnapshot(new VpnSessionSnapshot(VpnConnectionState.Disconnected, null, null));
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_snapshot.State is VpnConnectionState.Connecting)
                {
                    SetSnapshot(new VpnSessionSnapshot(
                        VpnConnectionState.Disconnected,
                        null,
                        ex.Message));
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            ClearConnectCts(linkedCts);
        }
    }

    public void CancelConnect()
    {
        CancellationTokenSource? cts;
        lock (_connectCtsLock)
        {
            cts = _connectCts;
        }

        cts?.Cancel();
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_snapshot.State is not (VpnConnectionState.Connected or VpnConnectionState.Paused))
            {
                return;
            }

            SetSnapshot(new VpnSessionSnapshot(
                VpnConnectionState.Disconnecting,
                _snapshot.ActiveServerId,
                null));
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            await apiClient.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_snapshot.State is VpnConnectionState.Disconnecting)
                {
                    SetSnapshot(new VpnSessionSnapshot(
                        VpnConnectionState.Connected,
                        _snapshot.ActiveServerId,
                        null));
                }
            }
            finally
            {
                _gate.Release();
            }

            throw;
        }
        catch (Exception ex)
        {
            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_snapshot.State is VpnConnectionState.Disconnecting)
                {
                    SetSnapshot(new VpnSessionSnapshot(
                        VpnConnectionState.Connected,
                        _snapshot.ActiveServerId,
                        ex.Message));
                }
            }
            finally
            {
                _gate.Release();
            }

            return;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_snapshot.State is VpnConnectionState.Disconnecting)
            {
                SetSnapshot(new VpnSessionSnapshot(VpnConnectionState.Disconnected, null, null));
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool Pause()
    {
        _gate.Wait();
        try
        {
            if (_snapshot.State is not VpnConnectionState.Connected)
            {
                return false;
            }

            SetSnapshot(new VpnSessionSnapshot(
                VpnConnectionState.Paused,
                _snapshot.ActiveServerId,
                null));
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool Resume()
    {
        _gate.Wait();
        try
        {
            if (_snapshot.State is not VpnConnectionState.Paused)
            {
                return false;
            }

            SetSnapshot(new VpnSessionSnapshot(
                VpnConnectionState.Connected,
                _snapshot.ActiveServerId,
                null));
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        CancelConnect();
        lock (_connectCtsLock)
        {
            _connectCts?.Dispose();
            _connectCts = null;
        }

        _gate.Dispose();
    }

    private CancellationTokenSource ReplaceConnectCts(CancellationToken externalToken)
    {
        lock (_connectCtsLock)
        {
            _connectCts?.Cancel();
            _connectCts?.Dispose();
            _connectCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            return _connectCts;
        }
    }

    private void ClearConnectCts(CancellationTokenSource cts)
    {
        lock (_connectCtsLock)
        {
            if (ReferenceEquals(_connectCts, cts))
            {
                _connectCts.Dispose();
                _connectCts = null;
            }
        }
    }

    private void SetSnapshot(VpnSessionSnapshot snapshot)
    {
        Volatile.Write(ref _snapshot!, snapshot);
        StateChanged?.Invoke(this, snapshot);
    }
}
