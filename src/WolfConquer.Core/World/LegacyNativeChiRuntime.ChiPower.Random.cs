using System.Linq;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeChiRuntime
{
	public partial class ChiPower
	{
		public void Reroll(LegacyNativeMsgChiInfo.LockedFlags Locked)
		{
			int x;
			for (x = 0; x < Attributes.Length; x++)
			{
				if (((uint)Locked & (uint)(byte)(1 << x)) != 0)
				{
					Attributes[x].Locked = true;
					continue;
				}
				Attributes[x].Type = (LegacyNativeChiAttribute)Rand.Next(1, 13);
				do
				{
					Attributes[x].Type = (LegacyNativeChiAttribute)Rand.Next(1, 13);
				}
				while (Attributes.Where((ChiAttribute i) => i.Type == Attributes[x].Type).Count() > 1);
				Attributes[x].Value = StatsRand(Attributes[x]);
				int num = ChiMaxValues(Attributes[x].Type);
				if (100 * Attributes[x].Value / num >= 90)
				{
					using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
					LegacyNativePacket stream = recycledPacket.GetStream();
					stream.ChiMessageCreate(Type, Attributes[x], Name);
					LegacyNativeServerBroadcast.SendGlobalPacket(stream);
				}
				Attributes[x].Locked = false;
			}
		}

		public ushort StatsRand(ChiAttribute attr, bool epic = true)
		{
			int num = 0;
			int num2 = ChiMaxValues(attr.Type);
			int lowerBound = ChiMinValues(attr.Type);
			if (epic)
			{
				return (ushort)num2;
			}
			do
			{
				num = (ushort)Rand.Next(lowerBound, num2);
				if ((ChiAttributes != null && !LegacyNativeRoleCore.Rate(ChiAttributes.StudyLuck / 100)) || ChiAttributes == null)
				{
					while ((100 * num / num2 >= 70 && LegacyNativeRoleCore.Rate(60)) || (100 * num / num2 >= 90 && LegacyNativeRoleCore.Rate(95)))
					{
						num = (ushort)Rand.Next(lowerBound, num2);
					}
				}
			}
			while (ChiAttributes != null && 100.0 * (double)num / (double)num2 < (double)ChiAttributes.MinScore);
			return (ushort)num;
		}
	}
}
