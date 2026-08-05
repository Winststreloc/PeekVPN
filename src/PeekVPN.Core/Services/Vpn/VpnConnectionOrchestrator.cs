using PeekVPN.Core.Vpn;
using Microsoft.Extensions.Logging;

namespace PeekVPN.Core.Services.Vpn;

/// <summary>
/// Selects the right protocol factory and runs the establish/teardown pipeline.
/// This is the only component the session state machine talks to for actual tunnel work.
/// </summary>
public sealed class VpnConnectionOrchestrator : IVpnConnectionOrchestrator, IDisposable
{
    private readonly IEnumerable<IVpnConnectionFactory> _factories;
    private readonly IPlatformNetworkServices _platformServices;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IVpnConnection? _currentConnection;
    private bool _disposed;

    public VpnConnectionOrchestrator(
        IEnumerable<IVpnConnectionFactory> factories,
        IPlatformNetworkServices platformServices,
        ILogger<VpnConnectionOrchestrator> logger)
    {
        _factories = factories;
        _platformServices = platformServices;
        _logger = logger;
    }

    private readonly ILogger<VpnConnectionOrchestrator> _logger;
    public async Task<ConnectionResult> ConnectAsync(
        VpnConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentConnection is not null)
            {
                _logger.LogWarning("Rejected VPN connection because another connection is active.");
                return ConnectionResult.Failed("A connection is already active.");
            }

            var factory = _factories.FirstOrDefault(f => f.CanHandle(request.Protocol));
            if (factory is null)
            {
                _logger.LogWarning("No VPN connection factory supports protocol {Protocol}.", request.Protocol);
                return ConnectionResult.Failed($"Unsupported protocol: {request.Protocol}");
            }

            var connection = factory.Create(_platformServices);
            _currentConnection = connection;
            _logger.LogInformation("Establishing {Protocol} VPN connection.", request.Protocol);

            try
            {
                await connection.EstablishAsync(request, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("{Protocol} VPN connection established.", request.Protocol);
                return ConnectionResult.Ok();
            }
            catch (Exception ex)
            {
                _currentConnection = null;
                try
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception disposeException)
                {
                    _logger.LogError(
                        disposeException,
                        "Cleanup failed after {Protocol} connection failure; original error will be preserved.",
                        request.Protocol);
                }

                _logger.LogError(ex, "Failed to establish {Protocol} VPN connection.", request.Protocol);
                return ConnectionResult.Failed(ex.Message);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = _currentConnection;
            _currentConnection = null;

            if (connection is null)
            {
                _logger.LogDebug("VPN disconnect requested without an active connection.");
                return;
            }

            try
            {
                _logger.LogInformation("Tearing down VPN connection.");
                await connection.TeardownAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("VPN connection torn down.");
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _gate.Dispose();

        var connection = _currentConnection;
        if (connection is not null)
        {
            _currentConnection = null;
            _ = connection.DisposeAsync().AsTask();
        }
    }
}
