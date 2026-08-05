using Grpc.Core;
using PeekVPN.Contracts.Grpc;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;
using PeekVPN.Core.Vpn;
using Microsoft.Extensions.Logging;

namespace PeekVPN.Service.Services;

/// <summary>
/// gRPC surface exposed by the background service. It delegates all VPN work to
/// <see cref="IVpnSession"/> and <see cref="IServerCatalog"/>.
/// </summary>
public sealed class VpnGrpcService(
    IVpnSession session,
    IServerCatalog catalog,
    ConnectionStatusNotifier notifier,
    ILogger<VpnGrpcService> logger)
    : VpnService.VpnServiceBase
{
    public override async Task<GetServersResponse> GetServers(
        GetServersRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("gRPC client requested the VPN server catalog.");
        var servers = await catalog.GetServersAsync(context.CancellationToken).ConfigureAwait(false);
        var response = new GetServersResponse();
        response.Servers.AddRange(servers.Select(Map));
        return response;
    }

    public override async Task<ConnectionStatus> Connect(
        ConnectRequest request,
        ServerCallContext context)
    {
        logger.LogInformation(
            "gRPC client requested connection to server {ServerId} using protocol {Protocol}.",
            request.ServerId,
            request.Protocol);

        var options = new global::PeekVPN.Core.Vpn.ConnectionOptions(
            request.Options?.KillSwitch ?? false,
            request.Options?.SplitTunnel ?? false,
            request.Options?.AllowedIps.ToArray() ?? ["0.0.0.0/0"]);

        var connectionRequest = new VpnConnectionRequest(
            request.Protocol,
            request.ServerId,
            request.Credentials.ToByteArray(),
            options);

        await session.ConnectAsync(connectionRequest, context.CancellationToken).ConfigureAwait(false);
        return Map(session.Snapshot);
    }

    public override async Task<ConnectionStatus> Disconnect(
        DisconnectRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("gRPC client requested VPN disconnect.");
        await session.DisconnectAsync(context.CancellationToken).ConfigureAwait(false);
        return Map(session.Snapshot);
    }

    public override Task<ConnectionStatus> Cancel(
        CancelRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("gRPC client requested cancellation of the pending VPN connection.");
        session.CancelConnect();
        return Task.FromResult(Map(session.Snapshot));
    }

    public override Task<ConnectionStatus> Pause(
        PauseRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("gRPC client requested VPN pause.");
        session.Pause();
        return Task.FromResult(Map(session.Snapshot));
    }

    public override Task<ConnectionStatus> Resume(
        ResumeRequest request,
        ServerCallContext context)
    {
        logger.LogInformation("gRPC client requested VPN resume.");
        session.Resume();
        return Task.FromResult(Map(session.Snapshot));
    }

    public override Task<ConnectionStatus> GetStatus(
        GetStatusRequest request,
        ServerCallContext context)
    {
        return Task.FromResult(Map(session.Snapshot));
    }

    public override async Task SubscribeStatus(
        SubscribeStatusRequest request,
        IServerStreamWriter<ConnectionStatus> responseStream,
        ServerCallContext context)
    {
        var (id, reader) = notifier.Subscribe();
        try
        {
            await foreach (var status in reader.ReadAllAsync(context.CancellationToken).ConfigureAwait(false))
            {
                await responseStream.WriteAsync(status).ConfigureAwait(false);
            }
        }
        finally
        {
            notifier.Unsubscribe(id);
        }
    }

    private static VpnServer Map(Contracts.VpnServerDto server) => new()
    {
        Id = server.Id,
        City = server.City,
        Country = server.Country,
        CountryCode = server.CountryCode,
        LatencyMs = server.LatencyMs,
        DisplayName = server.DisplayName,
        Latitude = server.Latitude,
        Longitude = server.Longitude
    };

    private static ConnectionStatus Map(VpnSessionSnapshot snapshot) => new()
    {
        State = (Contracts.Grpc.VpnConnectionState)snapshot.State,
        ActiveServerId = snapshot.ActiveServerId ?? string.Empty,
        LastError = snapshot.LastError ?? string.Empty
    };
}
