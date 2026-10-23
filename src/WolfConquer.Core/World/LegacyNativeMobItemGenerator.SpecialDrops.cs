namespace WolfConquer.Core.World;

internal partial class LegacyNativeMobItemGenerator
{
	public uint GenerateItemId(uint map, out byte dwItemQuality, out bool Special, out LegacyNativeItemRuneMetadata? DbItem)
	{
		Special = false;
		LegacyNativeSpecialItemWatcher[] dropSpecials = Family.DropSpecials!;
		for (int i = 0; i < dropSpecials.Length; i++)
		{
			LegacyNativeSpecialItemWatcher specialItemWatcher = dropSpecials[i];
			if ((bool)specialItemWatcher.Rate!)
			{
				Special = true;
				dwItemQuality = (byte)(specialItemWatcher.ID % 10);
				if (LegacyNativePoolItems.ItemsBase!.TryGetValue(specialItemWatcher.ID, out DbItem))
				{
					return specialItemWatcher.ID;
				}
			}
		}
		if ((bool)DropHp)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(Family.DropHPItem, out DbItem))
			{
				return Family.DropHPItem;
			}
		}
		if ((bool)DropMp)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(Family.DropMPItem, out DbItem))
			{
				return Family.DropMPItem;
			}
		}
		if ((bool)DragonBalls)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(1088000u, out DbItem))
			{
				return 1088000u;
			}
		}
		if ((bool)Letter14)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(3303131u, out DbItem))
			{
				return 3303131u;
			}
		}
		if ((bool)LetterC)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(3303124u, out DbItem))
			{
				return 3303124u;
			}
		}
		if ((bool)LetterO)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(3303125u, out DbItem))
			{
				return 3303125u;
			}
		}
		if ((bool)LetterN)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(3303126u, out DbItem))
			{
				return 3303126u;
			}
		}
		if ((bool)LetterQ)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(3303127u, out DbItem))
			{
				return 3303127u;
			}
		}
		if ((bool)LetterU)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(3303128u, out DbItem))
			{
				return 3303128u;
			}
		}
		if ((bool)LetterE)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(3303129u, out DbItem))
			{
				return 3303129u;
			}
		}
		if ((bool)LetterR)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(3303130u, out DbItem))
			{
				return 3303130u;
			}
		}
		if ((bool)Chi100)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(729476u, out DbItem))
			{
				return 729476u;
			}
		}
		if ((bool)Study20)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(723341u, out DbItem))
			{
				return 723341u;
			}
		}
		if ((bool)MoonBox)
		{
			dwItemQuality = 0;
			Special = true;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(721080u, out DbItem))
			{
				return 721080u;
			}
		}
		if (map == 1001 || LegacyNativeGameMap.IsFrozengrotoMaps(map))
		{
			if ((bool)Chi300)
			{
				dwItemQuality = 0;
				Special = true;
				if (LegacyNativePoolItems.ItemsBase!.TryGetValue(729478u, out DbItem))
				{
					return 729478u;
				}
			}
			if ((bool)Bomb)
			{
				dwItemQuality = 0;
				Special = true;
				if (LegacyNativePoolItems.ItemsBase!.TryGetValue(721261u, out DbItem))
				{
					return 721261u;
				}
			}
			if ((bool)LuckyAmulet)
			{
				dwItemQuality = 0;
				Special = true;
				if (LegacyNativePoolItems.ItemsBase!.TryGetValue(723087u, out DbItem))
				{
					return 723087u;
				}
			}
		}
		dwItemQuality = GenerateQuality();
		uint num = 0u;
		uint num2 = 0u;
		int num3 = LegacyNativeBaseFunc.RandGet(1200, bRealRand: false);
		if ((num3 < 0 || num3 >= 20) && (num3 < 20 || num3 >= 50) && (num3 < 50 || num3 >= 100) && (num3 < 100 || num3 >= 400) && (num3 < 400 || num3 >= 700))
		{
			int num4 = LegacyNativeBaseFunc.RandGet(100, bRealRand: false);
			if ((num4 < 0 || num4 >= 20) && (num4 < 40 || num4 >= 80) && num3 >= 80 && num3 >= 100)
			{
			}
		}
		if (num2 != 99)
		{
			num2 = AlterItemLevel(num2, num);
			uint num5 = num * 1000 + num2 * 10 + dwItemQuality;
			if (LegacyNativePoolItems.ItemsBase!.TryGetValue(num5, out DbItem))
			{
				ushort position = LegacyNativeItemPosition.ItemPosition(num5);
				byte b = LegacyNativeItemTypeRuneMetadata.ItemMaxLevel((LegacyNativeGameItemFlags.ConquerItem)position);
				if (DbItem.Level > b)
				{
					return 0u;
				}
				return num5;
			}
		}
		DbItem = null;
		return 0u;
	}
}
