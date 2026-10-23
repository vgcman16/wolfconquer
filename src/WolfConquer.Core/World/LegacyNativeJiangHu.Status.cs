using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu
{
	internal void CreateStatusAtributes(LegacyNativeGameClientRuntime? client)
	{
		if (!LegacyNativeServerConfig.IsInterServer && client != null && !client.OnInterServer)
		{
			statusclient = new LegacyNativeMsgStatus();
			uint inner_Strength = Inner_Strength;
			Inner_Strength = 0u;
			Stage[] arrayStages = ArrayStages;
			foreach (Stage stage in arrayStages)
			{
				if (!stage.Activate)
				{
					continue;
				}
				Stage.Star[] array = stage.ArrayStars.Where((Stage.Star p) => p.UID != 0).ToArray();
				byte b = 0;
				Dictionary<uint, List<Stage.Star>> dictionary = new Dictionary<uint, List<Stage.Star>>();
				List<Stage.Star> list = new List<Stage.Star>();
				ushort num = 0;
				for (byte b2 = 0; b2 < array.Length; b2++)
				{
					Stage.Star star = array[b2];
					b = 0;
					bool flag = false;
					byte b3 = (byte)(b2 + 1);
					while (b3 < array.Length)
					{
						Stage.Star star2 = array[b3];
						if (star2.Typ == star.Typ)
						{
							if (!flag && !dictionary.ContainsKey(num))
							{
								dictionary.Add(num, new List<Stage.Star>());
								dictionary[num].Add(star);
							}
							if (!dictionary.ContainsKey(num))
							{
								dictionary.Add(num, new List<Stage.Star>());
								dictionary[num].Add(star2);
							}
							else
							{
								dictionary[num].Add(star2);
							}
							flag = true;
							b2 = b3;
							b++;
							b3++;
							continue;
						}
						break;
					}
					uint num2 = 1u;
					if (b != 0)
					{
						num2 = (byte)(b + 1);
						num++;
					}
					if (num2 == 1)
					{
						list.Add(star);
					}
					b = 0;
				}
				foreach (List<Stage.Star> value in dictionary.Values)
				{
					byte b4 = (byte)LegacyNativeJianHuTable.AlignmentExtraPoints((byte)(value.Count - 1));
					ushort num3 = 0;
					Stage.AtributesType status = Stage.AtributesType.None;
					foreach (Stage.Star item in value)
					{
						LegacyNativeJianHuTable.Atribut atribut = LegacyNativeJianHuTable.Atributes[item.UID];
						// Finite UInt16 power + <=50% bonus fits UInt32; retain native low UInt16 bits.
						num3 = unchecked((ushort)(num3 + (ushort)(uint)((double)(int)atribut.Power + Math.Floor((double)(atribut.Power * b4 / 100)))));
						status = item.Typ;
					}
					if (client != null)
					{
						ComputeStatus(client, status, num3);
					}
					CalculateInner_StrengthAlignments(value);
				}
				foreach (Stage.Star item2 in list)
				{
					LegacyNativeJianHuTable.Atribut atribut2 = LegacyNativeJianHuTable.Atributes[item2.UID];
					if (client != null)
					{
						ComputeStatus(client, item2.Typ, atribut2.Power);
					}
					Inner_Strength += LegacyNativeJianHuTable.GetStatusPoints(item2.Level);
				}
			}
			if (inner_Strength != Inner_Strength)
			{
				JiangHuRanking.UpdateRank(this);
			}
		}
		if (client != null)
		{
			client.Status.Breakthrough += ((statusclient.Breakthrough > 810) ? 810 : statusclient.Breakthrough);
			client.Status.Counteraction += ((statusclient.Counteraction > 2430) ? 2430 : statusclient.Counteraction);
			client.Status.CriticalStrike += ((statusclient.CriticalStrike > 5400) ? 5400 : statusclient.CriticalStrike);
			client.Status.PhysicalDamageIncrease += statusclient.PhysicalDamageIncrease;
			client.Status.PhysicalDamageDecrease += statusclient.PhysicalDamageDecrease;
			client.Status.MagicDamageDecrease += statusclient.MagicDamageDecrease;
			client.Status.MagicDamageIncrease += statusclient.MagicDamageIncrease;
			client.Status.Immunity += ((statusclient.Immunity > 5400) ? 5400 : statusclient.Immunity);
			client.Status.MagicAttack += statusclient.MagicAttack;
			client.Status.MaxHitpoints += ((statusclient.MaxHitpoints > 12150) ? 12150 : statusclient.MaxHitpoints);
			client.Status.MaxMana += statusclient.MaxMana;
			client.Status.MagicDefence += statusclient.MagicDefence;
			client.Status.MagicDefence += statusclient.MagicDefence;
			client.Status.MaxAttack += ((statusclient.MaxAttack > 6480) ? 6480 : statusclient.MaxAttack);
			client.Status.MinAttack += ((statusclient.MinAttack > 6480) ? 6480 : statusclient.MinAttack);
			client.Status.Defence += ((statusclient.Defence > 4860) ? 4860 : statusclient.Defence);
			client.Status.SkillCStrike += ((statusclient.SkillCStrike > 5400) ? 5400 : statusclient.SkillCStrike);
		}
	}
}
