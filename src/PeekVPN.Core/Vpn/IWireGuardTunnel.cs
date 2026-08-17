namespace PeekVPN.Core.Vpn;

/// <summary>
/// Platform-specific WireGuard interface lifecycle (kernel module on Linux, Wintun + userspace on Windows).
/// </summary>
public interface IWireGuardTunnel : IAsyncDisposable
{
    string InterfaceName { get; }

    Task CreateAsync(CancellationToken cancellationToken = default);

    Task ApplyConfigurationAsync(string configText, CancellationToken cancellationToken = default);

    Task ConfigureAddressesAsync(IReadOnlyList<string> addresses, CancellationToken cancellationToken = default);

    Task BringUpAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);
}
