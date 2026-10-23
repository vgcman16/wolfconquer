using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
	public void GetQuestObjectives(uint UID, out uint[] Intentions)
	{
		if (src.ContainsKey(UID))
		{
			Intentions = new uint[src[UID].Intentions.Length];
			for (int i = 0; i < src[UID].Intentions.Length; i++)
			{
				Intentions[i] = src[UID].Intentions[i];
			}
		}
		else
		{
			Intentions = new uint[1];
		}
	}

	public bool IsActiveQuest(uint UID)
	{
		return CheckQuest(UID, LegacyNativeMsgQuestList.QuestListItem.QuestStatus.Accepted);
	}

	public bool CheckKingDomQuest(uint UID, params uint[] Intentions)
	{
		if (src.ContainsKey(UID))
		{
			try
			{
				LegacyNativeMsgQuestList.QuestListItem questListItem = src[UID];
				bool result = true;
				for (int i = 0; i < Intentions.Length; i++)
				{
					if (questListItem.Intentions[i] < Intentions[i])
					{
						result = false;
					}
				}
				return result;
			}
			catch
			{
				for (int j = 0; j < 10; j++)
				{
					LegacyNativeConsole.WriteLine("Error on quest : " + UID, ConsoleColor.DarkRed);
				}
			}
		}
		return false;
	}

	public bool CheckObjectives(uint UID, params uint[] Intentions)
	{
		if (AcceptedQuests.ContainsKey(UID))
		{
			try
			{
				LegacyNativeMsgQuestList.QuestListItem questListItem = AcceptedQuests[UID];
				bool result = true;
				for (int i = 0; i < Intentions.Length; i++)
				{
					if (questListItem.Intentions[i] < Intentions[i])
					{
						result = false;
					}
				}
				return result;
			}
			catch
			{
				for (int j = 0; j < 10; j++)
				{
					LegacyNativeConsole.WriteLine("Error on quest : " + UID, ConsoleColor.DarkRed);
				}
			}
		}
		return false;
	}

	public bool CheckQuest(uint UID, LegacyNativeMsgQuestList.QuestListItem.QuestStatus status)
	{
		if (LegacyNativeQuestInfo.IsKingDomMission(UID))
		{
			return src.Values.Where((LegacyNativeMsgQuestList.QuestListItem p) => p.UID == UID && p.Status == status).Count() == 1;
		}
		if (status == LegacyNativeMsgQuestList.QuestListItem.QuestStatus.Accepted)
		{
			return AcceptedQuests.ContainsKey(UID);
		}
		return src.Values.Where((LegacyNativeMsgQuestList.QuestListItem p) => p.UID == UID && p.Status == status).Count() == 1;
	}

	public int AcceptQuestsCount()
	{
		return src.Values.Where((LegacyNativeMsgQuestList.QuestListItem p) => p.Status == LegacyNativeMsgQuestList.QuestListItem.QuestStatus.Accepted).Count();
	}

	public bool AllowAccept()
	{
		return AcceptQuestsCount() < 5;
	}
}
