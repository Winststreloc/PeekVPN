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
        var command = route.Replace || route.Destination == "0.0.0.0/0" ? "route replace" : "route add";
        var args = $"{command} {route.Destination}";

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

    public async Task PreserveHostRouteAsync(string hostIp, CancellationToken cancellationToken = default)
    {
        if (!System.Net.IPAddress.TryParse(hostIp, out _))
        {
            _logger.LogWarning("Endpoint host '{EndpointHost}' is not an IP address; cannot add a host route.", hostIp);
            return;
        }

        var (exitCode, output, error) = await ShellHelper.RunAsync(
            "ip",
            $"route get {hostIp}",
            cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0)
        {
            _logger.LogWarning("Could not determine route to endpoint {EndpointIp}: {Error}", hostIp, error);
            return;
        }

        var parts = output.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        string? gateway = null;
        string? iface = null;

        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] == "via")
            {
                gateway = parts[i + 1];
            }
            else if (parts[i] == "dev")
            {
                iface = parts[i + 1];
            }
        }

        if (iface is null)
        {
            _logger.LogWarning("Could not determine physical interface for endpoint {EndpointIp}; route output: {Output}", hostIp, output);
            return;
        }

        _logger.LogInformation(
            "Adding host route to endpoint {EndpointIp} via {Gateway} dev {Interface} to avoid routing loop.",
            hostIp,
            gateway ?? "(direct)",
            iface);

        try
        {
            await AddRouteAsync(new Route(hostIp, gateway, iface), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to add endpoint route to {EndpointIp}.", hostIp);
        }
    }
}
