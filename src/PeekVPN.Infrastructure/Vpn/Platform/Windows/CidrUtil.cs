using System.Net;
using System.Runtime.Versioning;

namespace PeekVPN.Infrastructure.Vpn.Platform;

internal static class CidrUtil
{
    public static bool TrySplit(string cidr, out IPAddress address, out int prefix)
    {
        address = IPAddress.None;
        prefix = 0;

        var parts = cidr.Split('/', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !IPAddress.TryParse(parts[0], out address!))
        {
            return false;
        }

        if (parts.Length == 1)
        {
            prefix = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
            return true;
        }

        return int.TryParse(parts[1], out prefix);
    }

    public static string ToIpv4Mask(int prefix)
    {
        prefix = Math.Clamp(prefix, 0, 32);
        var bits = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        var bytes = BitConverter.GetBytes(bits);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return new IPAddress(bytes).ToString();
    }

    public static IReadOnlyList<string> ExpandDefaultRoute(string destination)
    {
        return destination is "0.0.0.0/0" or "0/0"
            ? ["0.0.0.0/1", "128.0.0.0/1"]
            : [destination];
    }
}
