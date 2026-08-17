using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

[SupportedOSPlatform("windows")]
internal sealed class WindowsDnsManager(string interfaceName, ILogger<WindowsDnsManager> logger) : IDnsManager
{
    public async Task SetDnsServersAsync(IReadOnlyList<string> servers, CancellationToken cancellationToken = default)
    {
        if (servers.Count == 0)
        {
            return;
        }

        logger.LogInformation("Setting DNS servers for {InterfaceName} to {Servers}.", interfaceName, string.Join(", ", servers));
        await WaitForAdapterAsync(cancellationToken).ConfigureAwait(false);

        var first = true;
        var index = 1;
        foreach (var server in servers)
        {
            var args = first
                ? $"interface ipv4 set dnsservers name=\"{interfaceName}\" source=static address={server} register=none validate=no"
                : $"interface ipv4 add dnsservers name=\"{interfaceName}\" address={server} index={index} validate=no";

            first = false;
            index++;

            var (exitCode, _, error) = await ShellHelper.RunAsync("netsh", args, cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Failed to set DNS on '{interfaceName}': {error}");
            }
        }
    }

    public async Task RestoreDnsAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Clearing DNS servers on {InterfaceName}.", interfaceName);
        await ShellHelper.RunAsync(
            "netsh",
            $"interface ipv4 set dnsservers name=\"{interfaceName}\" source=dhcp",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task WaitForAdapterAsync(CancellationToken cancellationToken)
        => await WindowsIpRoutingManager.WaitForInterfaceIndexAsync(interfaceName, cancellationToken).ConfigureAwait(false);
}
