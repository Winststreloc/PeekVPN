using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PeekVPN.Infrastructure.Vpn.Platform;

[SupportedOSPlatform("windows")]
internal static partial class WintunNative
{
    public const string LibraryName = "wintun";
    public const uint MinRingCapacity = 0x20000;
    public const uint DefaultRingCapacity = 0x400000;
    public const int ErrorNoMoreItems = 259;
    public const int ErrorHandleEof = 38;
    public const int ErrorBufferOverflow = 111;

    static WintunNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(WintunNative).Assembly, Resolve);
    }

    [LibraryImport(LibraryName, EntryPoint = "WintunCreateAdapter", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr CreateAdapter(string name, string tunnelType, in Guid requestedGuid);

    [LibraryImport(LibraryName, EntryPoint = "WintunOpenAdapter", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial IntPtr OpenAdapter(string name);

    [LibraryImport(LibraryName, EntryPoint = "WintunCloseAdapter")]
    public static partial void CloseAdapter(IntPtr adapter);

    [LibraryImport(LibraryName, EntryPoint = "WintunGetAdapterLUID")]
    public static partial void GetAdapterLuid(IntPtr adapter, out ulong luid);

    [LibraryImport(LibraryName, EntryPoint = "WintunGetRunningDriverVersion")]
    public static partial uint GetRunningDriverVersion();

    [LibraryImport(LibraryName, EntryPoint = "WintunStartSession", SetLastError = true)]
    public static partial IntPtr StartSession(IntPtr adapter, uint capacity);

    [LibraryImport(LibraryName, EntryPoint = "WintunEndSession")]
    public static partial void EndSession(IntPtr session);

    [LibraryImport(LibraryName, EntryPoint = "WintunGetReadWaitEvent")]
    public static partial IntPtr GetReadWaitEvent(IntPtr session);

    [LibraryImport(LibraryName, EntryPoint = "WintunReceivePacket", SetLastError = true)]
    public static unsafe partial byte* ReceivePacket(IntPtr session, out uint packetSize);

    [LibraryImport(LibraryName, EntryPoint = "WintunReleaseReceivePacket")]
    public static unsafe partial void ReleaseReceivePacket(IntPtr session, byte* packet);

    [LibraryImport(LibraryName, EntryPoint = "WintunAllocateSendPacket", SetLastError = true)]
    public static unsafe partial byte* AllocateSendPacket(IntPtr session, uint packetSize);

    [LibraryImport(LibraryName, EntryPoint = "WintunSendPacket")]
    public static unsafe partial void SendPacket(IntPtr session, byte* packet);

    public static void EnsureLoaded()
    {
        foreach (var path in CandidatePaths())
        {
            if (File.Exists(path))
            {
                return;
            }
        }

        throw new FileNotFoundException(
            "wintun.dll was not found next to PeekVPN.Service. Copy the official signed AMD64 build from https://www.wintun.net/ into the service directory.");
    }

    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Equals(LibraryName, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        foreach (var path in CandidatePaths())
        {
            if (File.Exists(path) && NativeLibrary.TryLoad(path, out var handle))
            {
                return handle;
            }
        }

        return NativeLibrary.TryLoad(LibraryName, assembly, searchPath, out var fallback)
            ? fallback
            : IntPtr.Zero;
    }

    private static IEnumerable<string> CandidatePaths()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "wintun.dll");
        yield return Path.Combine(baseDir, "native", "win-x64", "wintun.dll");
        yield return Path.Combine(Path.GetDirectoryName(typeof(WintunNative).Assembly.Location) ?? baseDir, "wintun.dll");
    }
}
