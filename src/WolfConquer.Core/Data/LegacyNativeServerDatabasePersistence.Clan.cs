using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    // Complete native ServerDatabase1590..1596. Only the supplied member UID is
    // read; all three native removal values are zero, independent of member data.
    public static void UpdateClanMember(LegacyNativeClanMember Member)
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("\\Users\\" + Member.UID + ".ini");
        iniFile.Write("Character", "ClanID", 0u);
        iniFile.Write("Character", "ClanRank", (ushort)0);
        iniFile.Write("Character", "ClanDonation", 0u);
    }
}
