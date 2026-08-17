using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PeekVPN.Infrastructure.Vpn.Platform;

[SupportedOSPlatform("windows")]
internal static partial class IpHelperNative
{
    public const int NoError = 0;
    public const int ErrorNotFound = 1168;

    [StructLayout(LayoutKind.Sequential)]
    public struct MibIpForwardRow
    {
        public uint ForwardDest;
        public uint ForwardMask;
        public uint ForwardPolicy;
        public uint ForwardNextHop;
        public uint ForwardIfIndex;
        public uint ForwardType;
        public uint ForwardProto;
        public uint ForwardAge;
        public uint ForwardNextHopAs;
        public int ForwardMetric1;
        public int ForwardMetric2;
        public int ForwardMetric3;
        public int ForwardMetric4;
        public int ForwardMetric5;
    }

    [LibraryImport("iphlpapi.dll", EntryPoint = "GetBestRoute")]
    public static partial int GetBestRoute(uint destAddr, uint sourceAddr, out MibIpForwardRow bestRoute);

    [LibraryImport("iphlpapi.dll", EntryPoint = "ConvertInterfaceAliasToLuid", StringMarshalling = StringMarshalling.Utf16)]
    public static partial int ConvertInterfaceAliasToLuid(string alias, out ulong luid);

    [LibraryImport("iphlpapi.dll", EntryPoint = "ConvertInterfaceLuidToIndex")]
    public static partial int ConvertInterfaceLuidToIndex(in ulong luid, out uint index);
}
