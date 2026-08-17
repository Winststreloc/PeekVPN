using System.Net;

namespace PeekVPN.Infrastructure.Vpn.WireGuard;

internal static class WireGuardEndpoint
{
    public static bool TryParse(string endpoint, out string host, out int port)
    {
        host = string.Empty;
        port = 0;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return false;
        }

        endpoint = endpoint.Trim();

        if (endpoint.StartsWith('['))
        {
            var close = endpoint.IndexOf(']');
            if (close < 0 || close + 2 >= endpoint.Length || endpoint[close + 1] != ':')
            {
                return false;
            }

            host = endpoint[1..close];
            return int.TryParse(endpoint[(close + 2)..], out port) && port is > 0 and <= 65535;
        }

        var separator = endpoint.LastIndexOf(':');
        if (separator <= 0 || separator == endpoint.Length - 1)
        {
            return false;
        }

        host = endpoint[..separator];
        if (!int.TryParse(endpoint[(separator + 1)..], out port) || port is <= 0 or > 65535)
        {
            return false;
        }

        return host.Length > 0 && (IPAddress.TryParse(host, out _) || Uri.CheckHostName(host) != UriHostNameType.Unknown);
    }
}
