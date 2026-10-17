using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort SacredBlessingSpellId = 15_890;
    private const ushort DeathSighSpellId = 15_900;
    private const ushort DeadlyStrikeSpellId = 15_910;
    private const ushort HerculesSpellId = 1_115;
    private const ushort BloodyScytheSpellId = 11_170;
    private const ushort MegaboltSpellId = 15_790;
    private const ushort WildDashAttackSpellId = 17_660;
    private const ushort ThunderStrikeSpellId = 15_860;
    private const ushort RevengeAttackSpellId = 17_410;
    private const ushort ArrowBladesSpellId = 18_220;
    private const ushort StarburstArrowsSpellId = 18_240;
    private const ushort DragonTransformationPassiveSpellId = 22_590;
    private const ushort DragonRisingSpellId = 22_630;
    private const ushort SuanniRoarSpellId = 22_810;
    private const ushort RoamerSpellId = 7_040;
    private const ushort DrukyleSpellId = 21_530;
    private const ushort SpitfireSpellId = 21_600;
    private const ushort LavaSeaSpellId = 21_610;
    private const ushort StarVolcanoSpellId = 21_620;
    private const ushort CaptiveArrowSpellId = 21_640;
    private const ushort SandMistSpellId = 21_650;
    private const ushort SpitfirePassiveSpellId = 21_690;
    private const ushort StarVolcanoPassiveSpellId = 22_070;
    private const ushort LavaSeaPassiveSpellId = 22_080;
    private const ushort DrukylePassiveSpellId = 22_110;
    private const ushort HolySanctionSpellId = 21_510;
    private const ushort IceAgeSpellId = 21_560;
    private const ushort TwospiendSpearSpellId = 21_570;
    private const ushort PheasantBeakSpellId = 21_580;
    private const ushort GiantGunSpellId = 21_660;
    private const ushort HolySanctionPassiveSpellId = 21_670;
    private const ushort TwospiendSpearPassiveSpellId = 21_680;
    private const ushort GiantGunPassiveSpellId = 21_700;
    private const ushort ThunderlordAttackSpellId = 21_920;
    private const ushort IceAgePassiveSpellId = 22_090;
    private const ushort PheasantBeakPassiveSpellId = 22_100;
    private const ushort SeaBurialSpellId = 14_680;
    private const ushort SuperTwofoldBladeSpellId = 12_080;
    private const ushort AngerOfStomperSpellId = 12_980;
    private const ushort SelectorCannonBarrageSpellId = 11_050;
    private const ushort WarSuit402NormalAttack1SpellId = 11_950;
    private const ushort WarSuit402NormalAttack2SpellId = 11_951;
    private const ushort WarSuit402NormalAttack3SpellId = 11_952;
    private const ushort WildwindSpellId = 14_530;
    private const ushort HeavensWrathSpellId = 15_710;
    private const ushort SelectorCrackingShockSpellId = 15_720;
    private const ushort ThunderRampageSpellId = 15_780;
    private const ushort WindstormBattleaxeSpellId = 15_800;
    private const ushort NormalAttack1SpellId = 17_390;
    private const ushort NormalAttack2SpellId = 17_391;
    private const ushort NormalAttack3SpellId = 17_392;
    private const ushort WarSuit401NormalAttack1SpellId = 17_400;
    private const ushort WarSuit401NormalAttack2SpellId = 17_401;
    private const ushort TripleBlastsSpellId = 12_850;
    private const ushort SwirlingStormSpellId = 12_890;
    private const ushort ChargingVortexSpellId = 11_190;
    private const ushort EagleEyeSpellId = 11_030;
    private const ushort OmnipotenceSpellId = 12_860;
    private const ushort BlisteringWaveSpellId = 11_650;
    private const ushort MortalWoundSpellId = 11_660;
    private const ushort BonePulseSpellId = 16_390;
    private const ushort WaterPrisonSpellId = 16_400;
    private const ushort WaterPrisonPassiveSpellId = 16_410;
    private const ushort WhirlShurikenSpellId = 16_420;
    private const ushort WhirlShurikenPassiveSpellId = 16_430;
    private const ushort LightningSlashSpellId = 16_480;
    private const ushort LightningSlashPassiveSpellId = 16_490;
    private const ushort WildFireballSpellId = 16_520;
    private const ushort WildFireballPassiveSpellId = 16_530;
    private const ushort IronboneSpellId = 17_640;
    private const ushort IronbonePassiveSpellId = 17_650;
    private const ushort DragonhowlWarSuitNormalAttack1SpellId = 17_460;
    private const ushort DragonhowlWarSuitNormalAttack2SpellId = 17_470;
    private const ushort DragonhowlWarSuitNormalAttack3SpellId = 17_480;
    private const ushort DragonhowlTripleAttackSpellId = 17_490;
    private const ushort BloodlustWarSuitNormalAttack1SpellId = 17_500;
    private const ushort BloodlustWarSuitNormalAttack2SpellId = 17_510;
    private const ushort BloodlustWarSuitNormalAttack3SpellId = 17_520;
    private const ushort BloodlustTripleAttackSpellId = 17_530;
    private const ushort RedcurseWarSuitNormalAttack1SpellId = 17_540;
    private const ushort RedcurseWarSuitNormalAttack2SpellId = 17_550;
    private const ushort RedcurseWarSuitNormalAttack3SpellId = 17_560;
    private const ushort RedcurseTripleAttackSpellId = 17_570;
    private const ushort CrackShotSpellId = 18_690;
    private const ushort OverlordSpellId = 21_490;

    [Theory]
    [InlineData(15_880, 16, 30_300U, 30_140, 30_180)]
    [InlineData(SacredBlessingSpellId, 5, 30_200U, 30_140, 30_195)]
    [InlineData(DeathSighSpellId, 1, 30_900U, 30_215, 30_290)]
    [InlineData(DeadlyStrikeSpellId, 5, 30_600U, 30_200, 30_255)]
    public void CalculateMatchingDecompileStatusPlayerTargetUsesDamageOnHumanPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int baselineDamageOnHuman,
        int boostedDamageOnHuman)
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, baselineDamageOnHuman));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, boostedDamageOnHuman));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal((uint)(baselineDamageOnHuman % 1_000), baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal((uint)(boostedDamageOnHuman % 1_000), boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected matching-decompile {baselineSpell.Name} Physical.OnPlayer damage percent to use DamageOnHuman. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Fact]
    public void CalculateBloodyScythePlayerTargetUsesDecompiledFixedOneHundredTwoPercent()
    {
        const ushort fixedPercentSpellId = 31_016;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [BloodyScytheSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(BloodyScytheSpellId, 5, 30_200, damageOnHuman: 30_080));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(BloodyScytheSpellId, 5, 30_200, damageOnHuman: 30_160));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 5, 102, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            BloodyScytheSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            BloodyScytheSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.NotEqual(baselineSpell.DamageOnHuman, boostedSpell.DamageOnHuman);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Fact]
    public void CalculateMegaboltPlayerTargetUsesDecompiledFixedOneHundredTwentyThreePercent()
    {
        const ushort fixedPercentSpellId = 31_007;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [MegaboltSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(MegaboltSpellId, 90, 30_300, damageOnHuman: 30_098));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(MegaboltSpellId, 90, 30_300, damageOnHuman: 30_148));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 90, 123, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            MegaboltSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            MegaboltSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(baselineSpell.DamageOnHuman, boostedSpell.DamageOnHuman);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Fact]
    public void CalculateHerculesPlayerTargetUsesDecompiledFixedOneHundredPercent()
    {
        const ushort fixedPercentSpellId = 31_009;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [HerculesSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(HerculesSpellId, 5, 82, damageOnHuman: 0));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(HerculesSpellId, 5, 150, damageOnHuman: 0));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 5, 100, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            HerculesSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            HerculesSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(baselineSpell.Damage, boostedSpell.Damage);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Fact]
    public void CalculateAngerOfStomperPlayerTargetUsesDecompiledDamage3Percent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [AngerOfStomperSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(AngerOfStomperSpellId, 14, 30_300, damageOnHuman: 0, damage3: 30_120));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(AngerOfStomperSpellId, 14, 30_300, damageOnHuman: 0, damage3: 30_170));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            AngerOfStomperSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            AngerOfStomperSpellId,
            out _,
            out var boostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(120U, baselineSpell.Damage3 % 1_000);
        Assert.Equal(170U, boostedSpell.Damage3 % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected decompiled AngerOfStomper Physical.OnPlayer damage percent to use Damage3. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Theory]
    [InlineData(ThunderStrikeSpellId, 5, 30_800U, 30_160, 30_210)]
    [InlineData(DragonTransformationPassiveSpellId, 90, 30_500U, 30_050, 30_100)]
    [InlineData(DragonRisingSpellId, 21, 30_900U, 30_245, 30_295)]
    [InlineData(SuanniRoarSpellId, 5, 30_300U, 30_050, 30_100)]
    [InlineData(DevouringStrikeSpellId, 1, 30_300U, 30_105, 30_155)]
    public void CalculateRemainingNonPiratePhysicalPlayerTargetUsesConfiguredDamageOnHumanPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int baselineDamageOnHuman,
        int boostedDamageOnHuman)
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, baselineDamageOnHuman));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, boostedDamageOnHuman));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal((uint)(baselineDamageOnHuman % 1_000), baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal((uint)(boostedDamageOnHuman % 1_000), boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected the configured {baselineSpell.Name} player-target physical selector to use DamageOnHuman. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Theory]
    [InlineData(RevengeAttackSpellId, 90, 30_600U, 30_100, 30_150, 200U, 250U)]
    [InlineData(WildDashAttackSpellId, 90, 30_300U, 30_085, 30_135, 185U, 235U)]
    [InlineData(ArrowBladesSpellId, 103, 30_300U, 30_080, 30_120, 180U, 220U)]
    public void CalculateCombatGearNonPiratePlayerTargetUsesDecompiledCombatGearMinimumBonusPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int baselineDamageOnHuman,
        int boostedDamageOnHuman,
        uint expectedBaselinePercent,
        uint expectedBoostedPercent)
    {
        const ushort fixedPercentSpellId = 31_025;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, baselineDamageOnHuman));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, boostedDamageOnHuman));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBaselinePercent, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBoostedPercent, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal((uint)(baselineDamageOnHuman % 1_000), baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal((uint)(boostedDamageOnHuman % 1_000), boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
    }

    [Theory]
    [InlineData(StarburstArrowsSpellId, 69, 30_300U, 30_103, 30_153)]
    public void CalculateSectorBackPhysicalPlayerTargetUsesDecompiledRowDamageFallback(
        ushort spellId,
        byte sort,
        uint damage,
        int baselineDamageOnHuman,
        int boostedDamageOnHuman)
    {
        const ushort fixedPercentSpellId = 31_026;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, baselineDamageOnHuman));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, boostedDamageOnHuman));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, damage, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(baselineSpell.Damage, fixedPercentSpell.Damage);
        Assert.Equal((uint)(baselineDamageOnHuman % 1_000), baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal((uint)(boostedDamageOnHuman % 1_000), boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Theory]
    [InlineData(DrukyleSpellId, 90, 30_900U, 30_100, 30_150)]
    [InlineData(SpitfireSpellId, 5, 30_800U, 30_100, 30_150)]
    [InlineData(LavaSeaSpellId, 21, 30_600U, 30_090, 30_140)]
    [InlineData(StarVolcanoSpellId, 21, 30_600U, 30_090, 30_140)]
    [InlineData(CaptiveArrowSpellId, 90, 30_300U, 30_090, 30_140)]
    [InlineData(SandMistSpellId, 21, 30_600U, 30_100, 30_150)]
    [InlineData(SpitfirePassiveSpellId, 5, 30_800U, 30_220, 30_270)]
    [InlineData(StarVolcanoPassiveSpellId, 21, 30_600U, 30_200, 30_250)]
    [InlineData(LavaSeaPassiveSpellId, 21, 30_600U, 30_200, 30_250)]
    [InlineData(DrukylePassiveSpellId, 90, 30_900U, 30_200, 30_250)]
    public void CalculatePirateDamageOnHumanPlayerTargetUsesDecompiledCombatGearMinimumBonusPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int baselineDamageOnHuman,
        int boostedDamageOnHuman)
    {
        const ushort fixedPercentSpellId = 31_027;
        var expectedBaselinePercent = 100U + (uint)(baselineDamageOnHuman % 1_000);
        var expectedBoostedPercent = 100U + (uint)(boostedDamageOnHuman % 1_000);
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, baselineDamageOnHuman));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, boostedDamageOnHuman));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBaselinePercent, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBoostedPercent, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal((uint)(baselineDamageOnHuman % 1_000), baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal((uint)(boostedDamageOnHuman % 1_000), boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Fact]
    public void CalculateRoamerPlayerTargetUsesDecompiledRowDamageFallback()
    {
        const ushort fixedPercentSpellId = 31_024;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [RoamerSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(RoamerSpellId, 90, 30_500, 30_110));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(RoamerSpellId, 90, 30_500, 30_160));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 90, 30_500, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            RoamerSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            RoamerSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(baselineSpell.Damage, fixedPercentSpell.Damage);
        Assert.Equal(110U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(160U, boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Fact]
    public void CalculateThunderRampagePlayerTargetUsesDecompiledDamageOnHumanPercent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ThunderRampageSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(ThunderRampageSpellId, 94, 30_500, 30_100, damageOnMonster: 30_300));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(ThunderRampageSpellId, 94, 30_500, 30_150, damageOnMonster: 30_300));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            ThunderRampageSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            ThunderRampageSpellId,
            out _,
            out var boostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(baselineSpell.DamageOnMonster, boostedSpell.DamageOnMonster);
        Assert.Equal(100U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(150U, boostedSpell.DamageOnHuman % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected decompiled ThunderRampage Physical.OnPlayer damage percent to use DamageOnHuman. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Theory]
    [InlineData(BonePulseSpellId, 16, 30_400U, 30_230, 30_280)]
    [InlineData(WhirlShurikenSpellId, 90, 30_350U, 30_105, 30_155)]
    [InlineData(WhirlShurikenPassiveSpellId, 90, 30_350U, 30_200, 30_250)]
    [InlineData(LightningSlashSpellId, 90, 30_350U, 30_105, 30_155)]
    [InlineData(LightningSlashPassiveSpellId, 90, 30_350U, 30_215, 30_265)]
    [InlineData(WildFireballSpellId, 90, 30_280U, 30_095, 30_145)]
    public void CalculateNonPirateDamage2PlayerTargetUsesDecompiledPhysicalOnPlayerDamage2Percent(
        ushort spellId,
        byte sort,
        uint damage,
        int baselineDamage2,
        int boostedDamage2)
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 0, damage2: baselineDamage2));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 0, damage2: boostedDamage2));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal((uint)(baselineDamage2 % 1_000), baselineSpell.Damage2 % 1_000);
        Assert.Equal((uint)(boostedDamage2 % 1_000), boostedSpell.Damage2 % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected decompiled {baselineSpell.Name} Physical.OnPlayer damage percent to use Damage2. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Theory]
    [InlineData(IronboneSpellId, 90, 30_120U, 30_100, 30_150)]
    [InlineData(IronbonePassiveSpellId, 90, 30_500U, 30_205, 30_255)]
    public void CalculateIronbonePlayerTargetUsesDecompiledCombatGearMinimumBonusPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int baselineDamage2,
        int boostedDamage2)
    {
        const ushort fixedPercentSpellId = 31_030;
        var expectedBaselinePercent = 100U + (uint)(baselineDamage2 % 1_000);
        var expectedBoostedPercent = 100U + (uint)(boostedDamage2 % 1_000);
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 0, damage2: baselineDamage2));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 0, damage2: boostedDamage2));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBaselinePercent, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBoostedPercent, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal((uint)(baselineDamage2 % 1_000), baselineSpell.Damage2 % 1_000);
        Assert.Equal((uint)(boostedDamage2 % 1_000), boostedSpell.Damage2 % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Theory]
    [InlineData(DragonhowlWarSuitNormalAttack1SpellId, 87, 30_130U, 0, 0, 0, 30_100, 100U, 200U)]
    [InlineData(DragonhowlWarSuitNormalAttack2SpellId, 87, 30_130U, 0, 0, 0, 30_100, 100U, 200U)]
    [InlineData(DragonhowlWarSuitNormalAttack3SpellId, 87, 30_130U, 0, 0, 0, 30_100, 100U, 200U)]
    [InlineData(DragonhowlTripleAttackSpellId, 99, 30_250U, 0, 33, 2, 30_085, 66U, 185U)]
    [InlineData(BloodlustWarSuitNormalAttack1SpellId, 87, 30_130U, 0, 0, 0, 30_100, 100U, 200U)]
    [InlineData(BloodlustWarSuitNormalAttack2SpellId, 87, 30_130U, 0, 0, 0, 30_100, 100U, 200U)]
    [InlineData(BloodlustWarSuitNormalAttack3SpellId, 87, 30_130U, 0, 0, 0, 30_100, 100U, 200U)]
    [InlineData(BloodlustTripleAttackSpellId, 99, 30_150U, 0, 33, 1, 30_085, 66U, 185U)]
    [InlineData(RedcurseWarSuitNormalAttack1SpellId, 87, 30_130U, 0, 0, 0, 30_100, 100U, 200U)]
    [InlineData(RedcurseWarSuitNormalAttack2SpellId, 87, 30_130U, 0, 0, 0, 30_100, 100U, 200U)]
    [InlineData(RedcurseWarSuitNormalAttack3SpellId, 87, 30_130U, 0, 0, 0, 30_100, 100U, 200U)]
    [InlineData(RedcurseTripleAttackSpellId, 99, 30_200U, 0, 33, 1, 30_085, 66U, 185U)]
    public void CalculateCombatGearActiveAttackPlayerTargetUsesDecompiledCombatGearMinimumBonusPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int damageOnHuman,
        int damage2,
        int damage3,
        int damageOnMonster,
        uint gDamage,
        uint expectedPercent)
    {
        const ushort fixedPercentSpellId = 31_031;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var combatGearWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(
                spellId,
                sort,
                damage,
                damageOnHuman,
                damage2,
                damage3,
                damageOnMonster,
                gDamage));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedPercent, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            combatGearWorldData,
            spellId,
            out _,
            out var combatGearSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(expectedPercent, combatGearSpell.Damage);

        var combatGearDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            combatGearSpell,
            combatGearWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, combatGearDamage);
    }

    [Theory]
    [InlineData(CrackShotSpellId, 104, 30_500U, 30_020, 30_500, 30_030, 0, 100U, 120U)]
    [InlineData(OverlordSpellId, 5, 30_050U, 30_200, 1_000, 400, 0, 100U, 300U)]
    public void CalculateCombatGearAttackCasePlayerTargetUsesDecompiledCombatGearMinimumBonusPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int damageOnHuman,
        int damage2,
        int damage3,
        int damageOnMonster,
        uint gDamage,
        uint expectedPercent)
    {
        const ushort fixedPercentSpellId = 31_032;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var combatGearWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(
                spellId,
                sort,
                damage,
                damageOnHuman,
                damage2,
                damage3,
                damageOnMonster,
                gDamage));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedPercent, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            combatGearWorldData,
            spellId,
            out _,
            out var combatGearSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(expectedPercent, combatGearSpell.Damage);

        var combatGearDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            combatGearSpell,
            combatGearWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, combatGearDamage);
    }

    [Fact]
    public void CalculateCloseMortalWoundPlayerTargetUsesDecompiledFixedEightyPercent()
    {
        const ushort fixedPercentSpellId = 31_017;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [MortalWoundSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(MortalWoundSpellId, 1, 30_400, damageOnHuman: 0, damage2: 30_116));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(MortalWoundSpellId, 1, 30_400, damageOnHuman: 0, damage2: 30_166));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 1, 80, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            MortalWoundSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            MortalWoundSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.NotEqual(baselineSpell.Damage2, boostedSpell.Damage2);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Theory]
    [InlineData(WaterPrisonSpellId)]
    [InlineData(WaterPrisonPassiveSpellId)]
    public void CalculateWaterPrisonPlayerTargetUsesDecompiledDamage2OverOneHundredEighty(ushort spellId)
    {
        const ushort fixedPercentSpellId = 31_004;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, 5, 30_350, damageOnHuman: 0, damage2: 30_180));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, 5, 30_350, damageOnHuman: 0, damage2: 30_270));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 5, 100, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 5, 150, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(180U, baselineSpell.Damage2 % 1_000);
        Assert.Equal(270U, boostedSpell.Damage2 % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Fact]
    public void CalculateCloseBlisteringWavePlayerTargetUsesDecompiledFixedFiftyPercent()
    {
        const ushort fixedPercentSpellId = 31_011;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [BlisteringWaveSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(BlisteringWaveSpellId, 69, 30_280, damageOnHuman: 0, damage2: 30_106));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(BlisteringWaveSpellId, 69, 30_280, damageOnHuman: 0, damage2: 30_156));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 69, 50, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            BlisteringWaveSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            BlisteringWaveSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.NotEqual(baselineSpell.Damage2, boostedSpell.Damage2);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Fact]
    public void CalculateCloseKineticSparkPlayerTargetUsesDecompiledFixedThirtyPercent()
    {
        const ushort fixedPercentSpellId = 31_012;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [KineticSparkSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(KineticSparkSpellId, 67, 30_280, damageOnHuman: 30_090));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(KineticSparkSpellId, 67, 30_380, damageOnHuman: 30_190));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 67, 30, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            KineticSparkSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            KineticSparkSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(baselineSpell.Damage, boostedSpell.Damage);
        Assert.NotEqual(baselineSpell.DamageOnHuman, boostedSpell.DamageOnHuman);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Theory]
    [InlineData(TripleBlastsSpellId, 1, 70U, 31_013)]
    [InlineData(SwirlingStormSpellId, 91, 110U, 31_014)]
    public void CalculateCloseWindWalkerPlayerTargetUsesDecompiledFixedPercent(
        ushort spellId,
        byte sort,
        uint fixedPercent,
        ushort fixedPercentSpellId)
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, 30_110, damageOnHuman: 30_090));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, 30_160, damageOnHuman: 30_140));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, fixedPercent, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(baselineSpell.Damage, boostedSpell.Damage);
        Assert.NotEqual(baselineSpell.DamageOnHuman, boostedSpell.DamageOnHuman);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Fact]
    public void CalculateChargingVortexPlayerTargetUsesDecompiledFixedOneHundredPercent()
    {
        const ushort fixedPercentSpellId = 31_015;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [ChargingVortexSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(ChargingVortexSpellId, 64, 30_070, damageOnHuman: 0));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(ChargingVortexSpellId, 64, 30_170, damageOnHuman: 0));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 64, 100, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            ChargingVortexSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            ChargingVortexSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(baselineSpell.Damage, boostedSpell.Damage);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Theory]
    [InlineData(EagleEyeSpellId, 1, 30_028U, 30_078U, 45U, 31_018)]
    [InlineData(MoveLineSpellId, 61, 30_095U, 30_145U, 101U, 31_019)]
    public void CalculateClosePiratePlayerTargetUsesDecompiledFixedPercent(
        ushort spellId,
        byte sort,
        uint baselineDamagePercent,
        uint boostedDamagePercent,
        uint fixedPercent,
        ushort fixedPercentSpellId)
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, baselineDamagePercent, damageOnHuman: 0));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, boostedDamagePercent, damageOnHuman: 0));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, fixedPercent, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(baselineSpell.Damage, boostedSpell.Damage);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Fact]
    public void CalculateOmnipotencePlayerTargetUsesDecompiledFixedTenPercent()
    {
        const ushort fixedPercentSpellId = 31_020;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [OmnipotenceSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(OmnipotenceSpellId, 85, 30_100, damageOnHuman: 30_350));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(OmnipotenceSpellId, 85, 30_150, damageOnHuman: 30_450));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 85, 10, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            OmnipotenceSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            OmnipotenceSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(baselineSpell.Damage, boostedSpell.Damage);
        Assert.NotEqual(baselineSpell.DamageOnHuman, boostedSpell.DamageOnHuman);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Theory]
    [InlineData(FastBladeSpellId, 31_021)]
    [InlineData(ScentSwordSpellId, 31_022)]
    public void CalculateLineSkillPlayerTargetUsesDecompiledFixedOneHundredTenPercent(
        ushort spellId,
        ushort fixedPercentSpellId)
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, 5, 30_100, damageOnHuman: 0));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, 5, 30_160, damageOnHuman: 0));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 5, 110, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.NotEqual(baselineSpell.Damage, boostedSpell.Damage);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(fixedPercentDamage, baselineDamage);
        Assert.Equal(fixedPercentDamage, boostedDamage);
    }

    [Theory]
    [InlineData(WildFireballPassiveSpellId, 90, 30_280U)]
    public void CalculatePlayerTargetUsesDecompiledDamageOnHumanOverThreeHundredFifty(
        ushort spellId,
        byte sort,
        uint damage)
    {
        const ushort fixedPercentSpellId = 31_006;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 30_350, damage2: 30_090));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 30_525, damage2: 30_090));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, 100, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, 150, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(350U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(525U, boostedSpell.DamageOnHuman % 1_000);
        Assert.Equal(baselineSpell.Damage2, boostedSpell.Damage2);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Fact]
    public void CalculatePheasantBeakPlayerTargetUsesDecompiledDamageOnHumanOverThreeHundredFiftyThenCombatGear()
    {
        const ushort fixedPercentSpellId = 31_029;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [PheasantBeakSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(
                PheasantBeakSpellId,
                21,
                30_800,
                damageOnHuman: 30_350,
                damage2: 30_090,
                gDamage: 30_500));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(
                PheasantBeakSpellId,
                21,
                30_800,
                damageOnHuman: 30_525,
                damage2: 30_090,
                gDamage: 30_500));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 21, 190, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 21, 285, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            PheasantBeakSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            PheasantBeakSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(350U, baselineSpell.DamageOnHuman % 1_000);
        Assert.Equal(525U, boostedSpell.DamageOnHuman % 1_000);
        Assert.Equal(90U, baselineSpell.Damage2 % 1_000);
        Assert.Equal(500U, baselineSpell.GDamage % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Theory]
    [InlineData(HolySanctionSpellId, 5, 30_550U, 0, 30_090, 30_140)]
    [InlineData(IceAgeSpellId, 21, 30_500U, 10_010, 30_100, 30_150)]
    [InlineData(GiantGunSpellId, 90, 30_800U, 0, 30_100, 30_150)]
    [InlineData(HolySanctionPassiveSpellId, 5, 30_550U, 30, 30_200, 30_250)]
    [InlineData(TwospiendSpearPassiveSpellId, 90, 30_800U, 5, 30_200, 30_250)]
    [InlineData(GiantGunPassiveSpellId, 90, 30_800U, 30, 30_200, 30_250)]
    [InlineData(ThunderlordAttackSpellId, 5, 30_500U, 500, 30_100, 30_150)]
    [InlineData(IceAgePassiveSpellId, 21, 30_500U, 10_010, 30_200, 30_250)]
    [InlineData(PheasantBeakPassiveSpellId, 21, 30_800U, 0, 30_200, 30_250)]
    public void CalculatePirateDamage2PlayerTargetUsesDecompiledCombatGearMinimumBonusPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int damageOnHuman,
        int baselineDamage2,
        int boostedDamage2)
    {
        const ushort fixedPercentSpellId = 31_028;
        var expectedBaselinePercent = 100U + (uint)(baselineDamage2 % 1_000);
        var expectedBoostedPercent = 100U + (uint)(boostedDamage2 % 1_000);
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman, damage2: baselineDamage2));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman, damage2: boostedDamage2));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBaselinePercent, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBoostedPercent, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(baselineSpell.DamageOnHuman, boostedSpell.DamageOnHuman);
        Assert.Equal((uint)(baselineDamage2 % 1_000), baselineSpell.Damage2 % 1_000);
        Assert.Equal((uint)(boostedDamage2 % 1_000), boostedSpell.Damage2 % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Fact]
    public void CalculateActiveTwospiendSpearPlayerTargetUsesDecompiledCombatGearMinimumBonusPercent()
    {
        const ushort fixedPercentSpellId = 31_001;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [TwospiendSpearSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(TwospiendSpearSpellId, 90, 30_800, damageOnHuman: 5, damage2: 30_090));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(TwospiendSpearSpellId, 90, 30_800, damageOnHuman: 55, damage2: 30_090));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 90, 190, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            TwospiendSpearSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            TwospiendSpearSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(baselineSpell.Damage2, boostedSpell.Damage2);
        Assert.Equal(90U, baselineSpell.Damage2 % 1_000);
        Assert.Equal(5U, baselineSpell.DamageOnHuman);
        Assert.Equal(55U, boostedSpell.DamageOnHuman);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(baselineDamage, boostedDamage);
        Assert.Equal(fixedPercentDamage, baselineDamage);
    }

    [Fact]
    public void CalculatePassiveTwospiendSpearPlayerTargetUsesDecompiledCombatGearMinimumBonusPercent()
    {
        const ushort fixedPercentSpellId = 31_002;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [TwospiendSpearPassiveSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(TwospiendSpearPassiveSpellId, 90, 30_800, damageOnHuman: 5, damage2: 30_200));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(TwospiendSpearPassiveSpellId, 90, 30_800, damageOnHuman: 55, damage2: 30_200));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 90, 300, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            TwospiendSpearPassiveSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            TwospiendSpearPassiveSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(baselineSpell.Damage2, boostedSpell.Damage2);
        Assert.Equal(200U, baselineSpell.Damage2 % 1_000);
        Assert.Equal(5U, baselineSpell.DamageOnHuman);
        Assert.Equal(55U, boostedSpell.DamageOnHuman);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(baselineDamage, boostedDamage);
        Assert.Equal(fixedPercentDamage, baselineDamage);
    }

    [Fact]
    public void CalculateSeaBurialPlayerTargetUsesDecompiledPhysicalOnPlayerFixedOneHundredTenPercent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SeaBurialSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(SeaBurialSpellId, 90, 30_100, damageOnHuman: 1, damage2: 30_100, gDamage: 100));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(SeaBurialSpellId, 90, 30_100, damageOnHuman: 1, damage2: 30_200, gDamage: 250));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(SeaBurialSpellId, 90, 110, damageOnHuman: 1, damage2: 1, gDamage: 1));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            SeaBurialSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            SeaBurialSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            SeaBurialSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.NotEqual(baselineSpell.Damage2, boostedSpell.Damage2);
        Assert.NotEqual(baselineSpell.GDamage, boostedSpell.GDamage);
        Assert.Equal(100U, baselineSpell.GDamage % 1_000);
        Assert.Equal(250U, boostedSpell.GDamage % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U);

        Assert.Equal(baselineDamage, boostedDamage);
        Assert.Equal(fixedPercentDamage, baselineDamage);
    }

    [Fact]
    public void CalculateCannonBarragePlayerTargetUsesDecompiledDefaultDamagePercent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [CannonBarrageSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(CannonBarrageSpellId, 57, 100, damageOnHuman: 0, damageOnMonster: 30_500));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(CannonBarrageSpellId, 57, 150, damageOnHuman: 0, damageOnMonster: 30_500));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            CannonBarrageSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            CannonBarrageSpellId,
            out _,
            out var boostedSpell));
        Assert.NotEqual(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(baselineSpell.DamageOnMonster, boostedSpell.DamageOnMonster);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected decompiled CannonBarrage Physical.OnPlayer to fall back to the spell Damage percent. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Theory]
    [InlineData(WarSuit402NormalAttack1SpellId, 87, 30_200U, 30_100, 30_150)]
    [InlineData(WarSuit402NormalAttack2SpellId, 87, 30_200U, 30_100, 30_150)]
    [InlineData(WarSuit402NormalAttack3SpellId, 87, 30_200U, 30_100, 30_150)]
    [InlineData(WildwindSpellId, 90, 30_600U, 30_100, 30_150)]
    [InlineData(HeavensWrathSpellId, 95, 30_300U, 30_025, 30_075)]
    [InlineData(WindstormBattleaxeSpellId, 5, 30_300U, 30_180, 30_230)]
    [InlineData(RageOfWarSpellId, 14, 30_300U, 30_110, 30_160)]
    [InlineData(NormalAttack1SpellId, 87, 30_300U, 30_100, 30_150)]
    [InlineData(NormalAttack2SpellId, 87, 30_300U, 30_100, 30_150)]
    [InlineData(NormalAttack3SpellId, 87, 30_300U, 30_100, 30_150)]
    [InlineData(WarSuit401NormalAttack1SpellId, 87, 30_400U, 30_100, 30_150)]
    [InlineData(WarSuit401NormalAttack2SpellId, 87, 30_400U, 30_100, 30_150)]
    public void CalculateDamageOnMonsterPlayerTargetUsesConfiguredDamageOnMonsterPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int baselineDamageOnMonster,
        int boostedDamageOnMonster)
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 0, damageOnMonster: baselineDamageOnMonster));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 0, damageOnMonster: boostedDamageOnMonster));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal((uint)(baselineDamageOnMonster % 1_000), baselineSpell.DamageOnMonster % 1_000);
        Assert.Equal((uint)(boostedDamageOnMonster % 1_000), boostedSpell.DamageOnMonster % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);

        Assert.True(
            boostedDamage > baselineDamage,
            $"Expected the configured {baselineSpell.Name} player-target physical selector to use DamageOnMonster. Baseline={baselineDamage}, boosted={boostedDamage}.");
    }

    [Fact]
    public void CalculateCrackingShockPlayerTargetUsesDecompiledEightyTwoThenDamageOnMonsterPercent()
    {
        const ushort fixedPercentSpellId = 31_010;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SelectorCrackingShockSpellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(SelectorCrackingShockSpellId, 34, 30_300, damageOnHuman: 0, damageOnMonster: 30_100));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(SelectorCrackingShockSpellId, 34, 30_300, damageOnHuman: 0, damageOnMonster: 30_200));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 34, 82, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, 34, 164, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            SelectorCrackingShockSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            SelectorCrackingShockSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(100U, baselineSpell.DamageOnMonster % 1_000);
        Assert.Equal(200U, boostedSpell.DamageOnMonster % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Theory]
    [InlineData(WarSuit402NormalAttack1SpellId, 87, 30_200U, 30_100, 30_150, 200U, 250U)]
    [InlineData(WarSuit402NormalAttack2SpellId, 87, 30_200U, 30_100, 30_150, 200U, 250U)]
    [InlineData(WarSuit402NormalAttack3SpellId, 87, 30_200U, 30_100, 30_150, 200U, 250U)]
    [InlineData(NormalAttack1SpellId, 87, 30_300U, 30_100, 30_150, 200U, 250U)]
    [InlineData(NormalAttack2SpellId, 87, 30_300U, 30_100, 30_150, 200U, 250U)]
    [InlineData(NormalAttack3SpellId, 87, 30_300U, 30_100, 30_150, 200U, 250U)]
    [InlineData(WarSuit401NormalAttack1SpellId, 87, 30_400U, 30_100, 30_150, 200U, 250U)]
    [InlineData(WarSuit401NormalAttack2SpellId, 87, 30_400U, 30_100, 30_150, 200U, 250U)]
    public void CalculateCombatGearNormalAttackPlayerTargetUsesDecompiledCombatGearMinimumBonusPercent(
        ushort spellId,
        byte sort,
        uint damage,
        int baselineDamageOnMonster,
        int boostedDamageOnMonster,
        uint expectedBaselinePercent,
        uint expectedBoostedPercent)
    {
        const ushort fixedPercentSpellId = 31_003;
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [spellId, fixedPercentSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 0, damageOnMonster: baselineDamageOnMonster));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(spellId, sort, damage, damageOnHuman: 0, damageOnMonster: boostedDamageOnMonster));
        var fixedBaselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBaselinePercent, damageOnHuman: 0));
        var fixedBoostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(fixedPercentSpellId, sort, expectedBoostedPercent, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            spellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            spellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBaselineWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBaselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedBoostedWorldData,
            fixedPercentSpellId,
            out _,
            out var fixedBoostedSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal((uint)(baselineDamageOnMonster % 1_000), baselineSpell.DamageOnMonster % 1_000);
        Assert.Equal((uint)(boostedDamageOnMonster % 1_000), boostedSpell.DamageOnMonster % 1_000);

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U);
        var fixedBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBaselineSpell,
            fixedBaselineWorldData,
            1U);
        var fixedBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attacker,
            target,
            fixedBoostedSpell,
            fixedBoostedWorldData,
            1U);

        Assert.Equal(fixedBaselineDamage, baselineDamage);
        Assert.Equal(fixedBoostedDamage, boostedDamage);
        Assert.True(boostedDamage > baselineDamage);
    }

    [Fact]
    public void CalculateCloseSuperTwofoldBladePlayerTargetUsesDecompiledPhysicalOnPlayerFixedOneHundredThirtyThreePercent()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SuperTwofoldBladeSpellId]);
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586);
        var baselineWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(SuperTwofoldBladeSpellId, 1, 30_085, damageOnHuman: 0, damageOnMonster: 112));
        var boostedWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(SuperTwofoldBladeSpellId, 1, 30_085, damageOnHuman: 0, damageOnMonster: 162));
        var fixedPercentWorldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(SuperTwofoldBladeSpellId, 1, 30_133, damageOnHuman: 0, damageOnMonster: 133));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            baselineWorldData,
            SuperTwofoldBladeSpellId,
            out _,
            out var baselineSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            boostedWorldData,
            SuperTwofoldBladeSpellId,
            out _,
            out var boostedSpell));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            fixedPercentWorldData,
            SuperTwofoldBladeSpellId,
            out _,
            out var fixedPercentSpell));
        Assert.Equal(baselineSpell.Damage, boostedSpell.Damage);
        Assert.Equal(112U, baselineSpell.DamageOnMonster % 1_000);
        Assert.Equal(162U, boostedSpell.DamageOnMonster % 1_000);
        Assert.Equal(133U, fixedPercentSpell.Damage % 1_000);

        var normalBaselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttack",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U,
            0);
        var normalBoostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttack",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U,
            0);
        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttack",
            attacker,
            target,
            baselineSpell,
            baselineWorldData,
            1U,
            1);
        var boostedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttack",
            attacker,
            target,
            boostedSpell,
            boostedWorldData,
            1U,
            1);
        var fixedPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamageWithIncreaseAttack",
            attacker,
            target,
            fixedPercentSpell,
            fixedPercentWorldData,
            1U,
            1);

        Assert.Equal(normalBaselineDamage, normalBoostedDamage);
        Assert.Equal(baselineDamage, boostedDamage);
        Assert.Equal(fixedPercentDamage, baselineDamage);
    }

    [Fact]
    public void ResolveSuperTwofoldBladeClosePlayerHitUsesDecompiledLessThanThreeTileBranch()
    {
        var attacker = CreateSession(
            strength: 300,
            stamina: 100,
            spellIds: [SuperTwofoldBladeSpellId]);
        var closeTarget = CreateSession(
            uid: TargetPlayerUid,
            name: "CloseTarget",
            x: 832,
            y: 585);
        var farTarget = CreateSession(
            uid: TargetPlayerUid + 1,
            name: "FarTarget",
            x: 834,
            y: 586);
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                SuperTwofoldBladeSpellId,
                sort: 1,
                useMana: 0,
                damage: 30_085,
                range: 5,
                coldTime: 0,
                damageOnMonster: 112));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attacker.PlayerSpells,
            worldData,
            SuperTwofoldBladeSpellId,
            out _,
            out var spell));

        var closeIncreaseAttack = InvokePrivateInt32(
            "ResolveLegacyPhysicalSpellsPlayerIncreaseAttack",
            attacker,
            closeTarget,
            spell);
        var farIncreaseAttack = InvokePrivateInt32(
            "ResolveLegacyPhysicalSpellsPlayerIncreaseAttack",
            attacker,
            farTarget,
            spell);

        Assert.Equal(1, closeIncreaseAttack);
        Assert.Equal(0, farIncreaseAttack);
    }

    [Fact]
    public void CalculatePlayerTargetPhysicalDamageUsesDecompiledDoFocusAjustAttackBeforeDefense()
    {
        var baselineAttacker = CreateSession(
            strength: 1_000,
            stamina: 100,
            spellIds: [HerculesSpellId]);
        var focusedAttacker = CreateSession(
            strength: 1_000,
            stamina: 100,
            spellIds: [HerculesSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["doFocus"] = "true",
                ["IntensifyDamage"] = "200",
            });
        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 834,
            y: 586,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Defense"] = "100",
            });
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateSelectorRow(HerculesSpellId, sort: 5, damage: 100, damageOnHuman: 0));
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            baselineAttacker.PlayerSpells,
            worldData,
            HerculesSpellId,
            out _,
            out var spell));

        var baselineDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            baselineAttacker,
            target,
            spell,
            worldData,
            1U);
        var focusedDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            focusedAttacker,
            target,
            spell,
            worldData,
            1U);

        Assert.True(
            focusedDamage > baselineDamage * 2U,
            $"Expected decompiled doFocus to boost before target defense. baseline={baselineDamage}, focused={focusedDamage}.");
    }

    private static MagicType CreateSelectorRow(
        ushort spellId,
        byte sort,
        uint damage,
        int damageOnHuman,
        int damage2 = 0,
        int damage3 = 0,
        int damageOnMonster = 0,
        uint gDamage = 0)
    {
        return CreateMagicRow(
            spellId,
            sort: sort,
            useMana: 0,
            damage: damage,
            range: 8,
            coldTime: 0,
            damageOnHuman: damageOnHuman,
            damage2: damage2,
            damage3: damage3,
            damageOnMonster: damageOnMonster,
            gDamage: gDamage);
    }

    private static int InvokePrivateInt32(string methodName, params object?[] arguments)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(null, arguments);
        return Assert.IsType<int>(result);
    }
}
