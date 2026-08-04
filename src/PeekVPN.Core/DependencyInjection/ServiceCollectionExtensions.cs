using Microsoft.Extensions.DependencyInjection;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.Services;

namespace PeekVPN.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        services.AddSingleton<IVpnSession, VpnSession>();
        services.AddSingleton<IServerCatalog, ServerCatalog>();
        return services;
    }
}