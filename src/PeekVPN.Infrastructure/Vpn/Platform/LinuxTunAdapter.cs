using System.Net.Sockets;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using PeekVPN.Core.Vpn;

namespace PeekVPN.Infrastructure.Vpn.Platform;

/// <summary>
/// Linux TUN adapter created via <c>/dev/net/tun</c>.
/// For now this creates the interface and applies addressing; packet I/O can be added later.
/// </summary>
public sealed class LinuxTunAdapter : ITunnelAdapter
{
    private const int TunSetIff = 0x400454ca;
    private const short IffTun = 0x0001;
    private const short IffNoPi = 0x1000;

    private readonly SafeFileHandle _handle;
    private bool _disposed;

    public LinuxTunAdapter(string name)
    {
        InterfaceName = name;

        _handle = File.OpenHandle("/dev/net/tun", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, FileOptions.None);

        var ifr = new IfReq(name);
        ifr.SetFlags(IffTun | IffNoPi);

        if (ioctl(_handle.DangerousGetHandle().ToInt32(), TunSetIff, ref ifr) != 0)
        {
            throw new InvalidOperationException($"Failed to create TUN interface '{name}': {Marshal.GetLastWin32Error()}");
        }
    }

    public string InterfaceName { get; }

    public async Task ConfigureAsync(AdapterConfiguration configuration, CancellationToken cancellationToken = default)
    {
        foreach (var address in configuration.Addresses)
        {
            var (exitCode, _, error) = await ShellHelper.RunAsync(
                "ip",
                $"address add {address} dev {InterfaceName}",
                cancellationToken)
                .ConfigureAwait(false);

            if (exitCode != 0)
            {
                throw new InvalidOperationException($"Failed to set address on '{InterfaceName}': {error}");
            }
        }

        var (upCode, _, upError) = await ShellHelper.RunAsync(
            "ip",
            $"link set {InterfaceName} up",
            cancellationToken)
            .ConfigureAwait(false);

        if (upCode != 0)
        {
            throw new InvalidOperationException($"Failed to bring up '{InterfaceName}': {upError}");
        }

        if (configuration.Mtu.HasValue)
        {
            await ShellHelper.RunAsync(
                "ip",
                $"link set {InterfaceName} mtu {configuration.Mtu.Value}",
                cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await ShellHelper.RunAsync("ip", $"link delete {InterfaceName}").ConfigureAwait(false);
        _handle.Dispose();
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, ulong request, ref IfReq ifr);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct IfReq
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string Name;

        public short Flags;
        private readonly short Padding;
        private readonly IntPtr OtherData;

        public IfReq(string name)
        {
            Name = name;
            Flags = 0;
            Padding = 0;
            OtherData = IntPtr.Zero;
        }

        public void SetFlags(short flags) => Flags = flags;
    }
}
