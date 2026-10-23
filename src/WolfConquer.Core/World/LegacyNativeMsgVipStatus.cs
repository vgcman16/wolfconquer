using System.Runtime.InteropServices;

namespace WolfConquer.Core.World;

// Complete native MsgVipStatus. None is65536, NOT zero; the enum is not [Flags].
[StructLayout(LayoutKind.Sequential, Size = 1)]
internal struct LegacyNativeMsgVipStatus
{
    public enum VipFlags
    {
        PortalTeleport = 1,
        Avatar = 2,
        MoreForVip = 4,
        FrozenGrot = 8,
        TeleportTeam = 16,
        CityTeleport = 32,
        CityTeleportTeam = 64,
        VipLevelOne = 34863,
        BlessTime = 128,
        OflineTG = 256,
        RefinaryAndArtefacts = 512,
        Friends = 1024,
        VipHaire = 2048,
        Labirint = 4096,
        DailyQuests = 8192,
        VipFurniture = 16384,
        BonusLotery = 32768,
        FullVip = 65535,
        None = 65536
    }
}
