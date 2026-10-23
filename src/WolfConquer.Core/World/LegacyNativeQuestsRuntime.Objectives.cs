using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeQuestsRuntime
{
	public void SetKingDomQuestObjectives(LegacyNativePacket stream, uint UID, params uint[] Intentions)
	{
		if (!LegacyNativeQuestInfo.IsKingDomMission(UID) || !src.ContainsKey(UID))
		{
			return;
		}
		LegacyNativeMsgQuestList.QuestListItem questListItem = src[UID];
		if (questListItem.Status == LegacyNativeMsgQuestList.QuestListItem.QuestStatus.Finished)
		{
			return;
		}
		if (Intentions.Length > questListItem.Intentions.Length)
		{
			uint[] array = new uint[questListItem.Intentions.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = questListItem.Intentions[i];
			}
			questListItem.Intentions = new uint[Intentions.Length];
			for (int j = 0; j < array.Length; j++)
			{
				questListItem.Intentions[j] = array[j];
			}
		}
		for (int k = 0; k < Intentions.Length; k++)
		{
			questListItem.Intentions[k] = Intentions[k];
		}
		src[UID] = questListItem;
		Player.Owner.Send(stream.MsgQuestDataCreate(0u, UID, questListItem.Intentions));
	}

	public void SetQuestObjectives(LegacyNativePacket stream, uint UID, params uint[] Intentions)
	{
		if (!src.ContainsKey(UID) || !IsActiveQuest(UID))
		{
			return;
		}
		LegacyNativeMsgQuestList.QuestListItem questListItem = src[UID];
		if (Intentions.Length > questListItem.Intentions.Length)
		{
			uint[] array = new uint[questListItem.Intentions.Length];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = questListItem.Intentions[i];
			}
			questListItem.Intentions = new uint[Intentions.Length];
			for (int j = 0; j < array.Length; j++)
			{
				questListItem.Intentions[j] = array[j];
			}
		}
		for (int k = 0; k < Intentions.Length; k++)
		{
			questListItem.Intentions[k] = Intentions[k];
		}
		src[UID] = questListItem;
		Player.Owner.Send(stream.MsgQuestDataCreate(0u, UID, questListItem.Intentions));
	}

	public void IncreaseQuestObjectives(LegacyNativePacket stream, uint UID, params uint[] Intentions)
	{
		if (LegacyNativeQuestInfo.IsKingDomMission(UID))
		{
			if (!src.ContainsKey(UID))
			{
				return;
			}
			LegacyNativeMsgQuestList.QuestListItem questListItem = src[UID];
			if (questListItem.Status == LegacyNativeMsgQuestList.QuestListItem.QuestStatus.Finished)
			{
				return;
			}
			if (Intentions.Length > questListItem.Intentions.Length)
			{
				uint[] array = new uint[questListItem.Intentions.Length];
				for (int i = 0; i < array.Length; i++)
				{
					array[i] = questListItem.Intentions[i];
				}
				questListItem.Intentions = new uint[Intentions.Length];
				for (int j = 0; j < array.Length; j++)
				{
					questListItem.Intentions[j] = array[j];
				}
			}
			for (int k = 0; k < Intentions.Length; k++)
			{
				questListItem.Intentions[k] += Intentions[k];
			}
			src[UID] = questListItem;
			Player.Owner.Send(stream.MsgQuestDataCreate(0u, UID, questListItem.Intentions));
		}
		else
		{
			if (!IsActiveQuest(UID) || !AcceptedQuests.ContainsKey(UID))
			{
				return;
			}
			LegacyNativeMsgQuestList.QuestListItem questListItem2 = AcceptedQuests[UID];
			if (Intentions.Length > questListItem2.Intentions.Length)
			{
				uint[] array2 = new uint[questListItem2.Intentions.Length];
				for (int l = 0; l < array2.Length; l++)
				{
					array2[l] = questListItem2.Intentions[l];
				}
				questListItem2.Intentions = new uint[Intentions.Length];
				for (int m = 0; m < array2.Length; m++)
				{
					questListItem2.Intentions[m] = array2[m];
				}
			}
			for (int n = 0; n < Intentions.Length; n++)
			{
				questListItem2.Intentions[n] += Intentions[n];
			}
			AcceptedQuests[UID] = questListItem2;
			Player.Owner.Send(stream.MsgQuestDataCreate(0u, UID, questListItem2.Intentions));
		}
	}
}
