using System;
using System.Collections.Generic;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal class LegacyNativeRefinaryBoxes : Dictionary<uint, LegacyNativeRefinaryBoxes.Boxe>
{
	public class Boxe
	{
		public uint Identifier;

		public uint Position;

		public bool Untradable;

		public LegacyNativeRifinery.RefineryType Type;
	}

	public LegacyNativeRefinaryBoxes()
	{
		using LegacyNativeDbRead read = new LegacyNativeDbRead("databaserefineryboxes.txt");
		if (read.Reader())
		{
			for (int i = 0; i < read.Count; i++)
			{
				LegacyNativeDbReadLine readLine = new LegacyNativeDbReadLine(read.ReadString("0,0"), ',');
				Boxe boxe = new Boxe
				{
					Identifier = readLine.Read(0u),
					Position = readLine.Read(0u),
					Type = (LegacyNativeRifinery.RefineryType)readLine.Read((byte)0),
					Untradable = (readLine.Read((byte)0) == 1)
				};
				Add(boxe.Identifier, boxe);
			}
		}
	}

	public uint GainRefineryItem(uint ID)
	{
		Boxe? value = null;
		if (TryGetValue(ID, out value))
		{
			List<LegacyNativeRifinery.Item> list = new List<LegacyNativeRifinery.Item>();
			foreach (LegacyNativeRifinery.Item value2 in LegacyNativePoolItems.RifineryItems!.Values)
			{
				if (value2.Type != value!.Type || value2.ForItemPosition != value!.Position)
				{
					continue;
				}
				if ((value!.Identifier >= 3004197 && value!.Identifier <= 3004226) || (value!.Identifier >= 3004266 && value!.Identifier <= 3004280))
				{
					if (value2.Level > 3)
					{
						list.Add(value2);
					}
				}
				else if (value2.Level < 6)
				{
					list.Add(value2);
				}
			}
			if (list.Count > 0)
			{
				Random random = new Random();
				int index = random.Next(1, list.Count);
				LegacyNativeRifinery.Item item = list[index];
				if (item != null)
				{
					return item.ItemID;
				}
			}
		}
		return 0u;
	}
}
