using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

/// <summary>
/// Creates the right <see cref="IPlatformNetworkServices"/> for the current OS.
/// </summary>
public static class PlatformNetworkServicesFactory
{
    public static IPlatformNetworkServices Create(ILoggerFactory loggerFactory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return new LinuxPlatformNetworkServices(loggerFactory);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows platform services are not implemented yet.");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new PlatformNotSupportedException("macOS platform services are not implemented yet.");
        }

        throw new PlatformNotSupportedException($"Unsupported OS: {RuntimeInformation.OSDescription}");
    }
}
