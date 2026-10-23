using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal partial struct LegacyNativeMsgDetainedItem
{
	public LegacyNativeInventoryRuntime? Send(LegacyNativeGameClientRuntime client, LegacyNativePacket stream)
	{
		if (Action == ContainerType.RewardCps)
		{
			OwnerUID = 500u;
			ItemID = 0u;
			Durability = 0;
			MaximDurability = 0;
			SocketOne = Flags.Gem.NoSocket;
			SocketTwo = Flags.Gem.NoSocket;
			Effect = Flags.ItemEffect.None;
			Plus = 0;
			Bless = 0;
			Enchant = 0;
			SocketProgress = 0u;
			Bound = false;
			Lock = 0;
			client.Send(stream.DetainedItemCreate(this));
			return client.Inventory;
		}
		LegacyNativeMsgDetainedItem msgDetainedItem = this;
		client.Send(stream.DetainedItemCreate(this));
		if (Purification.ItemUID != 0 || Refinary.ItemUID != 0)
		{
			LegacyNativeItemExtra msgItemExtra = new LegacyNativeItemExtra();
			if (Purification.InLife)
			{
				Purification.Typ = LegacyNativeItemExtra.Typing.PurificationAdding;
				msgItemExtra.Purifications.Add(Purification);
			}
			if (Refinary.InLife)
			{
				Refinary.Typ = LegacyNativeItemExtra.Typing.RefinaryAdding;
				if (Refinary.EffectDuration == 0)
				{
					Refinary.Typ = LegacyNativeItemExtra.Typing.PermanentRefinery;
				}
				msgItemExtra.Refinerys.Add(Refinary);
			}
			client.Send(msgItemExtra.CreateArray(stream));
		}
		return client.Inventory;
	}
}
