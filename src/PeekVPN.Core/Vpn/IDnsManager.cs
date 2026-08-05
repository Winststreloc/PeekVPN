namespace PeekVPN.Core.Vpn;

/// <summary>
/// Manages OS DNS settings for the VPN connection.
/// </summary>
public interface IDnsManager
{
    Task SetDnsServersAsync(IReadOnlyList<string> servers, CancellationToken cancellationToken = default);

    Task RestoreDnsAsync(CancellationToken cancellationToken = default);
}
