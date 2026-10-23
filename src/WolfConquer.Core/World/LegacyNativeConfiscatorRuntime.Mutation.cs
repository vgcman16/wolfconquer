using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeConfiscatorRuntime
{
internal void AddItem(LegacyNativeGameClientRuntime Owner, LegacyNativeGameClientRuntime Gainer, LegacyNativeGameItemRuntime GameItem, LegacyNativePacket stream)
	{
		LegacyNativeMsgDetainedItem msgDetainedItem = LegacyNativeMsgDetainedItem.Create(GameItem);
		msgDetainedItem.UID = CounterUID.Next;
		msgDetainedItem.OwnerUID = Owner.Player.UID;
		msgDetainedItem.OwnerName = Owner.Player.Name;
		msgDetainedItem.GainerUID = Gainer.Player.UID;
		msgDetainedItem.GainerName = Gainer.Player.Name;
		msgDetainedItem.Refinary = LegacyNativeItemExtra.Refinery.ShallowCopy(GameItem.Refinary);
		msgDetainedItem.Purification = LegacyNativeItemExtra.Purification.ShallowCopy(GameItem.Purification);
		msgDetainedItem.ConquerPointsCost = CalculateCpsCost(msgDetainedItem);
		DateTime now = DateTime.Now;
		msgDetainedItem.Date = CreateTimer(now.Year, now.Month, now.Day);
		msgDetainedItem.Action = LegacyNativeMsgDetainedItem.ContainerType.DetainPage;
		msgDetainedItem.Send(Owner, stream);
		msgDetainedItem.Action = LegacyNativeMsgDetainedItem.ContainerType.ClaimPage;
		msgDetainedItem.Send(Gainer, stream);
		Owner.Confiscator!.RedeemContainer.TryAdd(msgDetainedItem.UID, msgDetainedItem);
		Gainer.Confiscator!.ClaimContainer.TryAdd(msgDetainedItem.UID, msgDetainedItem);
		ushort x = Owner.Player.X;
		ushort y = Owner.Player.Y;
		if (Owner.Map!.AddGroundItem(ref x, ref y, 2))
		{
			LegacyNativeMsgItemPacket msgItemPacket = LegacyNativeMsgItemPacket.Create();
			msgItemPacket.DropType = LegacyNativeMsgDropID.DropDetain;
			msgItemPacket.m_Color = (byte)GameItem.Color;
			msgItemPacket.m_ID = GameItem.ITEM_ID;
			if (GameItem.Purification.PurificationItemID != 0)
			{
				msgItemPacket.m_ID = GameItem.Purification.PurificationItemID;
			}
			msgItemPacket.m_UID = GameItem.Uid;
			msgItemPacket.m_X = x;
			msgItemPacket.m_Y = y;
			Owner.Player.View.SendView(stream.ItemPacketCreate(msgItemPacket), me: true);
		}
		LegacyNativePoolConfiscator.QueueContainer.QueueObj(Owner.Player.UID, Owner.Confiscator!);
		LegacyNativePoolConfiscator.QueueContainer.QueueObj(Gainer.Player.UID, Gainer.Confiscator!);
	}
}
