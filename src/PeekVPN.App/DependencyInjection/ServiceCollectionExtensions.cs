using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PeekVPN.App.Services;
using PeekVPN.App.ViewModels;
using PeekVPN.Core.DependencyInjection;
using PeekVPN.Infrastructure.DependencyInjection;

namespace PeekVPN.App.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPeekVpnApp(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddCore();
        services.AddInfrastructure();

        var serviceUrl = Environment.GetEnvironmentVariable("PEEKVPN_SERVICE_URL")
            ?? "http://localhost:50052";
        services.AddGrpcClient(serviceUrl);

        services.AddSingleton<ServerRegistry>();
        services.AddSingleton<IServerLookup>(provider => provider.GetRequiredService<ServerRegistry>());
        services.AddSingleton<IVpnConnectionRequestFactory, VpnConnectionRequestFactory>();

        services.AddSingleton<IConnectionStatsProvider, MockConnectionStatsProvider>();
        services.AddTransient<ServerBrowserViewModel>();
        services.AddTransient<ConnectionPanelViewModel>();
        services.AddTransient<StatsSummaryViewModel>();
        services.AddTransient<MapViewModel>();
        services.AddTransient<FeatureCardsViewModel>();
        services.AddTransient<WorkspaceViewModel>();
        services.AddTransient<StatisticsPageViewModel>();
        services.AddTransient<ProfilePageViewModel>();
        services.AddTransient<SettingsPageViewModel>();
        services.AddTransient<ShellViewModel>();
        return services;
    }
}
