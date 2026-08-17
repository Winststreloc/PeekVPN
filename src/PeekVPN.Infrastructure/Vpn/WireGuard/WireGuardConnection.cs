using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.WireGuard;

/// <summary>
/// WireGuard protocol implementation. Platform-specific tunnel work is delegated to
/// <see cref="IWireGuardTunnel"/> (kernel module on Linux, Wintun + userspace on Windows).
/// </summary>
public sealed class WireGuardConnection : IVpnConnection
{
    private readonly IRoutingManager _routing;
    private readonly IDnsManager _dns;
    private readonly IFirewallManager _firewall;
    private readonly IWireGuardTunnel _tunnel;
    private readonly ILogger<WireGuardConnection> _logger;

    private bool _disposed;

    public WireGuardConnection(IPlatformNetworkServices platformServices, ILogger<WireGuardConnection> logger)
    {
        _routing = platformServices.RoutingManager;
        _dns = platformServices.DnsManager;
        _firewall = platformServices.FirewallManager;
        _tunnel = platformServices.CreateWireGuardTunnel();
        _logger = logger;
    }

    public string Protocol => "wireguard";

    public async Task EstablishAsync(VpnConnectionRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var configText = System.Text.Encoding.UTF8.GetString(request.Credentials);
        var parsed = WireGuardConfigParser.Parse(configText);

        _logger.LogInformation(
            "Establishing WireGuard connection to {Endpoint} with {AllowedIpCount} allowed IP ranges.",
            parsed.Endpoint ?? "(none)",
            parsed.AllowedIps.Count);

        await _tunnel.DeleteAsync(cancellationToken).ConfigureAwait(false);
        await _tunnel.CreateAsync(cancellationToken).ConfigureAwait(false);
        await _tunnel.ApplyConfigurationAsync(configText, cancellationToken).ConfigureAwait(false);
        await _tunnel.ConfigureAddressesAsync(parsed.Addresses, cancellationToken).ConfigureAwait(false);
        await _tunnel.BringUpAsync(cancellationToken).ConfigureAwait(false);
        await AddRoutesAsync(parsed, cancellationToken).ConfigureAwait(false);

        if (parsed.DnsServers.Count > 0)
        {
            _logger.LogInformation("Applying DNS servers: {DnsServers}", string.Join(", ", parsed.DnsServers));
            await _dns.SetDnsServersAsync(parsed.DnsServers, cancellationToken).ConfigureAwait(false);
        }

        if (request.Options.KillSwitch)
        {
            var allowedEndpoints = new List<string>();
            if (parsed.Endpoint is not null && WireGuardEndpoint.TryParse(parsed.Endpoint, out var host, out _))
            {
                allowedEndpoints.Add(host);
            }

            _logger.LogInformation("Enabling kill switch; allowed endpoints: {Endpoints}", string.Join(", ", allowedEndpoints));
            await _firewall.EnableKillSwitchAsync(
                new KillSwitchRules([_tunnel.InterfaceName], allowedEndpoints),
                cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation("WireGuard connection established on {InterfaceName}.", _tunnel.InterfaceName);
    }

    public async Task TeardownAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Tearing down WireGuard connection on {InterfaceName}.", _tunnel.InterfaceName);
        await _firewall.DisableKillSwitchAsync(cancellationToken).ConfigureAwait(false);
        await _dns.RestoreDnsAsync(cancellationToken).ConfigureAwait(false);
        await _tunnel.DeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await TeardownAsync(CancellationToken.None).ConfigureAwait(false);
        await _tunnel.DisposeAsync().ConfigureAwait(false);
    }

    private async Task AddRoutesAsync(WireGuardParsedConfig config, CancellationToken cancellationToken)
    {
        if (config.Endpoint is not null &&
            WireGuardEndpoint.TryParse(config.Endpoint, out var endpointHost, out _) &&
            System.Net.IPAddress.TryParse(endpointHost, out _))
        {
            await _routing.PreserveHostRouteAsync(endpointHost, cancellationToken).ConfigureAwait(false);
        }
        else if (config.Endpoint is not null)
        {
            _logger.LogWarning(
                "Endpoint host '{Endpoint}' is not an IP address; cannot add a host route.",
                config.Endpoint);
        }

        foreach (var allowedIp in config.AllowedIps)
        {
            _logger.LogDebug("Adding route {Destination} via {InterfaceName}.", allowedIp, _tunnel.InterfaceName);
            await _routing.AddRouteAsync(
                new Route(allowedIp, Gateway: null, _tunnel.InterfaceName, Replace: allowedIp == "0.0.0.0/0"),
                cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
