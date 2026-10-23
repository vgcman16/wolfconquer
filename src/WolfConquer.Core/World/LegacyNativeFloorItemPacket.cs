using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeFloorItemPacket
{
	public static void GetItemPacket(this LegacyNativePacket stream, out uint uid)
	{
		uid = stream.ReadUInt32();
	}

	public static LegacyNativePacket ItemPacketCreate(this LegacyNativePacket stream, LegacyNativeMsgItemPacket Item)
	{
		stream.InitWriter();
		stream.Write(Item.m_UID);
		stream.Write(Item.m_ID);
		stream.Write(Item.m_X);
		stream.Write(Item.m_Y);
		if (Item.MaxLife != 0)
		{
			stream.Write(Item.MaxLife);
		}
		else
		{
			stream.Write((ushort)40);
		}
		stream.Write((byte)Item.DropType);
		stream.Write(Item.Life);
		if (Item.DropType == LegacyNativeMsgDropID.Effect)
		{
			stream.Write(Item.m_Color);
		}
		else
		{
			stream.Write(Item.Plus);
		}
		stream.Write(Item.ItemOwnerUID);
		stream.Write(0);
		stream.Write(0);
		stream.Write(Item.GuildID);
		stream.Write(Item.FlowerType);
		stream.Write(Item.Timer);
		stream.Write(0);
		stream.Write(0);
		stream.Write(Item.OwnerX);
		stream.Write(Item.OwnerY);
		stream.ZeroFill(40);
		if (!string.IsNullOrEmpty(Item.Name))
		{
			stream.Write(Item.Name, 32);
		}
		else
		{
			stream.ZeroFill(32);
		}
		stream.Write(0u);
		stream.Write(0u);
		stream.Write(0u);
		stream.Write(0u);
		stream.Write(0u);
		stream.Finalize(2164);
		return stream;
	}
}
