using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

/// <summary>
/// Linux firewall manager that uses <c>iptables</c> to implement the VPN kill switch.
/// </summary>
public sealed class LinuxFirewallManager : IFirewallManager
{
    private const string ChainName = "PEEKVPN_KILLSWITCH";

    private readonly ILogger<LinuxFirewallManager> _logger;

    public LinuxFirewallManager(ILogger<LinuxFirewallManager> logger)
    {
        _logger = logger;
    }

    public async Task EnableKillSwitchAsync(KillSwitchRules rules, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Enabling kill switch chain {ChainName}.", ChainName);
        await DisableKillSwitchAsync(cancellationToken).ConfigureAwait(false);

        await EnsureChainAsync(cancellationToken).ConfigureAwait(false);

        foreach (var endpoint in rules.AllowedEndpoints)
        {
            _logger.LogDebug("Allowing endpoint {Endpoint} through kill switch.", endpoint);
            await ShellHelper.RunAsync(
                "iptables",
                $"-A {ChainName} -d {endpoint} -j ACCEPT",
                cancellationToken)
                .ConfigureAwait(false);
        }

        foreach (var iface in rules.AllowedInterfaces)
        {
            _logger.LogDebug("Allowing outbound traffic on {Interface} through kill switch.", iface);
            await ShellHelper.RunAsync(
                "iptables",
                $"-A {ChainName} -o {iface} -j ACCEPT",
                cancellationToken)
                .ConfigureAwait(false);
        }

        await ShellHelper.RunAsync(
            "iptables",
            $"-A {ChainName} -j DROP",
            cancellationToken)
            .ConfigureAwait(false);

        await ShellHelper.RunAsync(
            "iptables",
            $"-I OUTPUT 1 -j {ChainName}",
            cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task DisableKillSwitchAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Disabling kill switch chain {ChainName}.", ChainName);
        await ShellHelper.RunAsync(
            "iptables",
            $"-D OUTPUT -j {ChainName}",
            cancellationToken)
            .ConfigureAwait(false);

        await ShellHelper.RunAsync(
            "iptables",
            $"-F {ChainName}",
            cancellationToken)
            .ConfigureAwait(false);

        await ShellHelper.RunAsync(
            "iptables",
            $"-X {ChainName}",
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task EnsureChainAsync(CancellationToken cancellationToken)
    {
        await ShellHelper.RunAsync(
            "iptables",
            $"-N {ChainName}",
            cancellationToken)
            .ConfigureAwait(false);
    }
}
