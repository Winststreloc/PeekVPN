using PeekVPN.Contracts;
using PeekVPN.Core.Vpn;

namespace PeekVPN.App.Services;

/// <summary>
/// Fetches protocol credentials from the VPN API and packages them into a request for the background service.
/// </summary>
public sealed class VpnConnectionRequestFactory(IVpnApiClient apiClient) : IVpnConnectionRequestFactory
{
    public async Task<VpnConnectionRequest> CreateAsync(
        string serverId,
        string protocol = "wireguard",
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverId);
        ArgumentException.ThrowIfNullOrWhiteSpace(protocol);

        var config = await apiClient
            .GetCredentialsAsync(serverId, cancellationToken)
            .ConfigureAwait(false);

        if (config is null)
        {
            throw new InvalidOperationException($"Could not retrieve credentials for server '{serverId}'.");
        }

        var credentials = System.Text.Encoding.UTF8.GetBytes(config.RawConfig);
        return new VpnConnectionRequest(protocol, serverId, credentials, new ConnectionOptions());
    }
}
