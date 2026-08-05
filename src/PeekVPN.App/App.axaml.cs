using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using PeekVPN.App.DependencyInjection;
using PeekVPN.App.ViewModels;
using PeekVPN.App.Views;
using PeekVPN.Core.Logging;
using Microsoft.Extensions.Logging;
using Serilog;

namespace PeekVPN.App;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var collection = new ServiceCollection();
        collection.AddPeekVpnLogging(Log.Logger);
        collection.AddPeekVpnApp();
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<ILogger<App>>().LogInformation("Desktop application services initialized.");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = _services.GetRequiredService<ShellViewModel>(),
            };

            desktop.Exit += (_, _) =>
            {
                _services.GetRequiredService<ILogger<App>>().LogInformation("Desktop application exit requested.");
                _services.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
