using System.Net;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

[SupportedOSPlatform("windows")]
internal sealed class WindowsIpRoutingManager(ILogger<WindowsIpRoutingManager> logger) : IRoutingManager
{
    public async Task AddRouteAsync(Route route, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(route.InterfaceName))
        {
            throw new ArgumentException("Windows routes require an interface name.", nameof(route));
        }

        var index = await WaitForInterfaceIndexAsync(route.InterfaceName, cancellationToken).ConfigureAwait(false);
        foreach (var destination in CidrUtil.ExpandDefaultRoute(route.Destination))
        {
            if (!CidrUtil.TrySplit(destination, out var address, out var prefix) ||
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                logger.LogWarning("Skipping unsupported Windows route destination {Destination}.", destination);
                continue;
            }

            var mask = CidrUtil.ToIpv4Mask(prefix);
            var gateway = string.IsNullOrWhiteSpace(route.Gateway) ? "0.0.0.0" : route.Gateway;
            var metric = route.Metric is > 0 ? $" METRIC {route.Metric.Value}" : string.Empty;
            var args = $"add {address} mask {mask} {gateway} IF {index}{metric}";

            logger.LogDebug("Executing: route {Arguments}", args);
            var (exitCode, _, error) = await ShellHelper.RunAsync("route", args, cancellationToken).ConfigureAwait(false);
            if (exitCode != 0 && !error.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Failed to add route '{destination}': {error}");
            }
        }
    }

    public async Task RemoveRouteAsync(Route route, CancellationToken cancellationToken = default)
    {
        foreach (var destination in CidrUtil.ExpandDefaultRoute(route.Destination))
        {
            if (!CidrUtil.TrySplit(destination, out var address, out var prefix) ||
                address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            {
                continue;
            }

            var mask = CidrUtil.ToIpv4Mask(prefix);
            var args = $"delete {address} mask {mask}";
            if (!string.IsNullOrWhiteSpace(route.Gateway))
            {
                args += $" {route.Gateway}";
            }

            await ShellHelper.RunAsync("route", args, cancellationToken).ConfigureAwait(false);
        }
    }

    public Task FlushInterfaceRoutesAsync(string interfaceName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task PreserveHostRouteAsync(string hostIp, CancellationToken cancellationToken = default)
    {
        if (!IPAddress.TryParse(hostIp, out var address) ||
            address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            logger.LogWarning("Endpoint host '{EndpointHost}' is not an IPv4 address; cannot add a host route.", hostIp);
            return;
        }

        var dest = BitConverter.ToUInt32(address.GetAddressBytes());
        var status = IpHelperNative.GetBestRoute(dest, 0, out var row);
        if (status != IpHelperNative.NoError)
        {
            logger.LogWarning("GetBestRoute failed for {EndpointIp} with status {Status}.", hostIp, status);
            return;
        }

        var gatewayBytes = BitConverter.GetBytes(row.ForwardNextHop);
        var gateway = new IPAddress(gatewayBytes).ToString();
        logger.LogInformation(
            "Adding host route to endpoint {EndpointIp} via {Gateway} ifIndex {IfIndex} to avoid routing loop.",
            hostIp,
            gateway,
            row.ForwardIfIndex);

        var args = $"add {hostIp} mask 255.255.255.255 {gateway} IF {row.ForwardIfIndex}";
        var (exitCode, _, error) = await ShellHelper.RunAsync("route", args, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0 && !error.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("Failed to add endpoint route: {Error}", error);
        }
    }

    internal static async Task<uint> WaitForInterfaceIndexAsync(string alias, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 25; attempt++)
        {
            if (IpHelperNative.ConvertInterfaceAliasToLuid(alias, out var luid) == IpHelperNative.NoError &&
                IpHelperNative.ConvertInterfaceLuidToIndex(in luid, out var index) == IpHelperNative.NoError)
            {
                return index;
            }

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        throw new InvalidOperationException($"Network adapter '{alias}' did not become available.");
    }
}
