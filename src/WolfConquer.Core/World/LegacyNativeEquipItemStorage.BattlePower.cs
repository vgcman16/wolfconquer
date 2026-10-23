#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Core.World;

// Complete genuine Equip methods; full QueryEquipment/UpdateStats adoption remains held.
internal sealed partial class LegacyNativeEquipItemStorage
{
	public void CalculateBattlePower()
	{
		BattlePower = 0;
		int num = 0;
		int num2 = 0;
		LegacyNativeGameItemRuntime[] curentEquip = CurentEquip;
		foreach (LegacyNativeGameItemRuntime msgGameItem in curentEquip)
		{
			if (LegacyNativeItemPosition.ItemPosition(msgGameItem.ITEM_ID) != 7 && LegacyNativeItemPosition.ItemPosition(msgGameItem.ITEM_ID) != 9 && LegacyNativeItemPosition.ItemPosition(msgGameItem.ITEM_ID) != 16 && LegacyNativeItemPosition.ItemPosition(msgGameItem.ITEM_ID) != 15 && LegacyNativeItemPosition.ItemPosition(msgGameItem.ITEM_ID) != 17 && LegacyNativeItemPosition.ItemPosition(msgGameItem.ITEM_ID) != 13 && !LegacyNativeItemTypeRuneMetadata.IsHossu(msgGameItem.ITEM_ID))
			{
				num2 = 0;
				switch ((byte)(msgGameItem.ITEM_ID % 10))
				{
				case 9:
					num2 += 4;
					break;
				case 8:
					num2 += 3;
					break;
				case 7:
					num2 += 2;
					break;
				case 6:
					num2++;
					break;
				}
				num2 += msgGameItem.Plus;
				if (msgGameItem.SocketOne != Flags.Gem.NoSocket)
				{
					num2++;
				}
				if ((byte)((int)msgGameItem.SocketOne % 10 - 3) == 0)
				{
					num2++;
				}
				if (msgGameItem.SocketTwo != Flags.Gem.NoSocket)
				{
					num2++;
				}
				if ((byte)((int)msgGameItem.SocketTwo % 10 - 3) == 0)
				{
					num2++;
				}
				switch (msgGameItem.AnimaItemID % 100)
				{
				case 1u:
				case 2u:
				case 3u:
					num2++;
					break;
				case 19u:
					num2 += 16;
					break;
				default:
					num2 += (byte)(msgGameItem.AnimaItemID % 100 - 2);
					break;
				case 0u:
					break;
				}
				if (LegacyNativeItemTypeRuneMetadata.IsBacksword(msgGameItem.ITEM_ID) || LegacyNativeItemTypeRuneMetadata.IsTaoistEpicWeapon(msgGameItem.ITEM_ID) || (LegacyNativeItemTypeRuneMetadata.IsTwoHand(msgGameItem.ITEM_ID) && FreeEquip(Flags.ConquerItem.LeftWeapon) && FreeEquip(Flags.ConquerItem.AleternanteLeftWeapon)))
				{
					num2 *= 2;
				}
				num += num2;
			}
		}
		BattlePower = num;
	}
}
