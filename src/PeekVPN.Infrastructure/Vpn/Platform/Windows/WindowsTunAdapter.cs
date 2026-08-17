using System.Net;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using PeekVPN.Core.Vpn;
using PeekVPN.Infrastructure.Vpn.WireGuard;

namespace PeekVPN.Infrastructure.Vpn.Platform;

[SupportedOSPlatform("windows")]
internal sealed class WindowsTunAdapter : ITunnelAdapter
{
    public const string TunnelType = "PeekVPN";

    private static readonly Guid AdapterGuid = new("8a1c3e5b-2d47-4f9a-91c6-7e0b4d2a6f11");

    private IntPtr _adapter;
    private bool _disposed;

    public WindowsTunAdapter(string name)
    {
        WintunNative.EnsureLoaded();
        InterfaceName = name;

        var existing = WintunNative.OpenAdapter(name);
        if (existing != IntPtr.Zero)
        {
            WintunNative.CloseAdapter(existing);
        }

        _adapter = WintunNative.CreateAdapter(name, TunnelType, AdapterGuid);
        if (_adapter == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"WintunCreateAdapter failed: {System.Runtime.InteropServices.Marshal.GetLastPInvokeErrorMessage()}");
        }
    }

    public string InterfaceName { get; }

    internal IntPtr AdapterHandle => _adapter;

    public async Task ConfigureAsync(AdapterConfiguration configuration, CancellationToken cancellationToken = default)
    {
        await WindowsIpRoutingManager.WaitForInterfaceIndexAsync(InterfaceName, cancellationToken).ConfigureAwait(false);

        foreach (var cidr in configuration.Addresses)
        {
            if (!CidrUtil.TrySplit(cidr, out var address, out var prefix))
            {
                throw new InvalidOperationException($"Invalid adapter address '{cidr}'.");
            }

            var mask = CidrUtil.ToIpv4Mask(prefix);
            var args = $"interface ipv4 set address name=\"{InterfaceName}\" source=static address={address} mask={mask}";
            var (exitCode, _, error) = await ShellHelper.RunAsync("netsh", args, cancellationToken).ConfigureAwait(false);
            if (exitCode != 0 && !error.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Failed to set address on '{InterfaceName}': {error}");
            }
        }

        if (configuration.Mtu is { } mtu)
        {
            await ShellHelper.RunAsync(
                "netsh",
                $"interface ipv4 set subinterface \"{InterfaceName}\" mtu={mtu} store=active",
                cancellationToken).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
    {
        DisposeAdapter();
        return ValueTask.CompletedTask;
    }

    internal void DisposeAdapter()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_adapter != IntPtr.Zero)
        {
            WintunNative.CloseAdapter(_adapter);
            _adapter = IntPtr.Zero;
        }
    }
}
