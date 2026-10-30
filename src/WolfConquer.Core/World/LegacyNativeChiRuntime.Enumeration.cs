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
		ArgumentNullException.ThrowIfNull(user);
		ArgumentNullException.ThrowIfNull(MyPower);
		ArgumentNullException.ThrowIfNull(stream);
		var type = MyPower.Type;
		int rank = MyPower.Rank;
		uint score = (uint)MyPower.Score;
		uint uid = MyPower.UID;
		string name = MyPower.Name;
		ArgumentNullException.ThrowIfNull(name);
		stream.PrepareFrame(2165, prepared =>
		{
			prepared.GenericRankingCreate(LegacyNativeMsgGenericRanking.Action.QueryCount,
				(LegacyNativeMsgGenericRanking.RankType)(60000000u + (uint)type), 0, 0, 1, 0);
			prepared.AddItemGenericRankingCreate(rank, score, uid, name);
		});
		user.Send(stream);
	}
}
