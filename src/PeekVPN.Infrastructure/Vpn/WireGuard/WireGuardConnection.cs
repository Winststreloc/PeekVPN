using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;
using PeekVPN.Infrastructure.Vpn.Platform;

namespace PeekVPN.Infrastructure.Vpn.WireGuard;

/// <summary>
/// WireGuard protocol implementation for Linux using the kernel module and <c>wg</c> / <c>ip</c> tools.
/// </summary>
public sealed class WireGuardConnection : IVpnConnection
{
    private const string InterfaceName = "peekvpn0";

    private readonly IPlatformNetworkServices _platform;
    private readonly LinuxIpRoutingManager _routing;
    private readonly LinuxDnsManager _dns;
    private readonly LinuxFirewallManager _firewall;
    private readonly ILogger<WireGuardConnection> _logger;

    private WireGuardParsedConfig? _config;
    private bool _disposed;

    public WireGuardConnection(IPlatformNetworkServices platformServices, ILogger<WireGuardConnection> logger)
    {
        _platform = platformServices;
        _routing = (LinuxIpRoutingManager)platformServices.RoutingManager;
        _dns = (LinuxDnsManager)platformServices.DnsManager;
        _firewall = (LinuxFirewallManager)platformServices.FirewallManager;
        _logger = logger;
    }

    public string Protocol => "wireguard";

    public async Task EstablishAsync(VpnConnectionRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var configText = System.Text.Encoding.UTF8.GetString(request.Credentials);
        var parsed = WireGuardConfigParser.Parse(configText);
        _config = parsed;

        _logger.LogInformation(
            "Establishing WireGuard connection to {Endpoint} with {AllowedIpCount} allowed IP ranges.",
            parsed.Endpoint ?? "(none)",
            parsed.AllowedIps.Count);

        await EnsureInterfaceCleanAsync(cancellationToken).ConfigureAwait(false);
        await CreateInterfaceAsync(cancellationToken).ConfigureAwait(false);
        await ApplyConfigurationAsync(configText, cancellationToken).ConfigureAwait(false);
        await ConfigureAddressesAsync(parsed, cancellationToken).ConfigureAwait(false);
        await BringUpAsync(cancellationToken).ConfigureAwait(false);
        await AddRoutesAsync(parsed, cancellationToken).ConfigureAwait(false);

        if (parsed.DnsServers.Count > 0)
        {
            _logger.LogInformation("Applying DNS servers: {DnsServers}", string.Join(", ", parsed.DnsServers));
            await _dns.SetDnsServersAsync(parsed.DnsServers, cancellationToken).ConfigureAwait(false);
        }

        if (request.Options.KillSwitch)
        {
            var allowedEndpoints = parsed.Endpoint is not null
                ? new[] { parsed.Endpoint.Split(':')[0] }
                : Array.Empty<string>();

            _logger.LogInformation("Enabling kill switch; allowed endpoints: {Endpoints}", string.Join(", ", allowedEndpoints));
            await _firewall.EnableKillSwitchAsync(
                new KillSwitchRules([InterfaceName], allowedEndpoints),
                cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation("WireGuard connection established on {InterfaceName}.", InterfaceName);
    }

    public async Task TeardownAsync(CancellationToken cancellationToken = default)
    {
        // Teardown is safe to call multiple times and is invoked both explicitly and from DisposeAsync.
        _logger.LogInformation("Tearing down WireGuard connection on {InterfaceName}.", InterfaceName);
        await _firewall.DisableKillSwitchAsync(cancellationToken).ConfigureAwait(false);
        await _dns.RestoreDnsAsync(cancellationToken).ConfigureAwait(false);
        await DeleteInterfaceAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await TeardownAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task EnsureInterfaceCleanAsync(CancellationToken cancellationToken)
    {
        await DeleteInterfaceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task CreateInterfaceAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Creating WireGuard interface {InterfaceName}.", InterfaceName);
        var (exitCode, _, error) = await ShellHelper.RunAsync(
            "ip",
            $"link add dev {InterfaceName} type wireguard",
            cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to create WireGuard interface: {error}");
        }
    }

    private async Task ApplyConfigurationAsync(string configText, CancellationToken cancellationToken)
    {
        // wg setconf only understands the keys that belong to the kernel interface / peer.
        // wg-quick keys like Address and DNS must be handled separately.
        var wgConfig = string.Join(
            Environment.NewLine,
            configText.Split(['\r', '\n'])
                .Where(line =>
                {
                    var key = line.Split('=')[0].Trim();
                    return !key.Equals("Address", StringComparison.OrdinalIgnoreCase)
                        && !key.Equals("DNS", StringComparison.OrdinalIgnoreCase);
                }));

        var tempFile = Path.Combine(Path.GetTempPath(), $"peekvpn-{InterfaceName}.conf");
        await File.WriteAllTextAsync(tempFile, wgConfig, cancellationToken).ConfigureAwait(false);

        try
        {
            _logger.LogDebug("Applying WireGuard kernel configuration from {TempFile}.", tempFile);
            var (exitCode, _, error) = await ShellHelper.RunAsync(
                "wg",
                $"setconf {InterfaceName} {tempFile}",
                cancellationToken)
                .ConfigureAwait(false);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Failed to apply WireGuard configuration: {error}");
            }
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
                // Best effort.
            }
        }
    }

    private async Task ConfigureAddressesAsync(WireGuardParsedConfig config, CancellationToken cancellationToken)
    {
        foreach (var address in config.Addresses)
        {
            _logger.LogDebug("Adding address {Address} to {InterfaceName}.", address, InterfaceName);
            var (exitCode, _, error) = await ShellHelper.RunAsync(
                "ip",
                $"address add {address} dev {InterfaceName}",
                cancellationToken)
                .ConfigureAwait(false);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Failed to set address on WireGuard interface: {error}");
            }
        }
    }

