using System.Collections;
using System.Collections.Generic;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeChiRuntime
{
	public LegacyNativeChiRuntime(uint _UID)
	{
		UID = _UID;
		ChiPoints = 4000;
		Dragon = new ChiPower(LegacyNativeMsgChiInfo.ChiPowerType.Dragon);
		Phoenix = new ChiPower(LegacyNativeMsgChiInfo.ChiPowerType.Phoenix);
		Turtle = new ChiPower(LegacyNativeMsgChiInfo.ChiPowerType.Turtle);
		Tiger = new ChiPower(LegacyNativeMsgChiInfo.ChiPowerType.Tiger);
	}

	public uint AllScore()
	{
		uint num = 0u;
		using (IEnumerator<ChiPower> enumerator = GetEnumerator())
		{
			while (enumerator.MoveNext())
			{
				ChiPower current = enumerator.Current;
				num += (uint)current.Score;
			}
		}
		return num;
	}

	public IEnumerator<ChiPower> GetEnumerator()
	{
		yield return Dragon;
		yield return Phoenix;
		yield return Tiger;
		yield return Turtle;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return GetEnumerator();
	}

	public void SendQueryUpdate(LegacyNativeGameClientRuntime user, ChiPower MyPower, LegacyNativePacket stream)
	{
		stream.GenericRankingCreate(LegacyNativeMsgGenericRanking.Action.QueryCount, (LegacyNativeMsgGenericRanking.RankType)(60000000u + (uint)MyPower.Type), 0, 0, 1, 0);
		stream.AddItemGenericRankingCreate(MyPower.Rank, (uint)MyPower.Score, MyPower.UID, MyPower.Name);
		user.Send(stream.GenericRankingFinalize());
	}
}
