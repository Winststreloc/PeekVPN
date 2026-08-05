using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

/// <summary>
/// Linux routing manager that uses the <c>ip</c> utility.
/// </summary>
public sealed class LinuxIpRoutingManager : IRoutingManager
{
    private readonly ILogger<LinuxIpRoutingManager> _logger;

    public LinuxIpRoutingManager(ILogger<LinuxIpRoutingManager> logger)
    {
        _logger = logger;
    }

    public async Task AddRouteAsync(Route route, CancellationToken cancellationToken = default)
    {
        var args = $"route add {route.Destination}";

        if (!string.IsNullOrWhiteSpace(route.Gateway))
        {
            args += $" via {route.Gateway}";
        }

        if (!string.IsNullOrWhiteSpace(route.InterfaceName))
        {
            args += $" dev {route.InterfaceName}";
        }

        if (route.Metric.HasValue)
        {
            args += $" metric {route.Metric.Value}";
        }

        _logger.LogDebug("Executing: ip {Arguments}", args);
        var (exitCode, _, error) = await ShellHelper.RunAsync("ip", args, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to add route '{route.Destination}': {error}");
        }
    }

    public async Task RemoveRouteAsync(Route route, CancellationToken cancellationToken = default)
    {
        var args = $"route del {route.Destination}";

        if (!string.IsNullOrWhiteSpace(route.Gateway))
        {
            args += $" via {route.Gateway}";
        }

        if (!string.IsNullOrWhiteSpace(route.InterfaceName))
        {
            args += $" dev {route.InterfaceName}";
        }

        _logger.LogDebug("Executing: ip {Arguments}", args);
        var (exitCode, _, error) = await ShellHelper.RunAsync("ip", args, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to remove route '{route.Destination}': {error}");
        }
    }

    public async Task FlushInterfaceRoutesAsync(string interfaceName, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing: ip route flush dev {InterfaceName}", interfaceName);
        var (exitCode, _, error) = await ShellHelper.RunAsync(
            "ip",
            $"route flush dev {interfaceName}",
            cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to flush routes for '{interfaceName}': {error}");
        }
    }
}
