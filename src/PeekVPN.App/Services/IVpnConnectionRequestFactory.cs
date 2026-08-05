using PeekVPN.Core.Vpn;

namespace PeekVPN.App.Services;

/// <summary>
/// Builds a <see cref="VpnConnectionRequest"/> for the selected server and protocol.
/// </summary>
public interface IVpnConnectionRequestFactory
{
    Task<VpnConnectionRequest> CreateAsync(
        string serverId,
        string protocol = "wireguard",
        CancellationToken cancellationToken = default);
}
