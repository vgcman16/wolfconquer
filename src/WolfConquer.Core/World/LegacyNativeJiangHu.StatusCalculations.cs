using System.Collections;
using System.Collections.Concurrent;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeJiangHu
{
	internal void CalculateInner_StrengthAlignments(List<Stage.Star> collection)
	{
		ushort num = 0;
		for (int i = 0; i < collection.Count; i++)
		{
			num = unchecked((ushort)(num + LegacyNativeJianHuTable.GetStatusPoints(collection[i].Level)));
		}
		if (collection.Count > 0)
		{
			Inner_Strength += (uint)(num + num * (GetPercentALignment(collection.Count) * 10) / 100);
		}
	}

	internal byte GetPercentALignment(int count)
	{
		if (count < 8)
		{
			return (byte)(count - 1);
		}
		if (count == 8)
		{
			return (byte)count;
		}
		return (byte)(count + 1);
	}

	internal void ComputeStatus(LegacyNativeGameClientRuntime client, Stage.AtributesType status, ushort Power)
	{
		switch (status)
		{
		case Stage.AtributesType.Breakthrough:
			statusclient.Breakthrough += Power;
			break;
		case Stage.AtributesType.Counteraction:
			statusclient.Counteraction += Power;
			break;
		case Stage.AtributesType.CriticalStrike:
			statusclient.CriticalStrike += Power;
			break;
		case Stage.AtributesType.FinalAttack:
			statusclient.PhysicalDamageIncrease += Power;
			break;
		case Stage.AtributesType.FinalDefense:
			statusclient.PhysicalDamageDecrease += Power;
			break;
		case Stage.AtributesType.FinalMagicAttack:
			statusclient.MagicDamageIncrease += Power;
			break;
		case Stage.AtributesType.FinalMagicDefense:
			statusclient.MagicDamageDecrease += Power;
			break;
		case Stage.AtributesType.Immunity:
			statusclient.Immunity += Power;
			break;
		case Stage.AtributesType.MAttack:
			statusclient.MagicAttack += Power;
			break;
		case Stage.AtributesType.MaxLife:
			statusclient.MaxHitpoints += Power;
			break;
		case Stage.AtributesType.MaxMana:
			statusclient.MaxMana += Power;
			break;
		case Stage.AtributesType.Mdefense:
			statusclient.MagicDefence += Power;
			break;
		case Stage.AtributesType.PAttack:
			statusclient.MaxAttack += Power;
			statusclient.MinAttack += Power;
			break;
		case Stage.AtributesType.PDefense:
			statusclient.Defence += Power;
			break;
		case Stage.AtributesType.SkillCriticalStrike:
			statusclient.SkillCStrike += Power;
			break;
		}
	}
}
