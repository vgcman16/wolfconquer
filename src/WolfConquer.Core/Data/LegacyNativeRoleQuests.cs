using System.IO;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Entire native RoleQuests Save/Load. No truncated-file recovery or auto-owner.
internal class LegacyNativeRoleQuests
{
	public unsafe static void Save(LegacyNativeGameClientRuntime user)
	{
		LegacyNativeBinaryFile binaryFile = new LegacyNativeBinaryFile();
		if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\Quests\\" + user.Player.UID + ".bin", FileMode.Create))
		{
			return;
		}
		SaveOpenedFile(binaryFile, user);
	}

	// Native body on the SAME genuine opened file, for owned-file verification.
	// No finally: a body fault still skips the native Close.
	internal unsafe static void SaveOpenedFile(LegacyNativeBinaryFile binaryFile, LegacyNativeGameClientRuntime user)
	{
		int count = user.Player.QuestGUI.src.Count;
		binaryFile.Write(&count, 4);
		foreach (LegacyNativeMsgQuestList.QuestListItem value in user.Player.QuestGUI.src.Values)
		{
			uint uID = value.UID;
			uint status = (uint)value.Status;
			uint time = value.Time;
			int num = value.Intentions.Length;
			binaryFile.Write(&uID, 4);
			binaryFile.Write(&status, 4);
			binaryFile.Write(&time, 4);
			binaryFile.Write(&num, 4);
			for (int i = 0; i < num; i++)
			{
				uint num2 = value.Intentions[i];
				binaryFile.Write(&num2, 4);
			}
		}
		binaryFile.Close();
	}

	public unsafe static void Load(LegacyNativeGameClientRuntime user)
	{
		LegacyNativeBinaryFile binaryFile = new LegacyNativeBinaryFile();
		if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\Quests\\" + user.Player.UID + ".bin", FileMode.Open))
		{
			return;
		}
		LoadOpenedFile(binaryFile, user);
	}

	// Native Load has NO Close; this body does not add one or clear/replace quests.
	// Tests may explicitly close their OWN file after exercising the whole body.
	internal unsafe static void LoadOpenedFile(LegacyNativeBinaryFile binaryFile, LegacyNativeGameClientRuntime user)
	{
		int count = user.Player.QuestGUI.src.Count;
		binaryFile.Read(&count, 4);
		uint uID = default(uint);
		uint status = default(uint);
		uint time = default(uint);
		int num = default(int);
		uint num2 = default(uint);
		for (int i = 0; i < count; i++)
		{
			binaryFile.Read(&uID, 4);
			binaryFile.Read(&status, 4);
			binaryFile.Read(&time, 4);
			binaryFile.Read(&num, 4);
			uint[] array = new uint[num];
			for (int j = 0; j < num; j++)
			{
				binaryFile.Read(&num2, 4);
				array[j] = num2;
			}
			LegacyNativeMsgQuestList.QuestListItem questListItem = new LegacyNativeMsgQuestList.QuestListItem
			{
				UID = uID,
				Status = (LegacyNativeMsgQuestList.QuestListItem.QuestStatus)status,
				Time = time,
				Intentions = array
			};
			if (!user.Player.QuestGUI.src.ContainsKey(questListItem.UID))
			{
				user.Player.QuestGUI.src.Add(questListItem.UID, questListItem);
			}
			if (questListItem.Status == LegacyNativeMsgQuestList.QuestListItem.QuestStatus.Accepted)
			{
				user.Player.QuestGUI.AcceptedQuests.Add(questListItem.UID, questListItem);
			}
		}
	}
}
