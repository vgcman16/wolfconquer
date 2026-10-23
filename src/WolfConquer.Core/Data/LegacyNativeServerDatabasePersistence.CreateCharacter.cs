using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal static partial class LegacyNativeServerDatabasePersistence
{
    // Entire native CreateCharacte1514..1545; explicit component, no eager field admission.
    public static void CreateCharacte(LegacyNativeGameClientRuntime client)
    {
        LegacyNativeItemCounterShellIni iniFile = new LegacyNativeItemCounterShellIni("\\Users\\" + client.Player.UID + ".ini");
        string name = client.Player.Name;
        byte[] bytes = Encoding.UTF8.GetBytes(name);
        string value = Convert.ToBase64String(bytes);
        iniFile.Write("Character", "UID", client.Player.UID);
        iniFile.Write("Character", "Body", client.Player.Body);
        iniFile.Write("Character", "Face", client.Player.Face);
        iniFile.WriteString("Character", "Name", value);
        iniFile.Write("Character", "Class", client.Player.Class);
        iniFile.Write("Character", "Map", client.Player.Map);
        iniFile.Write("Character", "X", client.Player.X);
        iniFile.Write("Character", "Y", client.Player.Y);
        client.ArenaStatistic = new LegacyNativeArenaUser();
        client.ArenaStatistic.ApplayInfo(client.Player);
        client.ArenaStatistic.Info.ArenaPoints = 4000u;
        LegacyNativeArenaRankings.ArenaPoll.TryAdd(client.Player.UID, client.ArenaStatistic);
        client.Player.Nobility = new LegacyNativeNobility(client);
        client.TeamArenaStatistic = new LegacyNativeTeamArenaUser();
        client.TeamArenaStatistic.ApplayInfo(client.Player);
        client.TeamArenaStatistic.Info.ArenaPoints = 4000u;
        LegacyNativeTeamArenaRankings.ArenaPoll.TryAdd(client.Player.UID, client.TeamArenaStatistic);
        client.Player.Associate = new LegacyNativeAssociateRuntime.MyAsociats(client.Player.UID);
        client.Player.Associate.MyClient = client;
        client.Player.Associate.Online = 1u;
        client.Player.Flowers = new LegacyNativeFlowersRuntime(client.Player.UID, client.Player.Name);
        client.Player.SubClass = new LegacyNativeSubClassRuntime();
        client.Player.MyChi = new LegacyNativeChiRuntime(client.Player.UID);
        client.Achievement = new LegacyNativeAchievementCollection();
        client.FullLoading = true;
    }
}
