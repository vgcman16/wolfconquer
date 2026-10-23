using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal partial struct LegacyNativeMsgDetainedItem
{
	public static LegacyNativeMsgDetainedItem Create(LegacyNativeGameItemRuntime GameItem)
	{
		return new LegacyNativeMsgDetainedItem
		{
			ItemUID = GameItem.Uid,
			ItemID = GameItem.ITEM_ID,
			Durability = GameItem.Durability,
			MaximDurability = GameItem.MaximDurability,
			SocketProgress = GameItem.SocketProgress,
			SocketOne = GameItem.SocketOne,
			SocketTwo = GameItem.SocketTwo,
			Effect = GameItem.Effect,
			Plus = GameItem.Plus,
			Bless = GameItem.Bless,
			Bound = (GameItem.Bound == 1),
			Enchant = GameItem.Enchant,
			Suspicious = GameItem.Suspicious,
			Lock = GameItem.Locked,
			PlusProgres = GameItem.PlusProgress,
			PerfectionLevel = GameItem.PerfectionLevel,
			PerfectionProgres = GameItem.PerfectionProgress,
			Signature = GameItem.Signature,
			PerfectionOwnerName = GameItem.OwnerName,
			RuneEXP = GameItem.RuneEXP,
			AnimaItemID = GameItem.AnimaItemID,
			Attribute1 = GameItem.RelicAttributes[0],
			Attribute2 = GameItem.RelicAttributes[1],
			Attribute3 = GameItem.RelicAttributes[2],
			Attribute4 = GameItem.RelicAttributes[3],
			Attribute5 = GameItem.RelicAttributes[4]
		};
	}

	public static LegacyNativeGameItemRuntime CopyTo(LegacyNativeMsgDetainedItem Item)
	{
		LegacyNativeGameItemRuntime msgGameItem = new LegacyNativeGameItemRuntime();
		msgGameItem.Purification = LegacyNativeItemExtra.Purification.ShallowCopy(Item.Purification);
		msgGameItem.Refinary = LegacyNativeItemExtra.Refinery.ShallowCopy(Item.Refinary);
		msgGameItem.Uid = Item.ItemUID;
		msgGameItem.ITEM_ID = Item.ItemID;
		msgGameItem.Bless = Item.Bless;
		msgGameItem.Bound = (byte)(Item.Bound ? 1u : 0u);
		msgGameItem.Durability = Item.Durability;
		msgGameItem.Effect = Item.Effect;
		msgGameItem.Enchant = Item.Enchant;
		msgGameItem.MaximDurability = Item.MaximDurability;
		msgGameItem.Plus = Item.Plus;
		msgGameItem.PlusProgress = Item.PlusProgres;
		msgGameItem.SocketOne = Item.SocketOne;
		msgGameItem.SocketProgress = Item.SocketProgress;
		msgGameItem.SocketTwo = Item.SocketTwo;
		msgGameItem.Suspicious = (byte)Item.Suspicious;
		msgGameItem.PerfectionLevel = Item.PerfectionLevel;
		msgGameItem.PerfectionProgress = Item.PerfectionProgres;
		msgGameItem.OwnerUID = Item.OwnerUID;
		msgGameItem.OwnerName = Item.PerfectionOwnerName;
		msgGameItem.Signature = Item.Signature;
		msgGameItem.RuneEXP = Item.RuneEXP;
		msgGameItem.AnimaItemID = Item.AnimaItemID;
		msgGameItem.RelicAttributes = new LegacyNativeRelicAttribute[5];
		msgGameItem.RelicAttributes[0] = new LegacyNativeRelicAttribute(Item.Attribute1);
		msgGameItem.RelicAttributes[1] = new LegacyNativeRelicAttribute(Item.Attribute2);
		msgGameItem.RelicAttributes[2] = new LegacyNativeRelicAttribute(Item.Attribute3);
		msgGameItem.RelicAttributes[3] = new LegacyNativeRelicAttribute(Item.Attribute4);
		msgGameItem.RelicAttributes[4] = new LegacyNativeRelicAttribute(Item.Attribute5);
		return msgGameItem;
	}

}
