namespace WolfConquer.Core.Data;

// Complete native quest metadata owner. No loader/startup caller is installed.
internal partial class LegacyNativeQuestInfo
{
    public class ListNpcQuests
    {
        public List<DBQuest> Quests = new List<DBQuest>();
    }

    public static Dictionary<uint, DBQuest> AllQuests = new Dictionary<uint, DBQuest>();

    public static Dictionary<uint, ListNpcQuests> NpcQuests = new Dictionary<uint, ListNpcQuests>();

    public static List<uint> KingDomMissions = new List<uint> { 35024u, 35007u, 35025u, 35028u, 35034u };
}
