using Avalonia;
using PeekVPN.Core.Logging;
using Serilog;

namespace PeekVPN.App;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Log.Logger = PeekVpnLogging.CreateLogger();
        var logger = Log.ForContext<Program>();
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            logger.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled application exception. IsTerminating: {IsTerminating}", eventArgs.IsTerminating);
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            logger.Error(eventArgs.Exception, "Unobserved task exception.");
            eventArgs.SetObserved();
        };

        try
        {
            logger.Information("Starting PeekVPN desktop application.");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "PeekVPN desktop application terminated unexpectedly.");
            throw;
        }
        finally
        {
            logger.Information("PeekVPN desktop application is shutting down.");
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}
