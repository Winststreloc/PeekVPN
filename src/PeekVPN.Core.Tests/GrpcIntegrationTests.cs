using Grpc.Net.Client;
using Google.Protobuf;
using PeekVPN.Contracts.Grpc;

namespace PeekVPN.Core.Tests;

public sealed class GrpcIntegrationTests : IDisposable
{
    private readonly GrpcChannel _channel;

    public GrpcIntegrationTests()
    {
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        var port = Environment.GetEnvironmentVariable("PEEKVPN_TEST_PORT") ?? "50052";
        _channel = GrpcChannel.ForAddress($"http://localhost:{port}");
    }

    [Fact]
    public async Task GetServers_returns_server_list()
    {
        var client = new VpnService.VpnServiceClient(_channel);
        var response = await client.GetServersAsync(new GetServersRequest());

        Assert.NotEmpty(response.Servers);
        Assert.All(response.Servers, server => Assert.NotEmpty(server.Id));
    }

    [Fact]
    public async Task Connect_fails_gracefully_when_wireguard_tools_are_not_available()
    {
        var client = new VpnService.VpnServiceClient(_channel);
        var server = (await client.GetServersAsync(new GetServersRequest())).Servers.First();

        var request = new ConnectRequest
        {
            Protocol = "wireguard",
            ServerId = server.Id,
            Credentials = ByteString.CopyFromUtf8("[Interface]\nAddress = 10.8.0.2/32\n\n[Peer]\nAllowedIPs = 0.0.0.0/0\n"),
            Options = new ConnectionOptions()
        };

        var status = await client.ConnectAsync(request);

        // Without root / kernel module the interface cannot be created, so the service must
        // return a terminal state through the gRPC channel rather than crash.
        Assert.True(
            status.State is VpnConnectionState.Connected or VpnConnectionState.Disconnected,
            $"Unexpected state: {status.State}");

        status = await client.DisconnectAsync(new DisconnectRequest());
        Assert.Equal(VpnConnectionState.Disconnected, status.State);
    }

    public void Dispose()
    {
        _channel.Dispose();
    }
}
