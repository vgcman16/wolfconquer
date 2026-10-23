using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Native message metadata only; no HandleChi queue/handler is installed.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal partial struct LegacyNativeMsgChiInfo
{
    public enum LockedFlags : byte
    {
        None = 0,
        First = 1,
        Second = 2,
        Third = 4,
        Fourth = 8,
        All = 15
    }

    // The existing LegacyNativeChiAttribute is the native MsgChiInfo.ChiAttribute
    // enum already shared by native RelicAttribute. Do not duplicate its owner.
    public enum ChiPowerType : byte
    {
        None,
        Dragon,
        Phoenix,
        Tiger,
        Turtle
    }

    public enum Action : ushort
    {
        Send,
        Upgrade,
        InterServerStatus
    }
}
