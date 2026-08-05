using Grpc.Core;
using Google.Protobuf;
using PeekVPN.Contracts.Grpc;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;
using PeekVPN.Core.Vpn;
using Microsoft.Extensions.Logging;

namespace PeekVPN.Infrastructure.Grpc;

/// <summary>
/// Client-side <see cref="IVpnSession"/> implementation that talks to the PeekVPN background service over gRPC.
/// </summary>
public sealed class GrpcVpnSession : IVpnSession, IDisposable
{
    private readonly VpnService.VpnServiceClient _client;
    private readonly CancellationTokenSource _subscriptionCts = new();
    private readonly Task _subscriptionTask;
    private VpnSessionSnapshot _snapshot = new(Core.State.VpnConnectionState.Disconnected, null, null);

    private readonly ILogger<GrpcVpnSession> _logger;

    public GrpcVpnSession(VpnService.VpnServiceClient client, ILogger<GrpcVpnSession> logger)
    {
        _client = client;
        _logger = logger;
        _subscriptionTask = Task.Run(RunSubscriptionAsync);
    }

    public VpnSessionSnapshot Snapshot => Volatile.Read(ref _snapshot!);

    public event EventHandler<VpnSessionSnapshot>? StateChanged;

    public async Task ConnectAsync(VpnConnectionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Requesting VPN connection through the background service for {ServerId} using {Protocol}.",
            request.ServerId,
            request.Protocol);

        var grpcOptions = new global::PeekVPN.Contracts.Grpc.ConnectionOptions
        {
            KillSwitch = request.Options.KillSwitch,
            SplitTunnel = request.Options.SplitTunnel
        };
        grpcOptions.AllowedIps.AddRange(request.Options.AllowedIps);

        var grpcRequest = new ConnectRequest
        {
            Protocol = request.Protocol,
            ServerId = request.ServerId,
            Credentials = ByteString.CopyFrom(request.Credentials),
            Options = grpcOptions
        };

        await _client
            .ConnectAsync(grpcRequest, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Background service completed the VPN connection request for {ServerId}.",
            request.ServerId);

        await WaitForStateAsync(
            state => state is Core.State.VpnConnectionState.Connected or Core.State.VpnConnectionState.Disconnected,
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Requesting VPN disconnect through the background service.");
        await _client
            .DisconnectAsync(new DisconnectRequest(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        _logger.LogInformation("Background service completed the VPN disconnect request.");

        await WaitForStateAsync(state => state is Core.State.VpnConnectionState.Disconnected, cancellationToken)
            .ConfigureAwait(false);
    }

    public void CancelConnect()
    {
        _logger.LogInformation("Requesting cancellation of the pending VPN connection.");
        _client.Cancel(new CancelRequest());
    }

    public bool Pause()
    {
        _logger.LogInformation("Requesting VPN pause through the background service.");
        _client.Pause(new PauseRequest());
        return true;
    }

    public bool Resume()
    {
        _logger.LogInformation("Requesting VPN resume through the background service.");
        _client.Resume(new ResumeRequest());
        return true;
    }

    public void Dispose()
    {
        _subscriptionCts.Cancel();
        try
        {
            _subscriptionTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }
        finally
        {
            _subscriptionCts.Dispose();
        }
    }

    private async Task RunSubscriptionAsync()
    {
        while (!_subscriptionCts.IsCancellationRequested)
        {
            try
            {
                using var call = _client.SubscribeStatus(
                    new SubscribeStatusRequest(),
                    cancellationToken: _subscriptionCts.Token);

                await foreach (var status in call.ResponseStream
                    .ReadAllAsync(_subscriptionCts.Token)
                    .ConfigureAwait(false))
                {
                    SetSnapshot(Map(status));
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "VPN status subscription failed; retrying.");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), _subscriptionCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private async Task WaitForStateAsync(
        Func<Core.State.VpnConnectionState, bool> predicate,
        CancellationToken cancellationToken)
    {
        if (predicate(Snapshot.State))
        {
            return;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => tcs.TrySetCanceled());

        EventHandler<VpnSessionSnapshot>? handler = null;
        handler = (_, snapshot) =>
        {
            if (predicate(snapshot.State))
            {
                tcs.TrySetResult();
            }
        };

        StateChanged += handler;
        try
        {
            if (predicate(Snapshot.State))
            {
                tcs.TrySetResult();
            }

            await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            StateChanged -= handler;
        }
    }

    private void SetSnapshot(VpnSessionSnapshot snapshot)
    {
        Volatile.Write(ref _snapshot!, snapshot);
        StateChanged?.Invoke(this, snapshot);
    }

    private static VpnSessionSnapshot Map(ConnectionStatus status) => new(
        (Core.State.VpnConnectionState)status.State,
        string.IsNullOrEmpty(status.ActiveServerId) ? null : status.ActiveServerId,
        string.IsNullOrEmpty(status.LastError) ? null : status.LastError);
}
