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
	public bool TryGetValue(uint UID, out LegacyNativeGameItemRuntime itemdata)
	{
		return ClientItems.TryGetValue(UID, out itemdata);
	}

	public bool FreeEquip(Flags.ConquerItem position)
	{
		LegacyNativeGameItemRuntime msgGameItem = ClientItems.Values.Where((LegacyNativeGameItemRuntime p) => (uint)p.Position == (uint)position).FirstOrDefault();
		return msgGameItem == null;
	}

	public bool TryGetEquip(Flags.ConquerItem position, out LegacyNativeGameItemRuntime itemdata)
	{
		itemdata = ClientItems.Values.Where((LegacyNativeGameItemRuntime p) => (uint)p.Position == (uint)position).FirstOrDefault();
		return itemdata != null;
	}

	public LegacyNativeGameItemRuntime TryGetEquip(Flags.ConquerItem position)
	{
		return ClientItems.Values.Where((LegacyNativeGameItemRuntime p) => (uint)p.Position == (uint)position).FirstOrDefault();
	}

	private Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime> ComputeWeapons()
	{
		if (!Alternante)
		{
			return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(TryGetEquip(Flags.ConquerItem.RightWeapon), TryGetEquip(Flags.ConquerItem.LeftWeapon));
		}
		if (FreeEquip(Flags.ConquerItem.AleternanteRightWeapon))
		{
			return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(TryGetEquip(Flags.ConquerItem.RightWeapon), TryGetEquip(Flags.ConquerItem.LeftWeapon));
		}
		if (FreeEquip(Flags.ConquerItem.RightWeapon))
		{
			return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(TryGetEquip(Flags.ConquerItem.AleternanteRightWeapon), TryGetEquip(Flags.ConquerItem.AleternanteLeftWeapon));
		}
		if (!FreeEquip(Flags.ConquerItem.AleternanteLeftWeapon))
		{
			return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(TryGetEquip(Flags.ConquerItem.AleternanteRightWeapon), TryGetEquip(Flags.ConquerItem.AleternanteLeftWeapon));
		}
		if (FreeEquip(Flags.ConquerItem.LeftWeapon))
		{
			return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(TryGetEquip(Flags.ConquerItem.AleternanteRightWeapon), null);
		}
		LegacyNativeGameItemRuntime msgGameItem = TryGetEquip(Flags.ConquerItem.AleternanteRightWeapon);
		LegacyNativeGameItemRuntime msgGameItem2 = TryGetEquip(Flags.ConquerItem.LeftWeapon);
		if (LegacyNativeItemTypeRuneMetadata.IsTwoHand(msgGameItem.ITEM_ID))
		{
			if (LegacyNativeItemTypeRuneMetadata.IsArrow(msgGameItem2.ITEM_ID))
			{
				if (LegacyNativeItemTypeRuneMetadata.IsBow(msgGameItem.ITEM_ID))
				{
					return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(msgGameItem, msgGameItem2);
				}
				return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(msgGameItem, null);
			}
			if (LegacyNativeItemTypeRuneMetadata.IsShield(msgGameItem2.ITEM_ID))
			{
				if (!Owner.MySpells.ClientSpells.ContainsKey(10311))
				{
					return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(msgGameItem, null);
				}
				return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(msgGameItem, msgGameItem2);
			}
			return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(msgGameItem, null);
		}
		if (!LegacyNativeItemTypeRuneMetadata.IsTwoHand(msgGameItem2.ITEM_ID))
		{
			return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(msgGameItem, msgGameItem2);
		}
		return new Tuple<LegacyNativeGameItemRuntime, LegacyNativeGameItemRuntime>(msgGameItem, null);
	}
}
