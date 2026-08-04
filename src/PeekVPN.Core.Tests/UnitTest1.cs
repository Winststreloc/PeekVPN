using PeekVPN.Contracts;
using PeekVPN.Core.Services;
using PeekVPN.Core.State;

namespace PeekVPN.Core.Tests;

public sealed class VpnSessionTests
{
    [Fact]
    public async Task ConnectAsync_successfully_transitions_to_connected_and_notifies()
    {
        var api = new ControlledVpnApiClient
        {
            ConnectResult = new ConnectResponse(true, "server-2", null),
        };
        using var session = new VpnSession(api);
        var states = new List<VpnConnectionState>();
        session.StateChanged += (_, snapshot) => states.Add(snapshot.State);

        await session.ConnectAsync("server-1");

        Assert.Equal(new[] { VpnConnectionState.Connecting, VpnConnectionState.Connected }, states);
        Assert.Equal(new VpnSessionSnapshot(VpnConnectionState.Connected, "server-2", null), session.Snapshot);
    }

    [Fact]
    public async Task CancelConnect_cancels_request_and_returns_to_disconnected()
    {
        var api = new ControlledVpnApiClient { WaitForConnectCancellation = true };
        using var session = new VpnSession(api);
        var connectTask = session.ConnectAsync("server-1");

        await api.ConnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(VpnConnectionState.Connecting, session.Snapshot.State);

        session.CancelConnect();
        await connectTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(api.ConnectWasCanceled);
        Assert.Equal(new VpnSessionSnapshot(VpnConnectionState.Disconnected, null, null), session.Snapshot);
    }

    [Fact]
    public async Task Pause_resume_and_disconnect_follow_valid_transition_path()
    {
        var api = new ControlledVpnApiClient
        {
            ConnectResult = new ConnectResponse(true, "server-1", null),
        };
        using var session = new VpnSession(api);

        await session.ConnectAsync("server-1");

        Assert.True(session.Pause());
        Assert.Equal(VpnConnectionState.Paused, session.Snapshot.State);
        Assert.True(session.Resume());
        Assert.Equal(VpnConnectionState.Connected, session.Snapshot.State);

        await session.DisconnectAsync();

        Assert.Equal(new VpnSessionSnapshot(VpnConnectionState.Disconnected, null, null), session.Snapshot);
        Assert.Equal(1, api.DisconnectCalls);
    }

    [Fact]
    public async Task DisconnectAsync_cancellation_restores_connected_state_and_propagates()
    {
        var api = new ControlledVpnApiClient
        {
            ConnectResult = new ConnectResponse(true, "server-1", null),
            WaitForDisconnectCancellation = true,
        };
        using var session = new VpnSession(api);
        await session.ConnectAsync("server-1");
        using var cancellation = new CancellationTokenSource();

        var disconnectTask = session.DisconnectAsync(cancellation.Token);
        await api.DisconnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(VpnConnectionState.Disconnecting, session.Snapshot.State);

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => disconnectTask);
        Assert.Equal(new VpnSessionSnapshot(VpnConnectionState.Connected, "server-1", null), session.Snapshot);
    }

    private sealed class ControlledVpnApiClient : IVpnApiClient
    {
        public TaskCompletionSource<bool> ConnectStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<bool> DisconnectStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ConnectResponse ConnectResult { get; init; } = new(true, "server-1", null);

        public bool WaitForConnectCancellation { get; init; }

        public bool WaitForDisconnectCancellation { get; init; }

        public bool ConnectWasCanceled { get; private set; }

        public int DisconnectCalls { get; private set; }

        public Task<IReadOnlyList<VpnServerDto>> GetCitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VpnServerDto>>([]);

        public Task<ConnectResponse> ConnectAsync(
            ConnectRequest request,
            CancellationToken cancellationToken = default)
        {
            ConnectStarted.TrySetResult(true);
            if (!WaitForConnectCancellation)
            {
                return Task.FromResult(ConnectResult);
            }

            return WaitForCancellationAsync<ConnectResponse>(
                cancellationToken,
                () => ConnectWasCanceled = true);
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectCalls++;
            DisconnectStarted.TrySetResult(true);
            return WaitForDisconnectCancellation
                ? WaitForCancellationAsync(cancellationToken)
                : Task.CompletedTask;
        }

        private static Task WaitForCancellationAsync(CancellationToken cancellationToken, Action? onCanceled = null)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() =>
            {
                onCanceled?.Invoke();
                completion.TrySetCanceled(cancellationToken);
            });
            return completion.Task;
        }

        private static async Task<T> WaitForCancellationAsync<T>(
            CancellationToken cancellationToken,
            Action? onCanceled = null)
        {
            await WaitForCancellationAsync(cancellationToken, onCanceled);
            return default!;
        }
    }
}
