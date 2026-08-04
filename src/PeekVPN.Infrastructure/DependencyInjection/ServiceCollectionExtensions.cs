using Microsoft.Extensions.DependencyInjection;
using PeekVPN.Contracts;
using PeekVPN.Infrastructure.Http;

namespace PeekVPN.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IVpnApiClient, MockVpnHttpService>();
        return services;
    }
}