using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Whole native ServerDatabase1154..1352. Same reused count/row/deposited map and literal routes.
// Typed ReadFile entries reuse actual admitted native record/file owners; no read-result repair.
internal static partial class LegacyNativeServerDatabasePersistence
{
public unsafe static void LoadClientItems(LegacyNativeGameClientRuntime client)
	{
		LegacyNativeItemRecordFile binaryFile = new LegacyNativeItemRecordFile();
		if (!binaryFile.Open(LegacyNativeServerConfig.DbLocation + "\\PlayersItems\\" + client.Player.UID + ".bin", FileMode.Open))
		{
			return;
		}
		int num = default(int);
		_ = binaryFile.ReadCount(ref num);
		Dictionary<uint, LegacyNativeGameItemRuntime> dictionary = new Dictionary<uint, LegacyNativeGameItemRuntime>();
		LegacyNativeClientItemRecord dBItem = default(LegacyNativeClientItemRecord);
		for (int i = 0; i < num; i++)
		{
			_ = binaryFile.ReadRecord(ref dBItem);
			if (dBItem.ITEM_ID == 750000)
			{
				client.DemonExterminator!.ItemUID = dBItem.UID;
			}
			if (dBItem.ITEM_ID == 205901)
			{
				continue;
			}
			if (dBItem.ITEM_ID / 1000 == 203)
			{
				if (dBItem.Enchant > 0)
				{
					dBItem.Enchant = 0;
				}
				if (dBItem.Bless > 1)
				{
					dBItem.Bless = 0;
				}
			}
			if (dBItem.Bless > 7)
			{
				dBItem.Bless = 7;
			}
			if (dBItem.Plus > 12)
			{
				dBItem.Plus = 12;
			}
			if (dBItem.Enchant > byte.MaxValue)
			{
				dBItem.Enchant = byte.MaxValue;
			}
			if (dBItem.ITEM_ID / 1000 == 201 || dBItem.ITEM_ID / 1000 == 202 || dBItem.ITEM_ID / 1000 == 203)
			{
				if (dBItem.Bless > 1)
				{
					dBItem.Bless = 1;
				}
				if (dBItem.Enchant > 0)
				{
					dBItem.Enchant = 0;
				}
			}
			if (dBItem.ITEM_ID / 1000 == 204)
			{
				if (dBItem.Bless > 0)
				{
					dBItem.Bless = 0;
				}
				if (dBItem.Enchant > 0)
				{
					dBItem.Enchant = 0;
				}
			}
			LegacyNativeGameItemRuntime dataItem = dBItem.GetDataItem();
			if (dBItem.DepositeCount != 0)
			{
				uint depositeCount = dBItem.DepositeCount;
				for (int j = 0; j < depositeCount; j++)
				{
					_ = binaryFile.ReadRecord(ref dBItem);
					if (dBItem.ITEM_ID == 750000)
					{
						client.DemonExterminator!.ItemUID = dBItem.ITEM_ID;
					}
					LegacyNativeGameItemRuntime dataItem2 = dBItem.GetDataItem();
					if (client.Player.GuildID == 0)
					{
						dataItem2.Inscribed = 0;
					}
					dataItem.Deposite.TryAdd(dataItem2.Uid, dataItem2);
					if (!dictionary.ContainsKey(dataItem2.Uid))
					{
						dictionary.Add(dataItem2.Uid, dataItem2);
					}
				}
			}
			if (dBItem.WH_ID != 0)
			{
				if (dBItem.WH_ID == 100)
				{
					client.MyWardrobe!.AddItem(dataItem);
					if (dBItem.Position > 0 && dBItem.Position <= 59)
					{
						client.Equipment!.ClientItems.TryAdd(dBItem.UID, dataItem);
					}
					continue;
				}
				if (!client.Warehouse!.ClientItems.ContainsKey(dBItem.WH_ID))
				{
					client.Warehouse!.ClientItems.TryAdd(dBItem.WH_ID, new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>());
				}
				if (client.Player.GuildID == 0)
				{
					dataItem.Inscribed = 0;
				}
				client.Warehouse!.ClientItems[dBItem.WH_ID].TryAdd(dBItem.UID, dataItem);
				continue;
			}
			if (dBItem.Position == LegacyNativeGameItemRuntime.MythsoulBag)
			{
				client.GearMythsoulBag!.Add(dataItem.Uid, dataItem);
			}
			if (dBItem.Position >= 213 && dBItem.Position <= 215)
			{
				client.RelicFuse!.Add(dataItem.Uid, dataItem);
			}
			else if (dBItem.Position > 0 && dBItem.Position <= 63)
			{
				if (client.Player.GuildID == 0)
				{
					dataItem.Inscribed = 0;
				}
				client.Equipment!.ClientItems.TryAdd(dBItem.UID, dataItem);
			}
			else if (dBItem.Position == 0)
			{
				if (client.Player.GuildID == 0)
				{
					dataItem.Inscribed = 0;
				}
				client.Inventory!.AddDBItem(dataItem);
			}
			else if (dBItem.Position == 211 || dBItem.Position == 212 || (dBItem.Position >= 101 && dBItem.Position <= 135))
			{
				if (!LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(LegacyNativePoolItems.ItemsBase![dataItem.ITEM_ID], client))
				{
					dBItem.Position = 212;
				}
				if (client.Rune!.Free(dBItem.Position))
				{
					client.Rune!.Add(dataItem);
				}
			}
		}
		_ = binaryFile.ReadCount(ref num);
		for (int k = 0; k < num; k++)
		{
			LegacyNativeClientItemPerfectionRecord perfection = default(LegacyNativeClientItemPerfectionRecord);
			_ = binaryFile.ReadPerfection(ref perfection);
			if (dictionary.ContainsKey(perfection.ItemUID))
			{
				LegacyNativeGameItemRuntime msgGameItem = dictionary[perfection.ItemUID];
				msgGameItem.PerfectionLevel = perfection.Level;
				msgGameItem.OwnerUID = perfection.OwnerUID;
				msgGameItem.OwnerName = perfection.OwnerName;
				msgGameItem.PerfectionProgress = perfection.Progres;
				msgGameItem.Signature = perfection.SpecialText;
				continue;
			}
			if (client.Equipment!.ClientItems.ContainsKey(perfection.ItemUID))
			{
				LegacyNativeGameItemRuntime msgGameItem2 = client.Equipment!.ClientItems[perfection.ItemUID];
				msgGameItem2.PerfectionLevel = perfection.Level;
				msgGameItem2.OwnerUID = perfection.OwnerUID;
				msgGameItem2.OwnerName = perfection.OwnerName;
				msgGameItem2.PerfectionProgress = perfection.Progres;
				msgGameItem2.Signature = perfection.SpecialText;
				continue;
			}
			if (client.Inventory!.ClientItems.ContainsKey(perfection.ItemUID))
			{
				LegacyNativeGameItemRuntime msgGameItem3 = client.Inventory!.ClientItems[perfection.ItemUID];
				msgGameItem3.PerfectionLevel = perfection.Level;
				msgGameItem3.OwnerUID = perfection.OwnerUID;
				msgGameItem3.OwnerName = perfection.OwnerName;
				msgGameItem3.PerfectionProgress = perfection.Progres;
				msgGameItem3.Signature = perfection.SpecialText;
				continue;
			}
			foreach (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> value in client.Warehouse!.ClientItems.Values)
			{
				if (value.ContainsKey(perfection.ItemUID))
				{
					LegacyNativeGameItemRuntime msgGameItem4 = value[perfection.ItemUID];
					msgGameItem4.PerfectionLevel = perfection.Level;
					msgGameItem4.OwnerUID = perfection.OwnerUID;
					msgGameItem4.OwnerName = perfection.OwnerName;
					msgGameItem4.PerfectionProgress = perfection.Progres;
					msgGameItem4.Signature = perfection.SpecialText;
					break;
				}
			}
		}
		binaryFile.Close();
	}
}
