#nullable disable
using WolfConquer.Core.Networking;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeEquipItemStorage
{
	public byte SteedPlus => (byte)Owner.Player.SteedPlus;

	public void ClearItemSpawn()
	{
		Owner.Player.ClearItemsSpawn();
	}

	public void AddSpawn(LegacyNativeGameItemRuntime DataItem)
	{
		switch ((Flags.ConquerItem)DataItem.Position)
		{
		case Flags.ConquerItem.Bottle:
		case Flags.ConquerItem.AleternanteBottle:
			Owner.Player.Bottle = DataItem.ITEM_ID;
			break;
		case Flags.ConquerItem.Armor:
		case Flags.ConquerItem.AleternanteArmor:
			Owner.Player.ArmorId = DataItem.ITEM_ID;
			Owner.Player.ColorArmor = (ushort)DataItem.Color;
			Owner.Player.ArmorSoul = DataItem.Purification.PurificationItemID;
			break;
		case Flags.ConquerItem.Head:
		case Flags.ConquerItem.AleternanteHead:
			Owner.Player.HeadId = DataItem.ITEM_ID;
			Owner.Player.ColorHelment = (ushort)DataItem.Color;
			Owner.Player.HeadSoul = DataItem.Purification.PurificationItemID;
			break;
		case Flags.ConquerItem.LeftWeapon:
		case Flags.ConquerItem.AleternanteLeftWeapon:
			Owner.Player.LeftWeaponId = DataItem.ITEM_ID;
			Owner.Player.LeftWeapsonSoul = DataItem.Purification.PurificationItemID;
			break;
		case Flags.ConquerItem.LeftWeaponAccessory:
			Owner.Player.LeftWeaponAccessoryId = DataItem.ITEM_ID;
			break;
		case Flags.ConquerItem.RightWeapon:
		case Flags.ConquerItem.AleternanteRightWeapon:
			Owner.Player.RightWeaponId = DataItem.ITEM_ID;
			Owner.Player.ColorShield = (ushort)DataItem.Color;
			Owner.Player.RightWeapsonSoul = DataItem.Purification.PurificationItemID;
			break;
		case Flags.ConquerItem.RightWeaponAccessory:
			Owner.Player.RightWeaponAccessoryId = DataItem.ITEM_ID;
			break;
		case Flags.ConquerItem.Steed:
			Owner.Player.SteedId = DataItem.ITEM_ID;
			Owner.Player.SteedColor = DataItem.SocketProgress;
			Owner.Player.SteedPlus = DataItem.Plus;
			SteedPlusPorgres = DataItem.PlusProgress;
			break;
		case Flags.ConquerItem.SteedMount:
			Owner.Player.MountArmorId = DataItem.ITEM_ID;
			break;
		case Flags.ConquerItem.Garment:
		case Flags.ConquerItem.AleternanteGarment:
			Owner.Player.GarmentId = DataItem.ITEM_ID;
			break;
		case Flags.ConquerItem.Wing:
			if (DataItem.Plus == 12)
			{
				Owner.Player.WingProgress = DataItem.PlusProgress;
			}
			Owner.Player.WingId = DataItem.ITEM_ID;
			Owner.Player.WingPlus = DataItem.Plus;
			break;
		}
		if (Owner.Player.SpecialGarment != 0)
		{
			Owner.Player.GarmentId = Owner.Player.SpecialGarment;
		}
	}

	public void AppendItems(bool CreateSpawn, LegacyNativeGameItemRuntime[] Items, LegacyNativePacket stream)
	{
		LegacyNativeMsgShowEquipment msgShowEquipment = new LegacyNativeMsgShowEquipment();
		msgShowEquipment.UID = 46;
		msgShowEquipment.Alternante = (byte)(Alternante ? 1u : 0u);
		if (!CreateSpawn)
		{
			return;
		}
		foreach (LegacyNativeGameItemRuntime msgGameItem in Items)
		{
			if (msgGameItem != null)
			{
				switch ((Flags.ConquerItem)msgGameItem.Position)
				{
				case Flags.ConquerItem.Ring:
				case Flags.ConquerItem.AleternanteRing:
					msgShowEquipment.Ring = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.Head:
				case Flags.ConquerItem.AleternanteHead:
					msgShowEquipment.Head = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.Necklace:
				case Flags.ConquerItem.AleternanteNecklace:
					msgShowEquipment.Necklace = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.RightWeapon:
				case Flags.ConquerItem.AleternanteRightWeapon:
					msgShowEquipment.RightWeapon = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.LeftWeapon:
				case Flags.ConquerItem.AleternanteLeftWeapon:
					msgShowEquipment.LeftWeapon = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.Armor:
				case Flags.ConquerItem.AleternanteArmor:
					msgShowEquipment.Armor = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.Boots:
				case Flags.ConquerItem.AleternanteBoots:
					msgShowEquipment.Boots = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.Bottle:
				case Flags.ConquerItem.AleternanteBottle:
					msgShowEquipment.Bottle = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.SteedMount:
					msgShowEquipment.SteedMount = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.Garment:
				case Flags.ConquerItem.AleternanteGarment:
					msgShowEquipment.Garment = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.RidingCrop:
					msgShowEquipment.RidingCrop = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.LeftWeaponAccessory:
					msgShowEquipment.LeftWeaponAccessory = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.RightWeaponAccessory:
					msgShowEquipment.RightWeaponAccessory = msgGameItem.Uid;
					break;
				case Flags.ConquerItem.Wing:
					msgShowEquipment.Wing = msgGameItem.Uid;
					break;
				}
			}
		}
		if (Owner.Player.SpecialGarment != 0)
		{
			msgShowEquipment.Garment = 4294967294u;
		}
		Owner.Send(stream.ShowEquipmentCreate(msgShowEquipment));
	}

	public void SendAlowAlternante(LegacyNativePacket stream)
	{
		LegacyNativeMsgShowEquipment msgShowEquipment = new LegacyNativeMsgShowEquipment();
		msgShowEquipment.UID = 44;
		msgShowEquipment.Alternante = (byte)(Alternante ? 1u : 0u);
		Owner.Send(stream.ShowEquipmentCreate(msgShowEquipment));
	}
}
