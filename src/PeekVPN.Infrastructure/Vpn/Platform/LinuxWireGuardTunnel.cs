using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

/// <summary>
/// Kernel WireGuard interface on Linux (<c>ip link add type wireguard</c> + <c>wg setconf</c>).
/// </summary>
internal sealed class LinuxWireGuardTunnel(ILogger<LinuxWireGuardTunnel> logger) : IWireGuardTunnel
{
    public const string DefaultInterfaceName = "peekvpn0";

    private bool _disposed;

    public string InterfaceName => DefaultInterfaceName;

    public Task CreateAsync(CancellationToken cancellationToken = default)
        => RunOrThrowAsync(
            "ip",
            $"link add dev {InterfaceName} type wireguard",
            $"Failed to create WireGuard interface",
            cancellationToken);

    public async Task ApplyConfigurationAsync(string configText, CancellationToken cancellationToken = default)
    {
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
            logger.LogDebug("Applying WireGuard kernel configuration from {TempFile}.", tempFile);
            await RunOrThrowAsync(
                "wg",
                $"setconf {InterfaceName} {tempFile}",
                "Failed to apply WireGuard configuration",
                cancellationToken).ConfigureAwait(false);
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

    public async Task ConfigureAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default)
    {
        foreach (var address in addresses)
        {
            logger.LogDebug("Adding address {Address} to {InterfaceName}.", address, InterfaceName);
            await RunOrThrowAsync(
                "ip",
                $"address add {address} dev {InterfaceName}",
                "Failed to set address on WireGuard interface",
                cancellationToken).ConfigureAwait(false);
        }
    }

    public Task BringUpAsync(CancellationToken cancellationToken = default)
        => RunOrThrowAsync(
            "ip",
            $"link set {InterfaceName} up",
            $"Failed to bring up WireGuard interface",
            cancellationToken);

    public Task DeleteAsync(CancellationToken cancellationToken = default)
        => ShellHelper.RunAsync("ip", $"link delete {InterfaceName}", cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await DeleteAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private static async Task RunOrThrowAsync(
        string command,
        string arguments,
        string errorPrefix,
        CancellationToken cancellationToken)
    {
        var (exitCode, _, error) = await ShellHelper.RunAsync(command, arguments, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"{errorPrefix}: {error}");
        }
    }
}
