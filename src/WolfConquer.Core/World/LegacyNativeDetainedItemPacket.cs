using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeDetainedItemPacket
{
public static LegacyNativePacket DetainedItemCreate(this LegacyNativePacket stream, LegacyNativeMsgDetainedItem item)
	{
		stream.InitWriter();
		stream.Write(item.UID);
		stream.Write(item.ItemUID);
		stream.Write(item.ItemID);
		stream.Write(item.Durability);
		stream.Write(item.MaximDurability);
		stream.Write((uint)item.Action);
		stream.Write(item.SocketProgress);
		stream.Write((byte)item.SocketOne);
		stream.Write((byte)item.SocketTwo);
		stream.Write((ushort)0);
		stream.Write((uint)item.Effect);
		stream.Write((byte)0);
		stream.Write(item.Plus);
		stream.Write(item.Bless);
		stream.Write((byte)(item.Bound ? 1u : 0u));
		stream.Write((uint)item.Enchant);
		stream.Write((uint)item.Suspicious);
		stream.Write(item.Lock);
		stream.Write((byte)0);
		stream.Write((byte)0);
		stream.Write(item.PerfectionLevel);
		stream.Write(item.PerfectionProgres);
		stream.Write(item.OwnerUID);
		stream.Write(item.OwnerName, 32);
		stream.ZeroFill(4);
		stream.Write(item.Signature, 60);
		stream.Write(item.OwnerUID);
		stream.Write(item.OwnerName, 32);
		stream.Write(item.GainerUID);
		stream.Write(item.GainerName, 32);
		stream.Write(item.Date);
		stream.Write(item.RewardConquerPoints);
		stream.Write(item.ConquerPointsCost);
		stream.Write(item.DaysLeft);
		stream.Finalize(2262);
		return stream;
	}
}
