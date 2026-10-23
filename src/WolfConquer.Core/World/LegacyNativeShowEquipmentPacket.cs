using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static class LegacyNativeShowEquipmentPacket
{
	public static LegacyNativePacket ShowEquipmentCreate(this LegacyNativePacket stream, LegacyNativeMsgShowEquipment item)
	{
		stream.InitWriter();
		stream.Write(item.wParam);
		stream.Write(item.Alternante);
		stream.Write(0);
		stream.Write(item.UID);
		stream.SeekForward(13);
		stream.Write(item.Head);
		stream.Write(item.Necklace);
		stream.Write(item.Armor);
		stream.Write(item.RightWeapon);
		stream.Write(item.LeftWeapon);
		stream.Write(item.Ring);
		stream.Write(item.Bottle);
		stream.Write(item.Boots);
		stream.Write(item.Garment);
		stream.Write(item.RightWeaponAccessory);
		stream.Write(item.LeftWeaponAccessory);
		stream.Write(item.SteedMount);
		stream.Write(item.RidingCrop);
		stream.Write(item.Wing);
		stream.Finalize(2137);
		return stream;
	}
}
