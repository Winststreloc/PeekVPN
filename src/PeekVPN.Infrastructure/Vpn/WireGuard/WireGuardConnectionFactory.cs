using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.WireGuard;

/// <summary>
/// Creates <see cref="WireGuardConnection"/> instances on Linux and Windows.
/// </summary>
public sealed class WireGuardConnectionFactory(ILogger<WireGuardConnection> logger) : IVpnConnectionFactory
{
    public bool CanHandle(string protocol)
    {
        return protocol.Equals("wireguard", StringComparison.OrdinalIgnoreCase);
    }

    public IVpnConnection Create(IPlatformNetworkServices platformServices)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new WireGuardConnection(platformServices, logger);
        }

        throw new PlatformNotSupportedException("WireGuard is only implemented on Linux and Windows in this milestone.");
    }
}