    private async Task BringUpAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Bringing up {InterfaceName}.", InterfaceName);
        var (exitCode, _, error) = await ShellHelper.RunAsync(
            "ip",
            $"link set {InterfaceName} up",
            cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Failed to bring up WireGuard interface: {error}");
        }
    }

    private async Task AddRoutesAsync(WireGuardParsedConfig config, CancellationToken cancellationToken)
    {
        // If the endpoint is on the public internet, we must keep the encrypted WireGuard
        // packets flowing through the physical interface. Without this, the default route we
        // add later would send the WireGuard packets back into the tunnel, creating a loop.
        if (config.Endpoint is not null)
        {
            var endpointHost = config.Endpoint.Split(':')[0];
            if (System.Net.IPAddress.TryParse(endpointHost, out _))
            {
                await AddEndpointRouteAsync(endpointHost, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogWarning("Endpoint host '{EndpointHost}' is not an IP address; cannot add a host route.", endpointHost);
            }
        }

        foreach (var allowedIp in config.AllowedIps)
        {
            // Replace the default route so traffic is sent through the tunnel. Using 'replace'
            // avoids the 'File exists' error when a default route already exists.
            var command = allowedIp == "0.0.0.0/0" ? "route replace" : "route add";
            _logger.LogDebug("Adding route {Destination} via {InterfaceName}.", allowedIp, InterfaceName);
            var (exitCode, _, error) = await ShellHelper.RunAsync(
                "ip",
                $"{command} {allowedIp} dev {InterfaceName}",
                cancellationToken)
                .ConfigureAwait(false);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Failed to add route '{allowedIp}': {error}");
            }
        }
    }

    private async Task AddEndpointRouteAsync(string endpointIp, CancellationToken cancellationToken)
    {
        // Find the current route to the endpoint so we can duplicate it with a higher priority.
        var (exitCode, output, error) = await ShellHelper.RunAsync(
            "ip",
            $"route get {endpointIp}",
            cancellationToken)
            .ConfigureAwait(false);

        if (exitCode != 0)
        {
            _logger.LogWarning("Could not determine route to endpoint {EndpointIp}: {Error}", endpointIp, error);
            return;
        }

        // Typical output: "104.171.128.186 via 192.168.1.1 dev eth0 src 192.168.1.2 uid 0"
        // Or: "104.171.128.186 dev eth0 src 192.168.1.2 uid 0" (directly attached)
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
            _logger.LogWarning("Could not determine physical interface for endpoint {EndpointIp}; route output: {Output}", endpointIp, output);
            return;
        }

        var routeArgs = $"route add {endpointIp} ";
        if (!string.IsNullOrWhiteSpace(gateway))
        {
            routeArgs += $"via {gateway} ";
        }
        routeArgs += $"dev {iface}";

        _logger.LogInformation(
            "Adding host route to endpoint {EndpointIp} via {Gateway} dev {Interface} to avoid routing loop.",
            endpointIp,
            gateway ?? "(direct)",
            iface);

        var (addExitCode, _, addError) = await ShellHelper.RunAsync("ip", routeArgs, cancellationToken).ConfigureAwait(false);
        if (addExitCode != 0)
        {
            _logger.LogWarning("Failed to add endpoint route: {Error}", addError);
        }
    }

    private async Task DeleteInterfaceAsync(CancellationToken cancellationToken)
    {
        await ShellHelper.RunAsync(
            "ip",
            $"link delete {InterfaceName}",
            cancellationToken)
            .ConfigureAwait(false);
    }
}
