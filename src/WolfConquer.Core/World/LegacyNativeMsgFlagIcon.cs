using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Native metadata and complete echo handler. No host packet registration.
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgFlagIcon
{
    public enum ShowIcon : uint
    {
        TyrantAura = 1u,
        FeandAura = 2u,
        MetalAura = 3u,
        WoodAura = 4u,
        WaterAura = 5u,
        FireAura = 6u,
        EarthAura = 7u,
        MagicDefender = 8u,
        WackeSpirit = 10u,
        NobleSpirit = 9u
    }

    internal static void MsgFlagIconHandler(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
    {
        stream.GetFlagIcon(out var proto);
        user.Send(stream.FlagIconCreate(proto.UID, proto.Icon, proto.Level, proto.Damage));
    }
}
