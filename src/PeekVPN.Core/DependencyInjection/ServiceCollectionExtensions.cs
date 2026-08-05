using Microsoft.Extensions.DependencyInjection;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.Services;
using PeekVPN.Core.Services.Vpn;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<IVpnConnectionOrchestrator, VpnConnectionOrchestrator>();
        services.AddSingleton<IVpnSession, VpnSession>();
        services.AddSingleton<IServerCatalog, ServerCatalog>();
        return services;
    }
}
