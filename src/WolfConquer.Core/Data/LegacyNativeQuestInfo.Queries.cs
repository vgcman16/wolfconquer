namespace WolfConquer.Core.Data;

internal partial class LegacyNativeQuestInfo
{
    public static DBQuest? GetFinishQuest(uint NPCID, uint _class, uint QuestID = 0u, Func<DBQuest, bool>? P = null)
    {
        ListNpcQuests listNpcQuests = NpcQuests[NPCID];
        for (int i = 0; i < listNpcQuests.Quests.Count; i++)
        {
            DBQuest dBQuest = listNpcQuests.Quests[i];
            if (QuestID == 0)
            {
                if (dBQuest.CanAcceptQuest(_class))
                {
                    if (P == null)
                    {
                        return dBQuest;
                    }
                    if (P(dBQuest))
                    {
                        return dBQuest;
                    }
                }
            }
            else if (dBQuest.MissionId == QuestID)
            {
                return dBQuest;
            }
        }
        return null;
    }

    public static bool IsKingDomMission(uint MissionID)
    {
        return KingDomMissions.Contains(MissionID);
    }
}
