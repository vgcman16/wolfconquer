using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint PhysicalDragonSwingRuneItemId = 4_010_601;
    private const int PhysicalDragonSwingStatusFlag = 0xA0;
    private const string PhysicalDragonSwingRollAttribute = "LegacyPhysicalDragonSwingRoll";
    private const string PhysicalDragonSwingEffect = "poisonmiss";
    private const long PhysicalDragonSwingTargetHitpoints = 1_000_000_000;
    private const uint PhysicalDragonSwingInitialDodge = 40;

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalDragonSwingPassingRollZeroesDamageMutatesDodgeAndOrdersEffectBeforeOuterAnimation(
        ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalDragonSwingAsync(
            attackSpellId,
            new PhysicalDragonSwingOptions(BaseChance: 30, ForcedRoll: 29));

        AssertPhysicalDragonSwingTriggered(result);
        AssertPhysicalDragonSwingEffectBeforeOuterAnimation(result.CasterSent, attackSpellId);
        AssertPhysicalDragonSwingEffectBeforeOuterAnimation(result.TargetSent, attackSpellId);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalDragonSwingFailingRollLeavesDamageDodgeAndEffectsUnchanged(
        ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalDragonSwingAsync(
            attackSpellId,
            new PhysicalDragonSwingOptions(BaseChance: 30, ForcedRoll: 30));

        AssertPhysicalDragonSwingNotTriggered(result);
        var target = Assert.Single(result.OuterAnimation.Targets);
        Assert.True(target.Damage > 0);
        Assert.Equal(
            PhysicalDragonSwingTargetHitpoints - target.Damage,
            result.Target.Character.MinHitPoints);
    }

    [Theory]
    [InlineData(11_030, true)]
    [InlineData(11_060, true)]
    [InlineData(11_590, true)]
    [InlineData(11_600, true)]
    [InlineData(11_650, true)]
    [InlineData(11_660, true)]
    [InlineData(11_029, false)]
    [InlineData(11_031, false)]
    [InlineData(11_659, false)]
    [InlineData(11_661, false)]
    public async Task ShurikenVortexDragonSwingSwingingTailBonusAllowsOnlyMatchingPhysicalSpellIds(
        ushort physicalSpellId,
        bool expectedAvoidance)
    {
        var result = await RunAutomaticPhysicalDragonSwingAsync(
            ShurikenVortexSpellId,
            new PhysicalDragonSwingOptions(
                BaseChance: 1,
                ForcedRoll: 20,
                ShurikenPhysicalSpellId: physicalSpellId,
                EquipSwingingTailRune: true));

        Assert.Equal(expectedAvoidance, HasPhysicalDragonSwingEffect(result));
        if (expectedAvoidance)
        {
            AssertPhysicalDragonSwingTriggered(result);
        }
        else
        {
            AssertPhysicalDragonSwingNotTriggered(result);
        }
    }

    [Theory]
    [InlineData("eligible", true)]
    [InlineData("active-alternate", true)]
    [InlineData("inactive-alternate", false)]
    [InlineData("wrong-job", false)]
    [InlineData("wrong-rune", false)]
    [InlineData("low-reborn", false)]
    [InlineData("low-level", false)]
    public async Task ShurikenVortexDragonSwingSwingingTailRequiresEligibleEquippedRune(
        string scenario,
        bool expectedAvoidance)
    {
        var options = new PhysicalDragonSwingOptions(
            BaseChance: 1,
            ForcedRoll: 20,
            ShurikenPhysicalSpellId: 11_030,
            EquipSwingingTailRune: true);
        options = scenario switch
        {
            "eligible" => options,
            "active-alternate" => options with
            {
                SwingingTailRunePosition = 121,
                AlternateEquipmentActive = true,
            },
            "inactive-alternate" => options with { SwingingTailRunePosition = 121 },
            "wrong-job" => options with { TargetClassCode = 142 },
            "wrong-rune" => options with { SwingingTailRuneName = "OtherRune" },
            "low-reborn" => options with { TargetReborn = 1 },
            "low-level" => options with { TargetLevel = 14 },
            _ => throw new InvalidOperationException($"Unknown rune scenario '{scenario}'."),
        };

        var result = await RunAutomaticPhysicalDragonSwingAsync(ShurikenVortexSpellId, options);

        Assert.Equal(expectedAvoidance, HasPhysicalDragonSwingEffect(result));
        if (expectedAvoidance)
        {
            AssertPhysicalDragonSwingTriggered(result);
        }
        else
        {
            AssertPhysicalDragonSwingNotTriggered(result);
        }
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalDragonSwingRequiresTargetStatusFlag(ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalDragonSwingAsync(
            attackSpellId,
            new PhysicalDragonSwingOptions(
                HasDragonSwingFlag: false,
                BaseChance: 100,
                ForcedRoll: 0));

        AssertPhysicalDragonSwingNotTriggered(result);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId, LegacyShurikenVortexFlag)]
    [InlineData(ManiacDanceRuntimeSpellId, ManiacDanceRuntimeFlag)]
    public async Task AutomaticPhysicalDragonSwingStatusDefenseEarlyReturnSkipsTail(
        ushort attackSpellId,
        int defenderStatusFlag)
    {
        var result = await RunAutomaticPhysicalDragonSwingAsync(
            attackSpellId,
            new PhysicalDragonSwingOptions(
                BaseChance: 100,
                ForcedRoll: 0,
                DefenderStatusFlag: defenderStatusFlag));

        AssertPhysicalDragonSwingNotTriggered(result);
        var target = Assert.Single(result.OuterAnimation.Targets);
        var expectedDamage = ApplyPhysicalTailCallerDamage(attackSpellId, 1);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(
            PhysicalDragonSwingTargetHitpoints - expectedDamage,
            result.Target.Character.MinHitPoints);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalDragonSwingWildPhoenixEarlyReturnSkipsTail(ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalDragonSwingAsync(
            attackSpellId,
            new PhysicalDragonSwingOptions(
                BaseChance: 100,
                ForcedRoll: 0,
                WildPhoenixDefended: true));

        AssertPhysicalDragonSwingNotTriggered(result);
        var target = Assert.Single(result.OuterAnimation.Targets);
        var expectedDamage = ApplyPhysicalTailCallerDamage(attackSpellId, 1);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(
            PhysicalDragonSwingTargetHitpoints - expectedDamage,
            result.Target.Character.MinHitPoints);
        Assert.Equal(
            "1",
            result.Target.Character.Attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
    }

    [Theory]
    [InlineData(ShurikenVortexSpellId)]
    [InlineData(ManiacDanceRuntimeSpellId)]
    public async Task AutomaticPhysicalDragonSwingRunsBeforeEligibleHeavensWonderBackDamageAndOuterAnimation(
        ushort attackSpellId)
    {
        var result = await RunAutomaticPhysicalDragonSwingAsync(
            attackSpellId,
            new PhysicalDragonSwingOptions(
                BaseChance: 100,
                ForcedRoll: 0,
                EnableHeavensWonderBackDamage: true));

        AssertPhysicalDragonSwingTriggered(result);
        var effectIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacyStringPacket.TryParse(frame, out var effect) &&
                IsPhysicalDragonSwingEffect(effect));
        var backDamageIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.AttackerUid == TargetPlayerUid &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var outerAnimationIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.AttackerUid == PlayerUid &&
                animation.SpellId == attackSpellId);
        Assert.True(effectIndex < backDamageIndex);
        Assert.True(backDamageIndex < outerAnimationIndex);
    }

    private static async Task<PhysicalDragonSwingRunResult> RunAutomaticPhysicalDragonSwingAsync(
        ushort attackSpellId,
        PhysicalDragonSwingOptions options)
    {
        var isShuriken = attackSpellId == ShurikenVortexSpellId;
        Assert.True(isShuriken || attackSpellId == ManiacDanceRuntimeSpellId);
        var now = DateTimeOffset.Parse("2026-07-19T20:00:00Z", CultureInfo.InvariantCulture);

        var runeCatalogItems = new List<ItemType>();
        var runeItems = new List<PlayerItemSnapshotItem>();
        if (options.EquipSwingingTailRune)
        {
            runeCatalogItems.Add(CreatePhysicalTailRuneItemType(
                PhysicalDragonSwingRuneItemId,
                options.SwingingTailRuneName,
                options.RuneRequiredClass));
            runeItems.Add(new PlayerItemSnapshotItem
            {
                ItemId = PhysicalDragonSwingRuneItemId,
                Position = options.SwingingTailRunePosition,
            });
        }

        var magicRows = new List<MagicType>();
        if (isShuriken)
        {
            magicRows.Add(CreateShurikenVortexAutomaticContractRow());
            magicRows.Add(CreatePhysicalDragonSwingShurikenDamageRow(options.ShurikenPhysicalSpellId));
        }
        else
        {
            magicRows.Add(CreateManiacDanceRuntimeRow());
        }

        if (options.EnableHeavensWonderBackDamage)
        {
            magicRows.Add(CreateCounterKillRuntimeRow(
                HeavensWonderRuntimeSpellId,
                coolDown: 0,
                coldTime: 1_000));
        }

        var worldData = CreateMagicCatalogWithItems(
            CreateHighLifeMonsterFamily(),
            [],
            runeCatalogItems,
            magicRows.ToArray());
        var casterAttributes = isShuriken
            ? CreateActiveShurikenVortexContractAttributes(
                now.AddSeconds(10),
                now.AddTicks(-1))
            : CreateActiveManiacDanceAttributes(
                now.AddSeconds(10),
                now.AddTicks(-1));
        var caster = CreateSession(
            strength: 100_000,
            minHitPoints: PhysicalDragonSwingTargetHitpoints,
            spellIds: [attackSpellId],
            attributes: casterAttributes);

        var targetFlags = new List<int>();
        if (options.HasDragonSwingFlag)
        {
            targetFlags.Add(PhysicalDragonSwingStatusFlag);
        }
        if (options.DefenderStatusFlag > 0)
        {
            targetFlags.Add(options.DefenderStatusFlag);
        }
        if (options.WildPhoenixDefended)
        {
            targetFlags.Add(LegacyWildPhoenixFlag);
        }

        var targetAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(targetFlags.ToArray()),
            [WorldPacketHandlers.RuntimeLegacyDragonSwingChanceAttribute] =
                options.BaseChance.ToString(CultureInfo.InvariantCulture),
            [PhysicalDragonSwingRollAttribute] = options.ForcedRoll.ToString(CultureInfo.InvariantCulture),
            ["Dodge"] = options.InitialDodge.ToString(CultureInfo.InvariantCulture),
            [CounterKill16350BackDamageRollAttribute] = "0",
            [ManiacDanceHeavensWonderRollAttribute] = "0",
        };
        if (options.WildPhoenixDefended)
        {
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            targetAttributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = "2";
        }

        var targetSpellIds = new List<ushort>();
        if (options.WildPhoenixDefended)
        {
            targetSpellIds.Add(WildPhoenixSpellId);
        }
        if (options.EnableHeavensWonderBackDamage)
        {
            targetSpellIds.Add(HeavensWonderRuntimeSpellId);
        }

        var target = CreateSession(
            uid: TargetPlayerUid,
            name: "PhysicalDragonSwingTarget",
            x: 831,
            y: 584,
            minHitPoints: PhysicalDragonSwingTargetHitpoints,
            spellIds: targetSpellIds,
            attributes: targetAttributes,
            classCode: options.TargetClassCode,
            reborn: options.TargetReborn,
            playerItems: CreatePlayerItems(runeItems.ToArray()));
        target = target with
        {
            Character = target.Character with { Level = options.TargetLevel },
            AlternateEquipmentActive = options.AlternateEquipmentActive,
        };

        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            caster,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore());
        var sentAttack = isShuriken
            ? await WorldPacketHandlers.TrySendDueLegacyShurikenVortexAttackAsync(
                context,
                now,
                CancellationToken.None)
            : await WorldPacketHandlers.TrySendDueLegacyManiacDanceAttackAsync(
                context,
                now,
                CancellationToken.None);
        Assert.True(sentAttack, log.ToString());

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        var outerAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            animation => animation.AttackerUid == PlayerUid && animation.SpellId == attackSpellId);
        return new PhysicalDragonSwingRunResult(
            attackSpellId,
            outerAnimation,
            finalCaster,
            finalTarget,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static MagicType CreatePhysicalDragonSwingShurikenDamageRow(ushort physicalSpellId)
    {
        var row = CreateShurikenVortexDamageRow();
        var fields = row.Fields.ToArray();
        fields[1] = physicalSpellId.ToString(CultureInfo.InvariantCulture);
        return new MagicType(row.Id, physicalSpellId, row.Name, fields);
    }

    private static void AssertPhysicalDragonSwingTriggered(PhysicalDragonSwingRunResult result)
    {
        var target = Assert.Single(result.OuterAnimation.Targets);
        Assert.Equal(0U, target.Damage);
        Assert.Equal(PhysicalDragonSwingTargetHitpoints, result.Target.Character.MinHitPoints);
        Assert.Equal(
            (PhysicalDragonSwingInitialDodge + 30).ToString(CultureInfo.InvariantCulture),
            result.Target.Character.Attributes["Dodge"]);
        Assert.Single(ParseLegacyStrings(result.CasterSent), IsPhysicalDragonSwingEffect);
        Assert.Single(ParseLegacyStrings(result.TargetSent), IsPhysicalDragonSwingEffect);
    }

    private static void AssertPhysicalDragonSwingNotTriggered(PhysicalDragonSwingRunResult result)
    {
        Assert.Equal(
            PhysicalDragonSwingInitialDodge.ToString(CultureInfo.InvariantCulture),
            result.Target.Character.Attributes["Dodge"]);
        Assert.DoesNotContain(
            ParseLegacyStrings(result.CasterSent.Concat(result.TargetSent)),
            IsPhysicalDragonSwingEffect);
    }

    private static bool HasPhysicalDragonSwingEffect(PhysicalDragonSwingRunResult result)
    {
        return ParseLegacyStrings(result.CasterSent).Any(IsPhysicalDragonSwingEffect);
    }

    private static bool IsPhysicalDragonSwingEffect(LegacyStringPacket packet)
    {
        return packet.Uid == TargetPlayerUid &&
            packet.Id == LegacyStringId.Effect &&
            packet.Strings.SequenceEqual([PhysicalDragonSwingEffect]);
    }

    private static void AssertPhysicalDragonSwingEffectBeforeOuterAnimation(
        IReadOnlyList<byte[]> frames,
        ushort attackSpellId)
    {
        var effectIndex = SingleFrameIndex(
            frames,
            frame => LegacyStringPacket.TryParse(frame, out var effect) &&
                IsPhysicalDragonSwingEffect(effect));
        var animationIndex = SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.AttackerUid == PlayerUid &&
                animation.SpellId == attackSpellId);
        Assert.True(effectIndex < animationIndex);
    }

    private sealed record PhysicalDragonSwingOptions(
        bool HasDragonSwingFlag = true,
        uint BaseChance = 30,
        uint ForcedRoll = 29,
        uint InitialDodge = PhysicalDragonSwingInitialDodge,
        ushort ShurikenPhysicalSpellId = ShurikenVortexDamageTypeId,
        bool EquipSwingingTailRune = false,
        string SwingingTailRuneName = "SwingingTail",
        ushort SwingingTailRunePosition = 101,
        uint RuneRequiredClass = 16_000,
        bool AlternateEquipmentActive = false,
        long TargetReborn = 2,
        long TargetLevel = 120,
        int TargetClassCode = 16_000,
        int DefenderStatusFlag = 0,
        bool WildPhoenixDefended = false,
        bool EnableHeavensWonderBackDamage = false);

    private sealed record PhysicalDragonSwingRunResult(
        ushort AttackSpellId,
        LegacySpellAnimationPacket OuterAnimation,
        WorldSession Caster,
        WorldSession Target,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
