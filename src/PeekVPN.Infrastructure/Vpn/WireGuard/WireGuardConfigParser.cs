using System.Globalization;

namespace PeekVPN.Infrastructure.Vpn.WireGuard;

/// <summary>
/// Minimal parser for the INI-style WireGuard configuration file.
/// </summary>
internal static class WireGuardConfigParser
{
    public static WireGuardParsedConfig Parse(string rawConfig)
    {
        var interfaceSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var peerSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var current = interfaceSection;

        foreach (var rawLine in rawConfig.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#') || line.StartsWith(';'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var section = line[1..^1].Trim();
                current = section.Equals("Peer", StringComparison.OrdinalIgnoreCase)
                    ? peerSection
                    : interfaceSection;
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            current[key] = value;
        }

        return new WireGuardParsedConfig(
            GetAddresses(interfaceSection.GetValueOrDefault("Address")),
            GetList(interfaceSection.GetValueOrDefault("DNS")),
            GetList(peerSection.GetValueOrDefault("AllowedIPs")),
            peerSection.GetValueOrDefault("Endpoint"),
            int.TryParse(peerSection.GetValueOrDefault("PersistentKeepalive"), CultureInfo.InvariantCulture, out var pk) ? pk : null,
            DecodeKey(interfaceSection.GetValueOrDefault("PrivateKey")),
            DecodeKey(peerSection.GetValueOrDefault("PublicKey")),
            DecodeKey(peerSection.GetValueOrDefault("PresharedKey")));
    }

    private static byte[]? DecodeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(value.Trim());
            return bytes.Length == 32 ? bytes : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static IReadOnlyList<string> GetAddresses(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .ToArray();
    }

    private static IReadOnlyList<string> GetList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .ToArray();
    }
}
