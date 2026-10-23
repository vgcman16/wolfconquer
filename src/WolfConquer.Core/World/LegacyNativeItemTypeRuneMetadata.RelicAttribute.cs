using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyNativeItemTypeRuneMetadata
{
    // Whole native ItemType1014..1058. Real table filtering/redraw every iteration;
    // no precomputed whitelist, retry bound, None filter or missing-catalog guard.
	public static LegacyNativeChiAttribute RelicAttribute(uint ID)
	{
		LegacyNativeChiAttribute chiAttribute;
		while (true)
		{
			chiAttribute = LegacyNativeChiAttribute.None;
			LegacyNativeRuneTable.RandomAttribute[] array = LegacyNativeRuneTable.RandomAttributes.Where((LegacyNativeRuneTable.RandomAttribute i) => i.ItemID == ID).ToArray();
			chiAttribute = array[LegacyNativePoolRandom.GetRandom.Next(0, array.Count())].Attribute;
			switch (ID)
			{
			case 4100001u:
				if (chiAttribute == LegacyNativeChiAttribute.SkillCriticalStrike || chiAttribute == LegacyNativeChiAttribute.CriticalStrike || chiAttribute == LegacyNativeChiAttribute.HPAdd || chiAttribute == LegacyNativeChiAttribute.AddAttack)
				{
					continue;
				}
				break;
			case 4100002u:
				if (chiAttribute == LegacyNativeChiAttribute.SkillCriticalStrike || chiAttribute == LegacyNativeChiAttribute.CriticalStrike || chiAttribute == LegacyNativeChiAttribute.AddMagicAttack || chiAttribute == LegacyNativeChiAttribute.AddAttack)
				{
					continue;
				}
				break;
			case 4100003u:
				if (chiAttribute == LegacyNativeChiAttribute.HPAdd || chiAttribute == LegacyNativeChiAttribute.CriticalStrike || chiAttribute == LegacyNativeChiAttribute.AddMagicAttack || chiAttribute == LegacyNativeChiAttribute.AddAttack)
				{
					continue;
				}
				break;
			case 4100004u:
				if (chiAttribute == LegacyNativeChiAttribute.HPAdd || chiAttribute == LegacyNativeChiAttribute.CriticalStrike || chiAttribute == LegacyNativeChiAttribute.AddMagicAttack || chiAttribute == LegacyNativeChiAttribute.SkillCriticalStrike)
				{
					continue;
				}
				break;
			case 4100005u:
				if (chiAttribute == LegacyNativeChiAttribute.HPAdd || chiAttribute == LegacyNativeChiAttribute.AddAttack || chiAttribute == LegacyNativeChiAttribute.AddMagicAttack || chiAttribute == LegacyNativeChiAttribute.SkillCriticalStrike)
				{
					continue;
				}
				break;
			}
			break;
		}
		return chiAttribute;
	}
}
