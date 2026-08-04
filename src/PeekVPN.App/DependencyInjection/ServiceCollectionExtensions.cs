using Microsoft.Extensions.DependencyInjection;
using PeekVPN.App.Services;
using PeekVPN.App.ViewModels;
using PeekVPN.Core.DependencyInjection;
using PeekVPN.Infrastructure.DependencyInjection;

namespace PeekVPN.App.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPeekVpnApp(this IServiceCollection services)
    {
        services.AddCore();
        services.AddInfrastructure();
        services.AddSingleton<ServerRegistry>();
        services.AddSingleton<IServerLookup>(provider => provider.GetRequiredService<ServerRegistry>());

        services.AddSingleton<IConnectionStatsProvider, MockConnectionStatsProvider>();
        services.AddTransient<ServerBrowserViewModel>();
        services.AddTransient<ConnectionPanelViewModel>();
        services.AddTransient<StatsSummaryViewModel>();
        services.AddTransient<MapViewModel>();
        services.AddTransient<FeatureCardsViewModel>();
        services.AddTransient<WorkspaceViewModel>();
        services.AddTransient<ShellViewModel>();
        return services;
    }
}
