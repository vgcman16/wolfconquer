using System.Linq;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRankItems
{
    // Whole native RankItems.Rank, actual rank array alias and native update quirks.
	public class Rank
	{
		public const int MaxItems = 50;

		public LegacyNativeDbSafeDictionary<uint, LegacyNativeGameItemRuntime> Items = new LegacyNativeDbSafeDictionary<uint, LegacyNativeGameItemRuntime>();

		public object SynRoot = new object();

		private LegacyNativeGameItemRuntime[] Top50 = new LegacyNativeGameItemRuntime[0];

		public uint GetItemRank(uint UID)
		{
			if (Items.TryGetValue(UID, out var value))
			{
				return value.PerfectionRank;
			}
			return 100u;
		}

		public LegacyNativeGameItemRuntime[] GetRank50Items()
		{
			lock (Top50)
			{
				return Top50;
			}
		}

		public void AddItem(LegacyNativeGameItemRuntime item)
		{
			if (Items.Count < 50)
			{
				if (!Items.ContainsKey(item.Uid))
				{
					Items.Add(item.Uid, item);
					lock (SynRoot)
					{
						Top50 = Items.Values.OrderByDescending((LegacyNativeGameItemRuntime p) => p.ItemPoints).ToArray();
						for (int num = 0; num < Top50.Length; num++)
						{
							Top50[num].PerfectionRank = (ushort)(num + 1);
						}
						return;
					}
				}
				if (!Items.ContainsKey(item.Uid))
				{
					return;
				}
				uint position = Items[item.Uid].Position;
				ushort perfectionRank = Items[item.Uid].PerfectionRank;
				Items[item.Uid] = item;
				item.PerfectionRank = perfectionRank;
				if (position == item.ItemPoints)
				{
					return;
				}
				lock (SynRoot)
				{
					Top50 = Items.Values.OrderByDescending((LegacyNativeGameItemRuntime p) => p.ItemPoints).ToArray();
					for (int num2 = 0; num2 < Top50.Length; num2++)
					{
						Top50[num2].PerfectionRank = (ushort)(num2 + 1);
					}
					return;
				}
			}
			if (Items.ContainsKey(item.Uid))
			{
				ushort perfectionRank2 = Items[item.Uid].PerfectionRank;
				Items[item.Uid] = item;
				item.PerfectionRank = perfectionRank2;
				return;
			}
			LegacyNativeGameItemRuntime msgGameItem = Top50[Top50.Length - 1];
			if (item.ItemPoints <= msgGameItem.ItemPoints)
			{
				return;
			}
			Items.Remove(msgGameItem.Uid);
			Items.Add(item.Uid, item);
			lock (SynRoot)
			{
				Top50 = Items.Values.OrderByDescending((LegacyNativeGameItemRuntime p) => p.ItemPoints).ToArray();
				for (int num3 = 0; num3 < Top50.Length; num3++)
				{
					Top50[num3].PerfectionRank = (ushort)(num3 + 1);
				}
			}
		}
	}

}
