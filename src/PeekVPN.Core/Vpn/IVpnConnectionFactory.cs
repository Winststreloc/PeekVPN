namespace PeekVPN.Core.Vpn;

/// <summary>
/// Creates protocol-specific <see cref="IVpnConnection"/> instances for a given platform.
/// </summary>
public interface IVpnConnectionFactory
{
    bool CanHandle(string protocol);

    IVpnConnection Create(IPlatformNetworkServices platformServices);
}
