using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Whole native Flowers193..237; real owner/method family.
internal partial class LegacyNativeFlowersRuntime
{
	public class Flower
	{
		public LegacyNativeMsgFlower.FlowersType Type;

		public uint Amount;

		public uint Amount2day;

		public int Rank;

		public string Name;

		public uint UID;

		public Flower(LegacyNativeMsgFlower.FlowersType typ, uint _uid, string _name)
		{
			UID = _uid;
			Name = _name;
			Type = typ;
		}

		public byte GetGrade()
		{
			return Type switch
			{
				LegacyNativeMsgFlower.FlowersType.Rouse => 1,
				LegacyNativeMsgFlower.FlowersType.Tulips => 2,
				LegacyNativeMsgFlower.FlowersType.Orchids => 3,
				LegacyNativeMsgFlower.FlowersType.Lilies => 4,
				_ => 0,
			};
		}

		public static implicit operator uint(Flower Data)
		{
			return Data.Amount;
		}

		public static Flower operator +(Flower Data, uint amount)
		{
			Data.Amount += amount;
			Data.Amount2day += amount;
			return Data;
		}
	}
}
