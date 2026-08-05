using PeekVPN.Contracts;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;
using PeekVPN.Core.Vpn;
using Microsoft.Extensions.Logging;

namespace PeekVPN.Core.Services;

public sealed class VpnSession(
    IVpnConnectionOrchestrator orchestrator,
    IVpnApiClient apiClient,
    ILogger<VpnSession> logger) : IVpnSession, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _connectCtsLock = new();
    private CancellationTokenSource? _connectCts;
    private VpnSessionSnapshot _snapshot = new(VpnConnectionState.Disconnected, null, null);

    public VpnSessionSnapshot Snapshot => Volatile.Read(ref _snapshot!);

    public event EventHandler<VpnSessionSnapshot>? StateChanged;

    public async Task ConnectAsync(VpnConnectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var effectiveRequest = request.Credentials.Length > 0
            ? request
            : request with { Credentials = await FetchCredentialsAsync(request.ServerId, cancellationToken).ConfigureAwait(false) };

        CancellationTokenSource? linkedCts = null;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_snapshot.State is not VpnConnectionState.Disconnected)
            {
                return;
            }

            linkedCts = ReplaceConnectCts(cancellationToken);
            SetSnapshot(new VpnSessionSnapshot(VpnConnectionState.Connecting, request.ServerId, null));
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
            var result = await orchestrator
                .ConnectAsync(effectiveRequest, linkedCts.Token)
                .ConfigureAwait(false);

            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_snapshot.State is not VpnConnectionState.Connecting)
                {
                    return;
                }

                if (result.IsSuccessful)
                {
                    SetSnapshot(new VpnSessionSnapshot(
                        VpnConnectionState.Connected,
                        request.ServerId,
                        null,
                        _snapshot.Config));
                }
                else
                {
                    SetSnapshot(new VpnSessionSnapshot(
                        VpnConnectionState.Disconnected,
                        null,
                        result.ErrorMessage ?? "Connection failed."));
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
            logger.LogError(
                ex,
                "VPN connection to server {ServerId} failed; transitioning the session to disconnected.",
                request.ServerId);

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
                null,
                _snapshot.Config));
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            await orchestrator.DisconnectAsync(cancellationToken).ConfigureAwait(false);
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
                        null,
                        _snapshot.Config));
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
            logger.LogError(
                ex,
                "VPN disconnect failed; restoring the session to connected.");

            await _gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (_snapshot.State is VpnConnectionState.Disconnecting)
                {
                    SetSnapshot(new VpnSessionSnapshot(
                        VpnConnectionState.Connected,
                        _snapshot.ActiveServerId,
                        ex.Message,
                        _snapshot.Config));
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
                null,
                _snapshot.Config));
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
                null,
                _snapshot.Config));
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

    private async Task<byte[]> FetchCredentialsAsync(string serverId, CancellationToken cancellationToken)
    {
        var config = await apiClient
            .GetCredentialsAsync(serverId, cancellationToken)
            .ConfigureAwait(false);

        if (config is null)
        {
            throw new InvalidOperationException("Failed to fetch VPN credentials.");
        }

        return System.Text.Encoding.UTF8.GetBytes(config.RawConfig);
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
