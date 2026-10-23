namespace WolfConquer.Core.World;

internal partial class LegacyNativeChiRuntime
{
	public static int ChiMaxValues(LegacyNativeChiAttribute attribute)
	{
		return attribute switch
		{
			LegacyNativeChiAttribute.CriticalStrike => 200,
			LegacyNativeChiAttribute.SkillCriticalStrike => 200,
			LegacyNativeChiAttribute.Immunity => 200,
			LegacyNativeChiAttribute.Breakthrough => 200,
			LegacyNativeChiAttribute.Counteraction => 200,
			LegacyNativeChiAttribute.HPAdd => 3500,
			LegacyNativeChiAttribute.AddAttack => 2000,
			LegacyNativeChiAttribute.AddMagicAttack => 2500,
			LegacyNativeChiAttribute.AddMagicDefense => 250,
			LegacyNativeChiAttribute.PhysicalDamageIncrease => 500,
			LegacyNativeChiAttribute.PhysicalDamageDecrease => 500,
			LegacyNativeChiAttribute.MagicDamageIncrease => 300,
			LegacyNativeChiAttribute.MagicDamageDecrease => 300,
			_ => 100,
		};
	}

	public static int ChiMedValues(LegacyNativeChiAttribute attribute)
	{
		return attribute switch
		{
			LegacyNativeChiAttribute.CriticalStrike => 185,
			LegacyNativeChiAttribute.SkillCriticalStrike => 185,
			LegacyNativeChiAttribute.Immunity => 185,
			LegacyNativeChiAttribute.Breakthrough => 185,
			LegacyNativeChiAttribute.Counteraction => 185,
			LegacyNativeChiAttribute.HPAdd => 3250,
			LegacyNativeChiAttribute.AddAttack => 1900,
			LegacyNativeChiAttribute.AddMagicAttack => 2200,
			LegacyNativeChiAttribute.AddMagicDefense => 200,
			LegacyNativeChiAttribute.PhysicalDamageIncrease => 450,
			LegacyNativeChiAttribute.PhysicalDamageDecrease => 450,
			LegacyNativeChiAttribute.MagicDamageIncrease => 250,
			LegacyNativeChiAttribute.MagicDamageDecrease => 250,
			_ => 100,
		};
	}

	public static int ChiMinValues(LegacyNativeChiAttribute attribute)
	{
		return attribute switch
		{
			LegacyNativeChiAttribute.CriticalStrike => 10,
			LegacyNativeChiAttribute.SkillCriticalStrike => 10,
			LegacyNativeChiAttribute.Immunity => 10,
			LegacyNativeChiAttribute.Breakthrough => 10,
			LegacyNativeChiAttribute.Counteraction => 10,
			LegacyNativeChiAttribute.HPAdd => 1000,
			LegacyNativeChiAttribute.AddAttack => 500,
			LegacyNativeChiAttribute.AddMagicAttack => 500,
			LegacyNativeChiAttribute.AddMagicDefense => 50,
			LegacyNativeChiAttribute.PhysicalDamageIncrease => 50,
			LegacyNativeChiAttribute.PhysicalDamageDecrease => 50,
			LegacyNativeChiAttribute.MagicDamageIncrease => 50,
			LegacyNativeChiAttribute.MagicDamageDecrease => 50,
			_ => 10,
		};
	}
}
