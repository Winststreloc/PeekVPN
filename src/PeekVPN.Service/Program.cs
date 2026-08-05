using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PeekVPN.Core.DependencyInjection;
using PeekVPN.Core.Logging;
using PeekVPN.Infrastructure.DependencyInjection;
using PeekVPN.Service.Services;
using Serilog;

namespace PeekVPN.Service;

public sealed class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = PeekVpnLogging.CreateLogger();
        var logger = Log.ForContext<Program>();
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            logger.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled service exception. IsTerminating: {IsTerminating}", eventArgs.IsTerminating);
            Log.CloseAndFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            logger.Error(eventArgs.Exception, "Unobserved service task exception.");
            eventArgs.SetObserved();
        };

        try
        {
            logger.Information("Starting PeekVPN background service.");
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddPeekVpnLogging(Log.Logger);
            builder.Services.AddCore();
            builder.Services.AddInfrastructure();
            builder.Services.AddGrpc();
            builder.Services.AddSingleton<ConnectionStatusNotifier>();

            var port = builder.Configuration.GetValue<int?>("PeekVPN:Port") ?? 50052;

            builder.WebHost.ConfigureKestrel(options =>
            {
                options.ListenAnyIP(port, listenOptions =>
                {
                    listenOptions.Protocols = HttpProtocols.Http2;
                });
            });

            var app = builder.Build();
            app.MapGrpcService<VpnGrpcService>();
            logger.Information("PeekVPN background service is listening on port {Port}.", port);
            app.Run();
        }
        catch (Exception ex)
        {
            logger.Fatal(ex, "PeekVPN background service terminated unexpectedly.");
            throw;
        }
        finally
        {
            logger.Information("PeekVPN background service is shutting down.");
            Log.CloseAndFlush();
        }
    }
}
