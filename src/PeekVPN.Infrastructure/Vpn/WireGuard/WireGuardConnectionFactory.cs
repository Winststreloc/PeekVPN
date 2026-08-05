using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;
using PeekVPN.Infrastructure.Vpn.Platform;

namespace PeekVPN.Infrastructure.Vpn.WireGuard;

/// <summary>
/// Creates <see cref="WireGuardConnection"/> instances on Linux.
/// </summary>
public sealed class WireGuardConnectionFactory : IVpnConnectionFactory
{
    private readonly ILogger<WireGuardConnection> _logger;

    public WireGuardConnectionFactory(ILogger<WireGuardConnection> logger)
    {
        _logger = logger;
    }

    public bool CanHandle(string protocol)
    {
        return protocol.Equals("wireguard", StringComparison.OrdinalIgnoreCase);
    }

    public IVpnConnection Create(IPlatformNetworkServices platformServices)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            throw new PlatformNotSupportedException("WireGuard is only implemented on Linux in this milestone.");
        }

        return new WireGuardConnection(platformServices, _logger);
    }
}
