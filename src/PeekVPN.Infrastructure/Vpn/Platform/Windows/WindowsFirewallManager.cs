using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

[SupportedOSPlatform("windows")]
internal sealed class WindowsFirewallManager(ILogger<WindowsFirewallManager> logger) : IFirewallManager
{
    private const int ActionBlock = 0;
    private const int ActionAllow = 1;
    private const int DirectionOut = 2;
    private const string EndpointRule = "PeekVPN Allow Endpoint";
    private const string TunnelRule = "PeekVPN Allow Tunnel";

    private object? _policy;
    private readonly Dictionary<int, int> _previousOutbound = [];

    public Task EnableKillSwitchAsync(KillSwitchRules rules, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Enabling Windows kill switch.");
        DisableKillSwitchCore();

        var policy = GetPolicy();
        var profiles = (int)((dynamic)policy).CurrentProfileTypes;
        foreach (var profile in EnumerateProfiles(profiles))
        {
            _previousOutbound[profile] = (int)((dynamic)policy).DefaultOutboundAction[profile];
            ((dynamic)policy).DefaultOutboundAction[profile] = ActionBlock;
        }

        foreach (var endpoint in rules.AllowedEndpoints)
        {
            AddAllowRule(policy, EndpointRule, endpoint, interfaceName: null);
        }

        foreach (var iface in rules.AllowedInterfaces)
        {
            AddAllowRule(policy, TunnelRule, remoteAddress: null, iface);
        }

        return Task.CompletedTask;
    }

    public Task DisableKillSwitchAsync(CancellationToken cancellationToken = default)
    {
        DisableKillSwitchCore();
        return Task.CompletedTask;
    }

    private void DisableKillSwitchCore()
    {
        try
        {
            var policy = GetPolicy();
            var rules = ((dynamic)policy).Rules;
            TryRemoveRule(rules, EndpointRule);
            TryRemoveRule(rules, TunnelRule);

            foreach (var (profile, action) in _previousOutbound)
            {
                ((dynamic)policy).DefaultOutboundAction[profile] = action;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fully disable the Windows kill switch.");
        }

        _previousOutbound.Clear();
    }

    private object GetPolicy()
    {
        if (_policy is not null)
        {
            return _policy;
        }

        var type = Type.GetTypeFromProgID("HNetCfg.FwPolicy2")
            ?? throw new InvalidOperationException("Windows Firewall COM API is unavailable.");
        _policy = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Failed to create HNetCfg.FwPolicy2.");
        return _policy;
    }

    private static void AddAllowRule(object policy, string name, string? remoteAddress, string? interfaceName)
    {
        var ruleType = Type.GetTypeFromProgID("HNetCfg.FwRule")
            ?? throw new InvalidOperationException("Windows Firewall rule COM API is unavailable.");
        dynamic rule = Activator.CreateInstance(ruleType)
            ?? throw new InvalidOperationException("Failed to create HNetCfg.FwRule.");

        rule.Name = name;
        rule.Enabled = true;
        rule.Action = ActionAllow;
        rule.Direction = DirectionOut;
        rule.Protocol = 256; // ANY
        if (!string.IsNullOrWhiteSpace(remoteAddress))
        {
            rule.RemoteAddresses = remoteAddress;
        }

        if (!string.IsNullOrWhiteSpace(interfaceName))
        {
            rule.Interfaces = new object[] { interfaceName };
        }

        ((dynamic)policy).Rules.Add(rule);
    }

    private static void TryRemoveRule(dynamic rules, string name)
    {
        try
        {
            rules.Remove(name);
        }
        catch
        {
            // Rule may not exist.
        }
    }

    private static IEnumerable<int> EnumerateProfiles(int mask)
    {
        foreach (var bit in new[] { 1, 2, 4 })
        {
            if ((mask & bit) != 0)
            {
                yield return bit;
            }
        }
    }
}
