#nullable disable
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Complete native leaf methods only; no ExecuteLogin worker or queue publication.
internal static partial class LegacyNativeServerDatabasePersistence
{
    public static bool AllowCreate(uint UID)
    {
        return !File.Exists(LegacyNativeServerConfig.DbLocation + "\\Users\\" + UID + ".ini");
    }

    public static void UpdateGuildMember(LegacyNativeGuildRuntime.Member Member)
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("\\Users\\" + Member.UID + ".ini");
        iniFile.Write("Character", "GuildRank", (ushort)0);
    }

    public static void UpdateGuildMember(LegacyNativeGuildRuntime.UpdateDB Member)
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("\\Users\\" + Member.UID + ".ini");
        iniFile.Write("Character", "GuildRank", (ushort)0);
        iniFile.Write("Character", "GuildID", (ushort)0);
    }

    public static void UpdateMapRace(LegacyNativeGameMap map)
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("\\maps\\" + map.ID + ".ini");
        iniFile.Write("info", "race_record", map.RecordSteedRace);
    }
}
