using System.Collections.Generic;
using System.Linq;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeChiRuntime
{
	public static void ComputeStatus(LegacyNativeChiRuntime ClientChi)
	{
		lock (SynRoot)
		{
			ClientChi.CriticalStrike = 0u;
			ClientChi.SkillCriticalStrike = 0u;
			ClientChi.Immunity = 0u;
			ClientChi.Breakthrough = 0u;
			ClientChi.Counteraction = 0u;
			ClientChi.MaxLife = 0u;
			ClientChi.AddAttack = 0u;
			ClientChi.AddMagicAttack = 0u;
			ClientChi.AddMagicDefense = 0u;
			ClientChi.FinalAttack = 0u;
			ClientChi.FinalDefense = 0u;
			ClientChi.FinalMagicAttack = 0u;
			ClientChi.FinalMagicDefense = 0u;
			foreach (ChiPower item in ClientChi)
			{
				if (!item.UnLocked)
				{
					continue;
				}
				IEnumerable<ChiAttribute> enumerable = item.Attributes.Concat((item.ChiAttributes == null) ? new List<ChiAttribute>().ToArray() : item.ChiAttributes.Attributes!);
				foreach (ChiAttribute item2 in enumerable)
				{
					int value = item2.Value;
					switch (item2.Type)
					{
					case LegacyNativeChiAttribute.AddAttack:
						ClientChi.AddAttack += item2.Value;
						break;
					case LegacyNativeChiAttribute.AddMagicAttack:
						ClientChi.AddMagicAttack += item2.Value;
						break;
					case LegacyNativeChiAttribute.AddMagicDefense:
						ClientChi.AddMagicDefense += item2.Value;
						break;
					case LegacyNativeChiAttribute.Breakthrough:
						ClientChi.Breakthrough += item2.Value;
						break;
					case LegacyNativeChiAttribute.Counteraction:
						ClientChi.Counteraction += item2.Value;
						break;
					case LegacyNativeChiAttribute.CriticalStrike:
						ClientChi.CriticalStrike += (uint)(item2.Value * 10);
						break;
					case LegacyNativeChiAttribute.PhysicalDamageIncrease:
						ClientChi.FinalAttack += item2.Value;
						break;
					case LegacyNativeChiAttribute.PhysicalDamageDecrease:
						ClientChi.FinalDefense += item2.Value;
						break;
					case LegacyNativeChiAttribute.MagicDamageIncrease:
						ClientChi.FinalMagicAttack += item2.Value;
						break;
					case LegacyNativeChiAttribute.MagicDamageDecrease:
						ClientChi.FinalMagicDefense += item2.Value;
						break;
					case LegacyNativeChiAttribute.Immunity:
						ClientChi.Immunity += (uint)(item2.Value * 10);
						break;
					case LegacyNativeChiAttribute.HPAdd:
						ClientChi.MaxLife += item2.Value;
						break;
					case LegacyNativeChiAttribute.SkillCriticalStrike:
						ClientChi.SkillCriticalStrike += (uint)(item2.Value * 10);
						break;
					}
				}
				if (item.Type == LegacyNativeMsgChiInfo.ChiPowerType.Dragon)
				{
					if ((item.Rank >= 1 && item.Rank <= 3) || item.Score == 400)
					{
						ClientChi.MaxLife += 5000u;
						ClientChi.FinalAttack += 1000u;
						ClientChi.FinalMagicAttack += 300u;
						ClientChi.FinalMagicDefense += 300u;
					}
					else if (item.Rank >= 4 && item.Rank <= 20)
					{
						ClientChi.MaxLife += 3000 - (uint)((double)(item.Rank - 4) * 62.5);
						ClientChi.FinalAttack += 600 - (uint)((double)(item.Rank - 4) * 12.5);
						ClientChi.FinalMagicAttack += (uint)(200 - (item.Rank - 4) * 3);
						ClientChi.FinalMagicDefense += (uint)(200 - (item.Rank - 4) * 3);
					}
					else if (item.Rank >= 21 && item.Rank <= 50)
					{
						ClientChi.MaxLife += 1500u;
						ClientChi.FinalAttack += 300u;
						ClientChi.FinalMagicAttack += 100u;
						ClientChi.FinalMagicDefense += 100u;
					}
				}
				else if (item.Type == LegacyNativeMsgChiInfo.ChiPowerType.Phoenix)
				{
					if ((item.Rank >= 1 && item.Rank <= 3) || item.Score == 400)
					{
						ClientChi.AddAttack += 3000u;
						ClientChi.AddMagicAttack += 3000u;
						ClientChi.FinalAttack += 1000u;
						ClientChi.FinalMagicAttack += 300u;
					}
					else if (item.Rank >= 4 && item.Rank <= 20)
					{
						ClientChi.AddAttack += 2000 - (uint)((double)(item.Rank - 4) * 31.5);
						ClientChi.AddMagicAttack += 2000 - (uint)((double)(item.Rank - 4) * 31.5);
						ClientChi.FinalAttack += 600 - (uint)((double)(item.Rank - 4) * 12.5);
						ClientChi.FinalMagicAttack += (uint)(200 - (item.Rank - 4) * 3);
					}
					else if (item.Rank >= 21 && item.Rank <= 50)
					{
						ClientChi.AddAttack += 1000u;
						ClientChi.AddMagicAttack += 2000u;
						ClientChi.FinalAttack += 1000u;
						ClientChi.FinalMagicAttack += 300u;
					}
				}
				else if (item.Type == LegacyNativeMsgChiInfo.ChiPowerType.Tiger)
				{
					if ((item.Rank >= 1 && item.Rank <= 3) || item.Score == 400)
					{
						ClientChi.CriticalStrike += 1500u;
						ClientChi.SkillCriticalStrike += 1500u;
						ClientChi.Immunity += 800u;
					}
					else if (item.Rank >= 4 && item.Rank <= 20)
					{
						ClientChi.CriticalStrike += (uint)(1100 - (item.Rank - 4) * 17);
						ClientChi.SkillCriticalStrike += (uint)(1100 - (item.Rank - 4) * 17);
						ClientChi.Immunity += (uint)(500 - (item.Rank - 4) * 13);
					}
					else if (item.Rank >= 21 && item.Rank <= 50)
					{
						ClientChi.CriticalStrike += 500u;
						ClientChi.SkillCriticalStrike += 500u;
						ClientChi.Immunity += 200u;
					}
				}
				else if (item.Type == LegacyNativeMsgChiInfo.ChiPowerType.Turtle)
				{
					if ((item.Rank >= 1 && item.Rank <= 3) || item.Score == 400)
					{
						ClientChi.Breakthrough += 150u;
						ClientChi.Counteraction += 150u;
						ClientChi.Immunity += 800u;
					}
					else if (item.Rank >= 4 && item.Rank <= 20)
					{
						ClientChi.Breakthrough += (uint)(1100 - (item.Rank - 4) * 17) / 10u;
						ClientChi.Counteraction += (uint)(1100 - (item.Rank - 4) * 17) / 10u;
						ClientChi.Immunity += (uint)(500 - (item.Rank - 4) * 13);
					}
					else if (item.Rank >= 21 && item.Rank <= 50)
					{
						ClientChi.Breakthrough += 50u;
						ClientChi.Counteraction += 50u;
						ClientChi.Immunity += 200u;
					}
				}
			}
		}
	}
}
