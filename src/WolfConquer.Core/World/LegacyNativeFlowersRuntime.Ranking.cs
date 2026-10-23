using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Flowers75..191; real owner/method family.
internal partial class LegacyNativeFlowersRuntime
{
	public class FlowerRanking
	{
		public const int File_Size = 100;

		public bool Girls = true;

		public object SynRoot;

		public Dictionary<uint, Flower> RedRoses;

		public Dictionary<uint, Flower> Lilies;

		public Dictionary<uint, Flower> Orchids;

		public Dictionary<uint, Flower> Tulips;

		public FlowerRanking(bool _Girls = true)
		{
			Girls = _Girls;
			SynRoot = new object();
			RedRoses = new Dictionary<uint, Flower>(100);
			Lilies = new Dictionary<uint, Flower>(100);
			Orchids = new Dictionary<uint, Flower>(100);
			Tulips = new Dictionary<uint, Flower>(100);
		}

		public void UpdateRank(Flower AllFlower, LegacyNativeMsgFlower.FlowersType Typ)
		{
			lock (SynRoot)
			{
				switch (Typ)
				{
				case LegacyNativeMsgFlower.FlowersType.Rouse:
					CreateRank(RedRoses, AllFlower);
					break;
				case LegacyNativeMsgFlower.FlowersType.Lilies:
					CreateRank(Lilies, AllFlower);
					break;
				case LegacyNativeMsgFlower.FlowersType.Orchids:
					CreateRank(Orchids, AllFlower);
					break;
				case LegacyNativeMsgFlower.FlowersType.Tulips:
					CreateRank(Tulips, AllFlower);
					break;
				}
			}
		}

		public void CreateRank(Dictionary<uint, Flower> AllRank, Flower AllFlower)
		{
			if (AllRank.Count < 100)
			{
				Calculate(AllRank, AllFlower);
				return;
			}
			Flower[] array = AllRank.Values.ToArray();
			if (array[99].Amount <= AllFlower.Amount)
			{
				Calculate(AllRank, AllFlower);
			}
			else if (AllRank.ContainsKey(AllFlower.UID))
			{
				Calculate(AllRank, AllFlower);
			}
		}

		public void Calculate(Dictionary<uint, Flower> FlowerRank, Flower Flower)
		{
			lock (FlowerRank)
			{
				if (!FlowerRank.ContainsKey(Flower.UID))
				{
					FlowerRank.Add(Flower.UID, Flower);
				}
				Flower[] source = FlowerRank.Values.ToArray();
				Flower[] array = source.OrderByDescending((Flower p) => p.Amount).ToArray();
				int num = 1;
				FlowerRank.Clear();
				Flower[] array2 = array;
				foreach (Flower flower in array2)
				{
					int rank = flower.Rank;
					flower.Rank = num;
					if (flower.UID == Flower.UID)
					{
						Flower.Rank = num;
					}
					if (num <= 100 && FlowerRank.Count < 100)
					{
						if (!FlowerRank.ContainsKey(flower.UID))
						{
							FlowerRank.Add(flower.UID, flower);
						}
					}
					else
					{
						num = 0;
						Flower.Rank = 0;
						flower.Rank = 0;
					}
					if (rank != flower.Rank && Girls)
					{
						UpdateRank(Flower.UID, Flower);
					}
					num++;
				}
			}
		}

		public void UpdateRank(uint UID, Flower Flower)
		{
			if (LegacyNativePoolClients.GamePoll.TryGetValue(UID, out var value))
			{
				value.Player.Flowers.UpdateMyRank(value, ReloadScreen: true);
			}
		}
	}
}
