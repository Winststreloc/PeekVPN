using Grpc.Net.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PeekVPN.Contracts;
using PeekVPN.Contracts.Grpc;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.Vpn;
using PeekVPN.Infrastructure.Grpc;
using PeekVPN.Infrastructure.Http;
using PeekVPN.Infrastructure.Vpn.Platform;
using PeekVPN.Infrastructure.Vpn.WireGuard;

namespace PeekVPN.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IVpnApiClient, MockVpnHttpService>();
        services.AddSingleton<IPlatformNetworkServices>(provider => PlatformNetworkServicesFactory.Create(
            provider.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IVpnConnectionFactory, WireGuardConnectionFactory>();
        return services;
    }

    /// <summary>
    /// Replaces the in-process VPN session and server catalog with gRPC clients that talk to the
    /// PeekVPN background service. Call this after <c>AddCore()</c>.
    /// </summary>
    public static IServiceCollection AddGrpcClient(this IServiceCollection services, string serviceAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceAddress);

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        services.AddSingleton(provider =>
        {
            var handler = new SocketsHttpHandler
            {
                EnableMultipleHttp2Connections = true,
                KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5)
            };

            var channel = GrpcChannel.ForAddress(serviceAddress, new GrpcChannelOptions
            {
                HttpHandler = handler
            });

            return new VpnService.VpnServiceClient(channel);
        });

        services.AddSingleton<IVpnSession, GrpcVpnSession>();
        services.AddSingleton<IServerCatalog, GrpcServerCatalog>();
        return services;
    }
}
