using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu
{
	internal ushort ValueToRoll(Stage.AtributesType status, byte level)
	{
		return (ushort)((ushort)status + level * 256);
	}

	internal byte GetValueLevel(ushort val)
	{
		return (byte)((val - (ushort)(val % 256)) / 256);
	}

	internal Stage.AtributesType GetValueType(uint val)
	{
		return (Stage.AtributesType)(val % 256);
	}

	internal byte CreateStarLevel(byte tryingultra)
	{
		byte b = (byte)Rand.Next((tryingultra <= 0) ? 1 : 3, 7);
		if (tryingultra > 0)
		{
			tryingultra--;
		}
		if (LegacyNativeRoleCore.Rate(1) && LegacyNativeRoleCore.Rate(1) && b >= 6)
		{
			b = (byte)Rand.Next(1, 20);
			if (b > 6)
			{
				b = 6;
			}
		}
		return b;
	}

	internal byte GetGrade()
	{
		int num = ArrayStages.Where((Stage p) => p.Activate).Count();
		if (num >= 9)
		{
			if (ArrayStages.Where((Stage p) => p.ArrayStars.Where((Stage.Star c) => c.Level >= 1).Count() == 9).Count() == 9)
			{
				num++;
			}
			if (ArrayStages.Where((Stage p) => p.ArrayStars.Where((Stage.Star c) => c.Level >= 2).Count() == 9).Count() == 9)
			{
				num++;
			}
			if (ArrayStages.Where((Stage p) => p.ArrayStars.Where((Stage.Star c) => c.Level >= 3).Count() == 9).Count() == 9)
			{
				num++;
			}
			if (ArrayStages.Where((Stage p) => p.ArrayStars.Where((Stage.Star c) => c.Level >= 4).Count() == 9).Count() == 9)
			{
				num++;
			}
			if (ArrayStages.Where((Stage p) => p.ArrayStars.Where((Stage.Star c) => c.Level >= 5).Count() == 9).Count() == 9)
			{
				num++;
			}
			if (ArrayStages.Where((Stage p) => p.ArrayStars.Where((Stage.Star c) => c.Level >= 6).Count() == 9).Count() == 9)
			{
				num++;
			}
		}
		return (byte)num;
	}
}
