using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu
{
	public static class JiangHuRanking
	{
		private static ConcurrentDictionary<uint, LegacyNativeJiangHu> TopRank = new ConcurrentDictionary<uint, LegacyNativeJiangHu>();

		public static LegacyNativeJiangHu[]? TopRank100 = null;

		private static object SyncRoot = new object();

		public static void UpdateRank(LegacyNativeJiangHu jiang)
		{
			lock (SyncRoot)
			{
				if (!TopRank.ContainsKey(jiang.UID))
				{
					TopRank.TryAdd(jiang.UID, jiang);
				}
				CalculateRanks();
			}
		}

		private static void CalculateRanks()
		{
			foreach (LegacyNativeJiangHu value in TopRank.Values)
			{
				value.Rank = 0;
			}
			LegacyNativeJiangHu[] source = TopRank.Values.ToArray();
			IOrderedEnumerable<LegacyNativeJiangHu> orderedEnumerable = source.OrderByDescending((LegacyNativeJiangHu jiang) => jiang.Inner_Strength);
			List<LegacyNativeJiangHu> list = new List<LegacyNativeJiangHu>();
			byte b = 1;
			foreach (LegacyNativeJiangHu item in orderedEnumerable)
			{
				if (b == 101)
				{
					break;
				}
				item.Rank = b;
				list.Add(item);
				b++;
			}
			TopRank100 = list.ToArray();
			TopRank = new ConcurrentDictionary<uint, LegacyNativeJiangHu>();
			foreach (LegacyNativeJiangHu item2 in list)
			{
				TopRank.TryAdd(item2.UID, item2);
			}
			list = null!;
		}
	}
}
