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
	public bool FullSuper
	{
		get
		{
			if (!SuperArmor)
			{
				return false;
			}
			LegacyNativeGameItemRuntime[] curentEquip = CurentEquip;
			foreach (LegacyNativeGameItemRuntime msgGameItem in curentEquip)
			{
				if (msgGameItem.Position != 12 && msgGameItem.Position != 9 && msgGameItem.Position != 7 && msgGameItem.Position != 57 && msgGameItem.Position != 59 && msgGameItem.Position != 17 && msgGameItem.Position != 13 && msgGameItem.Position != 15 && msgGameItem.Position != 16 && msgGameItem.ITEM_ID % 10 != 9)
				{
					return false;
				}
			}
			return true;
		}
	}

	public bool CanUpdatePerfectionItem(LegacyNativeGameItemRuntime item)
	{
		int num = CurentEquip.Where((LegacyNativeGameItemRuntime p) => p.PerfectionLevel == item.PerfectionLevel).Count();
		uint num2 = ((!LegacyNativeItemTypeRuneMetadata.IsTwoHand(RightWeapon)) ? 1u : 0u);
		if (LegacyNativeItemTypeRuneMetadata.IsShield(LeftWeapon))
		{
			num2 = 1u;
		}
		if (num == 11 + num2)
		{
			return true;
		}
		uint num3 = 0u;
		LegacyNativeGameItemRuntime[] curentEquip = CurentEquip;
		foreach (LegacyNativeGameItemRuntime msgGameItem in curentEquip)
		{
			if (msgGameItem.PerfectionLevel > num3)
			{
				num3 = msgGameItem.PerfectionLevel;
			}
		}
		return num3 >= item.PerfectionLevel;
	}

	public int AttackSpeed(int MS_Delay)
	{
		MS_Delay = Math.Max(300, MS_Delay - 100);
		MS_Delay = Math.Max(300, MS_Delay - SpeedR);
		MS_Delay = Math.Max(300, MS_Delay - SpeedL);
		MS_Delay = Math.Max(300, MS_Delay - SpeedRing);
		MS_Delay = Math.Max(300, MS_Delay - Owner.Player.Agility / 2);
		if (Owner.Player.ContainFlag(LegacyNativeMsgUpdate.Flags.Cyclone))
		{
			MS_Delay = Math.Max(300, MS_Delay - 150);
		}
		byte itemLevel = 0;
		if (Owner.Rune.IsEquipped("DeadlySight", ref itemLevel))
		{
			MS_Delay = Math.Max(300, MS_Delay - (25 + itemLevel * 10));
		}
		return MS_Delay;
	}

	public int AttackSpeed(bool physical)
	{
		return AttackSpeed(800);
	}

	public int GetAttackRange(int targetSizeAdd)
	{
		int num = 1;
		if (rangeR != 0 && rangeL != 0)
		{
			num = (rangeR + rangeL) / 2;
		}
		else if (rangeR != 0)
		{
			num = rangeR;
		}
		else if (rangeL != 0)
		{
			num = rangeL;
		}
		return num + (SizeAdd + targetSizeAdd + 1) / 2;
	}

	public bool UseEpicTrojanWeapon()
	{
		LegacyNativeGameItemRuntime[] curentEquip = CurentEquip;
		foreach (LegacyNativeGameItemRuntime msgGameItem in curentEquip)
		{
			if (msgGameItem != null && LegacyNativeItemTypeRuneMetadata.IsTrojanEpicWeapon(msgGameItem.ITEM_ID))
			{
				return true;
			}
		}
		return false;
	}

	public bool UseEpicNinjaWeapon()
	{
		LegacyNativeGameItemRuntime[] curentEquip = CurentEquip;
		foreach (LegacyNativeGameItemRuntime msgGameItem in curentEquip)
		{
			if (msgGameItem != null && LegacyNativeItemTypeRuneMetadata.IsNinjaEpicWeapon(msgGameItem.ITEM_ID))
			{
				return true;
			}
		}
		return false;
	}

	public bool UseWindWalkerWeapons()
	{
		return LegacyNativeItemTypeRuneMetadata.IsWindWalkerWeapon(LeftWeapon) && LegacyNativeItemTypeRuneMetadata.IsWindWalkerWeapon(RightWeapon);
	}
}
