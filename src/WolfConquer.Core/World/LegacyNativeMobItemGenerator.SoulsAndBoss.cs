namespace WolfConquer.Core.World;

internal partial class LegacyNativeMobItemGenerator
{
	public List<uint> GenerateSoulsItems(ushort level)
	{
		List<uint> list = new List<uint>();
		ushort num = (ushort)(LegacyNativePoolRandom.GetRandom.Next() % 1000);
		byte b = (byte)(num % 3);
		if (LegacyNativeItemTypeRuneMetadata.PurificationItems.ContainsKey(level))
		{
			LegacyNativeItemRuneMetadata[] array = LegacyNativeItemTypeRuneMetadata.PurificationItems[level].Values.ToArray();
			for (int i = 0; i < ((b == 0) ? 1 : b); i++)
			{
				int num2 = LegacyNativePoolRandom.GetRandom.Next(0, array.Length);
				list.Add(array[num2].ID);
			}
		}
		LegacyNativeItemRuneMetadata[] array2 = LegacyNativeItemTypeRuneMetadata.Accessorys.Values.ToArray();
		num = (ushort)(LegacyNativePoolRandom.GetRandom.Next() % 1000);
		b = (byte)(num % 3);
		for (int j = 0; j < b; j++)
		{
			int num3 = LegacyNativePoolRandom.GetRandom.Next(0, array2.Length);
			list.Add(array2[num3].ID);
		}
		if (level <= 3)
		{
			list.Add(3001044u);
		}
		else if (level > 3)
		{
			list.Add(729463u);
		}
		return list;
	}

	public List<uint> GenerateBossFamily()
	{
		List<uint> list = new List<uint>();
		byte b = (byte)LegacyNativePoolRandom.GetRandom.Next(1, 7);
		for (int num = 0; num < 4; num++)
		{
			byte b2 = GenerateQuality();
			uint num2 = 0u;
			uint dwItemLev = 0u;
			switch (b)
			{
			}
			dwItemLev = AlterItemLevel(dwItemLev, num2);
			uint num3 = num2 * 1000 + dwItemLev * 10 + b2;
			if (LegacyNativePoolItems.ItemsBase!.ContainsKey(num3))
			{
				list.Add(num3);
			}
		}
		return list;
	}
}
