using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

/// <summary>
/// Linux DNS manager. Prefers <c>resolvectl</c> for interface-specific DNS, and falls back to
/// overwriting <c>/etc/resolv.conf</c> when systemd-resolved is not available.
/// </summary>
public sealed class LinuxDnsManager : IDnsManager, IDisposable
{
    private readonly string _interfaceName;
    private readonly string _resolvBackupPath = "/etc/resolv.conf.peekvpn.bak";
    private readonly ILogger<LinuxDnsManager> _logger;

    public LinuxDnsManager(string interfaceName, ILogger<LinuxDnsManager> logger)
    {
        _interfaceName = interfaceName;
        _logger = logger;
    }

    public async Task SetDnsServersAsync(IReadOnlyList<string> servers, CancellationToken cancellationToken = default)
    {
        if (servers.Count == 0)
        {
            return;
        }

        var serverList = string.Join(" ", servers);
        _logger.LogInformation("Setting DNS servers for {InterfaceName} to {Servers}.", _interfaceName, serverList);

        // Try systemd-resolved first. This is the correct way on modern Linux distributions.
        var resolveArgs = $"dns {_interfaceName} {serverList}";
        _logger.LogDebug("Executing: resolvectl {Arguments}", resolveArgs);
        var (resolveExit, _, resolveError) = await ShellHelper.RunAsync("resolvectl", resolveArgs, cancellationToken).ConfigureAwait(false);
        if (resolveExit == 0)
        {
            _logger.LogInformation("DNS servers applied via resolvectl.");
            return;
        }

        _logger.LogWarning(
            "resolvectl failed (exit {ExitCode}): {Error}. Falling back to /etc/resolv.conf.",
            resolveExit,
            resolveError);

        if (File.Exists("/etc/resolv.conf") && !File.Exists(_resolvBackupPath))
        {
            _logger.LogDebug("Backing up /etc/resolv.conf to {BackupPath}.", _resolvBackupPath);
            File.Copy("/etc/resolv.conf", _resolvBackupPath, overwrite: true);
        }

        var lines = servers.Select(s => $"nameserver {s}").ToArray();
        await File.WriteAllLinesAsync("/etc/resolv.conf", lines, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Wrote DNS servers to /etc/resolv.conf.");
    }

    public Task RestoreDnsAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_resolvBackupPath))
        {
            _logger.LogInformation("Restoring /etc/resolv.conf from backup.");
            File.Copy(_resolvBackupPath, "/etc/resolv.conf", overwrite: true);
            File.Delete(_resolvBackupPath);
        }

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (File.Exists(_resolvBackupPath))
        {
            try
            {
                File.Delete(_resolvBackupPath);
            }
            catch
            {
                // Best effort.
            }
        }
    }
}
