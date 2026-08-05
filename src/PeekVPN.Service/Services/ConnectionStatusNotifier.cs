using System.Collections.Concurrent;
using System.Threading.Channels;
using PeekVPN.Contracts.Grpc;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;

namespace PeekVPN.Service.Services;

/// <summary>
/// Forwards <see cref="IVpnSession.StateChanged"/> events to all active gRPC subscribers.
/// </summary>
public sealed class ConnectionStatusNotifier : IDisposable
{
    private readonly IVpnSession _session;
    private readonly ConcurrentDictionary<Guid, Channel<ConnectionStatus>> _subscribers = new();

    public ConnectionStatusNotifier(IVpnSession session)
    {
        _session = session;
        _session.StateChanged += OnStateChanged;
    }

    public (Guid Id, ChannelReader<ConnectionStatus> Reader) Subscribe()
    {
        var channel = Channel.CreateUnbounded<ConnectionStatus>();
        var id = Guid.NewGuid();
        _subscribers[id] = channel;

        channel.Writer.TryWrite(Map(_session.Snapshot));
        return (id, channel.Reader);
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.Complete();
        }
    }

    public void Dispose()
    {
        _session.StateChanged -= OnStateChanged;
        foreach (var channel in _subscribers.Values)
        {
            channel.Writer.Complete();
        }
    }

    private void OnStateChanged(object? sender, VpnSessionSnapshot snapshot)
    {
        var status = Map(snapshot);
        foreach (var channel in _subscribers.Values)
        {
            channel.Writer.TryWrite(status);
        }
    }

    private static ConnectionStatus Map(VpnSessionSnapshot snapshot) => new()
    {
        State = (Contracts.Grpc.VpnConnectionState)snapshot.State,
        ActiveServerId = snapshot.ActiveServerId ?? string.Empty,
        LastError = snapshot.LastError ?? string.Empty
    };
}
