using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Flowers15..73; real owner/method family.
internal partial class LegacyNativeFlowersRuntime
{
	public class FlowersRankingToday
	{
		public const int File_Size = 30;

		public Dictionary<uint, uint> Flowers;

		public FlowersRankingToday()
		{
			Flowers = new Dictionary<uint, uint>();
		}

		public void UpdateRank(uint UID, uint Amount)
		{
			if (Flowers.Count < 30)
			{
				Calculate(UID, Amount);
				return;
			}
			uint[] array = Flowers.Values.ToArray();
			if (array[29] <= Amount)
			{
				Calculate(UID, Amount);
			}
			else if (Flowers.ContainsKey(UID))
			{
				Calculate(UID, Amount);
			}
		}

		public void Calculate(uint UID, uint Amount)
		{
			lock (Flowers)
			{
				if (!Flowers.ContainsKey(UID))
				{
					Flowers.Add(UID, Amount);
				}
				uint[] array = Flowers.Values.ToArray();
				KeyValuePair<uint, uint>[] array2 = Flowers.OrderByDescending((KeyValuePair<uint, uint> p) => p.Value).ToArray();
				int num = 1;
				Flowers.Clear();
				KeyValuePair<uint, uint>[] array3 = array2;
				for (int num2 = 0; num2 < array3.Length; num2++)
				{
					KeyValuePair<uint, uint> keyValuePair = array3[num2];
					if (num <= 30 && Flowers.Count < 30)
					{
						if (!Flowers.ContainsKey(keyValuePair.Key))
						{
							Flowers.Add(keyValuePair.Key, keyValuePair.Value);
						}
						num++;
						continue;
					}
					break;
				}
			}
		}
	}
}
