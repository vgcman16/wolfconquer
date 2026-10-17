using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClassSkillCapabilityTests
{
    public static TheoryData<LegacyMagicSort, ushort, LegacyMagicSortCapability, string, string> ModeledSkills =>
        new()
        {
            { LegacyMagicSort.BurntFrost, 12_940, LegacyMagicSortCapability.SpecialCombatDamage, "BurntFrost(86)", "burntfrost-combat-runtime" },
            { LegacyMagicSort.SwirlingStorm, 12_890, LegacyMagicSortCapability.SpecialCombatDamage, "SwirlingStorm(91)", "swirlingstorm-combat-runtime" },
            { LegacyMagicSort.Pitching, 14_270, LegacyMagicSortCapability.StateAndBuffRuntime, "Pitching(92)", "pitching-state-runtime" },
            { LegacyMagicSort.HeavensWrath, 15_710, LegacyMagicSortCapability.SpecialCombatDamage, "HeavensWrath(95)", "heavenswrath-combat-runtime" },
            { LegacyMagicSort.ShieldBlock, 10_470, LegacyMagicSortCapability.StateAndBuffRuntime, "ShieldBlock(52)", "shieldblock-state-runtime" },
            { LegacyMagicSort.Oblivion, 10_390, LegacyMagicSortCapability.XpSkill, "Oblivion(53)", "oblivion-xp-skill-runtime" },
            { LegacyMagicSort.WhirlwindKick, 10_415, LegacyMagicSortCapability.SpecialCombatDamage, "WhirlwindKick(54)", "whirlwindkick-combat-runtime" },
            { LegacyMagicSort.WhirlwindKick, 12_330, LegacyMagicSortCapability.SpecialCombatDamage, "WhirlwindKick(54)", "whirlwindkick-combat-runtime" },
            { LegacyMagicSort.WhirlwindKick, 12_340, LegacyMagicSortCapability.SpecialCombatDamage, "WhirlwindKick(54)", "whirlwindkick-combat-runtime" },
            { LegacyMagicSort.WhirlwindKick, 13_490, LegacyMagicSortCapability.SpecialCombatDamage, "WhirlwindKick(54)", "whirlwindkick-combat-runtime" },
            { LegacyMagicSort.MortalDrag, 11_180, LegacyMagicSortCapability.SpecialCombatDamage, "MortalDrag(65)", "mortaldrag-combat-runtime" },
            { LegacyMagicSort.AirKick, 12_320, LegacyMagicSortCapability.SpecialCombatDamage, "AirKick(79)", "airkick-combat-runtime" },
        };

    [Theory]
    [MemberData(nameof(ModeledSkills))]
    public void ClassifyExactMatchingDecompileClassSkillAsDedicatedRuntime(
        LegacyMagicSort sort,
        ushort spellId,
        LegacyMagicSortCapability capability,
        string sortName,
        string nextSeam)
    {
        var info = LegacyMagicSortCapabilities.Classify(sort, spellId);

        Assert.Equal(capability, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal(sortName, info.SortName);
        Assert.Equal(nextSeam, info.NextSeam);
        Assert.Contains("Matching DecompiledSource", info.SourceEvidence);
        Assert.Contains("D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5", info.SourceEvidence);
        Assert.Contains("visible exact-D13", info.SuggestedNextOwner);
    }

    public static TheoryData<LegacyMagicSort, ushort> AdjacentUnsupportedSkills =>
        new()
        {
            { LegacyMagicSort.BurntFrost, 12_941 },
            { LegacyMagicSort.SwirlingStorm, 12_891 },
            { LegacyMagicSort.Pitching, 14_271 },
            { LegacyMagicSort.HeavensWrath, 15_711 },
            { LegacyMagicSort.ShieldBlock, 10_471 },
            { LegacyMagicSort.Oblivion, 10_391 },
            { LegacyMagicSort.WhirlwindKick, 10_420 },
            { LegacyMagicSort.MortalDrag, 11_181 },
            { LegacyMagicSort.AirKick, 12_321 },
        };

    [Theory]
    [MemberData(nameof(AdjacentUnsupportedSkills))]
    public void KeepAdjacentClassSkillRowsUnsupported(LegacyMagicSort sort, ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(sort, spellId);

        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
    }
}
