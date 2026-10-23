using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Flowers307..354; real owner/method family.
internal partial class LegacyNativeFlowersRuntime
{
	public void UpdateMyRank(LegacyNativeGameClientRuntime user, bool ReloadScreen = false)
	{
		using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
		LegacyNativePacket stream = recycledPacket.GetStream();
		stream.GenericRankingCreate(LegacyNativeMsgGenericRanking.Action.InformationRequest, LegacyNativeMsgGenericRanking.RankType.None, 0, 0, 1, 0);
		user.Send(stream.GenericRankingFinalize());
		Flower[] array = this.Where((Flower f1) => f1.Rank >= 1 && f1.Rank <= 100).ToArray();
		Array.Sort(array, delegate(Flower f1, Flower f2)
		{
			int num3 = f1.Rank.CompareTo(f2.Rank);
			return (f2.Rank == f1.Rank) ? f2.GetGrade().CompareTo(f1.GetGrade()) : num3;
		});
		if (array.Length != 0)
		{
			user.Player.FlowerRank = CreateFlowerIcon(array[0], Today: false, 0);
			Flower[] array2 = array;
			foreach (Flower flower in array2)
			{
				stream.GenericRankingCreate(LegacyNativeMsgGenericRanking.Action.QueryCount, (LegacyNativeMsgGenericRanking.RankType)user.Player.Flowers.CreateFlowerIcon(flower, Today: false, 0), 0, 0, 1, 0);
				stream.AddItemGenericRankingCreate(flower.Rank, flower.Amount, flower.UID, flower.Name);
				user.Send(stream.GenericRankingFinalize());
			}
		}
		else if (LegacyNativePoolFlowers.FlowersRankToday.Flowers.ContainsKey(user.Player.UID))
		{
			Dictionary<uint, uint> flowers = LegacyNativePoolFlowers.FlowersRankToday.Flowers;
			int num2 = 1;
			foreach (KeyValuePair<uint, uint> item in flowers)
			{
				if (item.Key == user.Player.UID)
				{
					user.Player.FlowerRank = CreateFlowerIcon(null, Today: true, (byte)num2);
					stream.GenericRankingCreate(LegacyNativeMsgGenericRanking.Action.QueryCount, (LegacyNativeMsgGenericRanking.RankType)CreateFlowerIcon(null, Today: true, (byte)num2), 0, 0, 1, 0);
					stream.AddItemGenericRankingCreate(num2, item.Value, user.Player.UID, user.Player.Name);
					user.Send(stream.GenericRankingFinalize());
					break;
				}
				num2++;
			}
		}
		stream.GenericRankingCreate(LegacyNativeMsgGenericRanking.Action.InformationRequest, LegacyNativeMsgGenericRanking.RankType.None, 0, 0, 0, 0);
		stream.AddItemGenericRankingCreate(0, 0u, 0u, "");
		user.Send(stream.GenericRankingFinalize());
		if (ReloadScreen)
		{
			user.Player.View.SendView(user.Player.GetArray(stream, WindowsView: false, st: false), me: false);
		}
	}
}
