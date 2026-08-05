namespace PeekVPN.Core.Vpn;

/// <summary>
/// A virtual network adapter created by the platform layer.
/// </summary>
public interface ITunnelAdapter : IAsyncDisposable
{
    string InterfaceName { get; }

    Task ConfigureAsync(AdapterConfiguration configuration, CancellationToken cancellationToken = default);
}
