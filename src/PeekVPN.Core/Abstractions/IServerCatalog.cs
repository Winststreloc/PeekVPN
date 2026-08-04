using PeekVPN.Contracts;

namespace PeekVPN.Core.Abstractions;

public interface IServerCatalog
{
    Task<IReadOnlyList<VpnServerDto>> GetServersAsync(CancellationToken cancellationToken = default);
}
