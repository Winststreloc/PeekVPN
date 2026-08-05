namespace PeekVPN.Core.Vpn;

/// <summary>
/// Parameters sent by the client to the service when asking for a VPN connection.
/// </summary>
public sealed record VpnConnectionRequest(
    string Protocol,
    string ServerId,
    byte[] Credentials,
    ConnectionOptions Options);
