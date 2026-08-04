namespace PeekVPN.Contracts;

public interface IVpnApiClient
{
    Task<IReadOnlyList<VpnServerDto>> GetCitiesAsync(CancellationToken cancellationToken = default);

    Task<ConnectResponse> ConnectAsync(ConnectRequest request, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);
}