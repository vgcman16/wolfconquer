using System;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Player.cs 3228..3273, with physical source fields 815..817.
public sealed partial class LegacyNativePlayerRuntime
{
    public byte TaoistPower = 0;
    public DateTime TaoistStampPower = DateTime.Now;

	internal void UpdateTaoPower(LegacyNativePacket stream)
	{
		if (LegacyNativeAtributesStatus.IsWater(Class) && TaoistPower == 10)
		{
			if (!ContainFlag(LegacyNativeMsgUpdate.Flags.FullPowerWater))
			{
				AddFlag(LegacyNativeMsgUpdate.Flags.FullPowerWater, 2592000, RemoveOnDead: false);
			}
		}
		else if (LegacyNativeAtributesStatus.IsFire(Class) && TaoistPower == 10 && !ContainFlag(LegacyNativeMsgUpdate.Flags.FullPowerFire))
		{
			AddFlag(LegacyNativeMsgUpdate.Flags.FullPowerFire, 2592000, RemoveOnDead: false);
		}
		uint flag = (LegacyNativeAtributesStatus.IsWater(Class) ? 172u : 173u);
		LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
		stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.AppendIcon, flag, 3u, (uint)(TaoistPower * 30), 0u);
		stream = msgUpdate.GetArray(stream);
		View.SendView(stream, me: true);
	}

	internal void SendPowerTaoist(LegacyNativeGameClientRuntime user, LegacyNativePacket stream)
	{
		if (TaoistPower > 0)
		{
			uint flag = (LegacyNativeAtributesStatus.IsWater(Class) ? 172u : 173u);
			LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
			stream = msgUpdate.Append(stream, LegacyNativeMsgUpdate.DataType.AppendIcon, flag, 3u, (uint)(TaoistPower * 30), 0u);
			stream = msgUpdate.GetArray(stream);
			user.Send(stream);
		}
	}

	public void UpdateTaoistPower(DateTime now)
	{
		if (TaoistPower >= 10 || Class <= 10000 || !(now > TaoistStampPower.AddSeconds(9.0)))
		{
			return;
		}
		if (Owner.Equipment!.TryGetEquip(LegacyNativeGameItemFlags.ConquerItem.RightWeapon, out var itemdata) && LegacyNativeItemTypeRuneMetadata.IsTaoistEpicWeapon(itemdata.ITEM_ID))
		{
			TaoistPower++;
			using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
			UpdateTaoPower(recycledPacket.GetStream());
		}
		TaoistStampPower = DateTime.Now;
	}
}
