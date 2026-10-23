using System.Linq;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRankItems
{
    // Whole native SaveRanks/LoadAllItems over genuine BinaryFile and ClientItems records.
	public unsafe static void SaveRanks()
	{
		LegacyNativeBinaryFile binaryFile = new LegacyNativeBinaryFile();
		if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\PerfectionRanking.bin", FileMode.Create))
		{
			return;
		}
		LegacyNativeClientItemRecord dBItem = default(LegacyNativeClientItemRecord);
		int num = 11;
		binaryFile.Write(&num, 4);
		foreach (Rank value in RankPoll.Values)
		{
			int count = value.Items.Count;
			binaryFile.Write(&count, 4);
			LegacyNativeGameItemRuntime[] values = value.Items.GetValues();
			foreach (LegacyNativeGameItemRuntime dataItem in values)
			{
				dBItem.GetDBItem(dataItem);
				if (!binaryFile.Write(&dBItem, sizeof(LegacyNativeClientItemRecord)))
				{
					Console.WriteLine("test");
				}
				LegacyNativeClientItemPerfectionRecord perfectionInfo = LegacyNativeClientItemPerfectionRecord.GetPerfectionInfo(dataItem);
				if (!binaryFile.Write(&perfectionInfo, sizeof(LegacyNativeClientItemPerfectionRecord)))
				{
					Console.WriteLine("test");
				}
			}
		}
		binaryFile.Close();
	}

	public unsafe static void LoadAllItems()
	{
		CreateRanks();
		if (!File.Exists(LegacyNativeServerConfig.DbLocation + "\\PerfectionRanking.bin"))
		{
			return;
		}
		LegacyNativeBinaryFile binaryFile = new LegacyNativeBinaryFile();
		if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\PerfectionRanking.bin", FileMode.Open))
		{
			return;
		}
		int num = default(int);
		binaryFile.Read(&num, 4);
		int num2 = default(int);
		LegacyNativeClientItemRecord dBItem = default(LegacyNativeClientItemRecord);
		for (int i = 0; i < num; i++)
		{
			binaryFile.Read(&num2, 4);
			for (int j = 0; j < num2; j++)
			{
				binaryFile.Read(&dBItem, sizeof(LegacyNativeClientItemRecord));
				LegacyNativeGameItemRuntime dataItem = dBItem.GetDataItem();
				LegacyNativeClientItemPerfectionRecord perfection = default(LegacyNativeClientItemPerfectionRecord);
				binaryFile.Read(&perfection, sizeof(LegacyNativeClientItemPerfectionRecord));
				dataItem.PerfectionLevel = perfection.Level;
				dataItem.OwnerUID = perfection.OwnerUID;
				dataItem.OwnerName = perfection.OwnerName;
				dataItem.PerfectionProgress = perfection.Progres;
				dataItem.Signature = perfection.SpecialText;
				RankPoll[(uint)(i + 1)].AddItem(dataItem);
			}
		}
		binaryFile.Close();
	}
}
