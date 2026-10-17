using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const string ManiacDanceHeavensWonderRollAttribute =
        "LegacyHeavensWonderBackDamageRoll";

    [Fact]
    public async Task DueManiacDance12700PlayerHitDefensiveStanceHalvesDamageAndSendsBlockEffect()
    {
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false);

        Assert.True(
            result.RawPhysicalDamage >= 2,
            "The fixture must keep DefensiveStance's integer half observable.");
        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ManiacDanceRuntimeSpellId);
        Assert.Equal(PlayerUid, incoming.AttackerUid);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var expectedAfterStance = result.RawPhysicalDamage / 2;
        var expectedAfterNoBreakthrough = ApplyExpectedManiacDanceNoBreakthroughBonus(
            expectedAfterStance);

        Assert.Equal(0U, result.AttackerBreakthroughIndex);
        Assert.Equal(expectedAfterNoBreakthrough, target.Damage);
        Assert.NotEqual(expectedAfterStance, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.True(target.Hit);
        Assert.Equal(100_000L - target.Damage, result.TargetRemainingHitpoints);
    }

    [Fact]
    public async Task DueManiacDance12700DefensiveStanceStillCastsHeavensWonderBeforeIncomingAnimation()
    {
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: true);
        var targetAnimations = ParseSpellAnimations(result.TargetSent);

        var proc = Assert.Single(
            targetAnimations,
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, proc.AttackerUid);
        Assert.Equal(0U, proc.AttackedUid);
        Assert.Equal(0, proc.X);
        Assert.Equal(0, proc.Y);
        Assert.Equal(
            [result.MonsterUid, PlayerUid],
            proc.Targets.Select(target => target.Uid).ToArray());

        var incoming = Assert.Single(
            targetAnimations,
            animation => animation.SpellId == ManiacDanceRuntimeSpellId);
        var incomingTarget = Assert.Single(
            incoming.Targets,
            target => target.Uid == TargetPlayerUid);
        var expectedAfterNoBreakthrough = ApplyExpectedManiacDanceNoBreakthroughBonus(
            result.RawPhysicalDamage / 2);
        Assert.Equal(expectedAfterNoBreakthrough, incomingTarget.Damage);
        Assert.Equal(MsgAttackEffect.Block, incomingTarget.Effect);

        AssertManiacDanceHeavensWonderAnimationOrder(result.TargetSent);
        AssertManiacDanceHeavensWonderAnimationOrder(result.CasterSent);
        Assert.Contains(HeavensWonderRuntimeSpellId, result.TargetMagicCooldowns.Keys);
    }

    [Fact]
    public async Task DueManiacDance12700InfinityMultipliesAfterDefensiveStance()
    {
        const uint infinitySpellDamagePercent = 175;
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            infinitySpellDamagePercent);

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ManiacDanceRuntimeSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var expectedAfterStance = result.RawPhysicalDamage / 2;
        var expectedAfterInfinity = expectedAfterStance * infinitySpellDamagePercent / 100;
        var expectedAfterNoBreakthrough = ApplyExpectedManiacDanceNoBreakthroughBonus(
            expectedAfterInfinity);

        Assert.Equal(0U, result.AttackerBreakthroughIndex);
        Assert.Equal(expectedAfterNoBreakthrough, target.Damage);
        Assert.NotEqual(expectedAfterInfinity, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(100_000L - target.Damage, result.TargetRemainingHitpoints);
    }

    [Fact]
    public async Task DueManiacDance12700NonzeroBreakthroughFailedRollAppliesFiftyPercent()
    {
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            breakthroughFixture: new PhysicalBreakthroughRuntimeFixture(
                AttackerBreakthroughIndex: 1,
                RefineryRoll: 101));

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ManiacDanceRuntimeSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var expectedAfterStance = result.RawPhysicalDamage / 2;
        var expectedAfterFailedBreakthrough = ApplyExpectedManiacDanceNoBreakthroughBonus(
            expectedAfterStance);

        Assert.Equal(1U, result.AttackerBreakthroughIndex);
        Assert.Equal(0U, result.TargetCounteractionIndex);
        Assert.Equal(expectedAfterFailedBreakthrough, target.Damage);
        Assert.NotEqual(expectedAfterStance, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.False(target.Effect.HasFlag(MsgAttackEffect.Penetration));
    }

    [Fact]
    public async Task DueManiacDance12700IronShieldAbsorbsBeforeBackDamageAndRetainsRemainingShield()
    {
        const uint ironShieldDamage = 10_000;
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            ironShieldDamage: ironShieldDamage);

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ManiacDanceRuntimeSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var damageBeforeShield = ApplyExpectedManiacDanceNoBreakthroughBonus(
            result.RawPhysicalDamage / 2);
        Assert.InRange(damageBeforeShield, 1U, ironShieldDamage);
        Assert.NotEqual(result.RawPhysicalDamage / 2, damageBeforeShield);

        var blueDamage = AssertSingleManiacDanceBlueDamage(result.TargetSent);
        Assert.Equal(PlayerUid, blueDamage.Query.Uid);
        Assert.Equal(TargetPlayerUid, blueDamage.Query.OpponentUid);
        Assert.Equal(damageBeforeShield, unchecked((uint)blueDamage.Query.Damage));
        Assert.Equal(1U, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(99_999L, result.TargetRemainingHitpoints);
        Assert.Equal(
            (ironShieldDamage - damageBeforeShield).ToString(CultureInfo.InvariantCulture),
            result.TargetAttributes["IronShieldDamage"]);
        Assert.True(HasLegacyStatusFlag(result.TargetAttributes, IronShieldStatusFlag));

        var blueDamageIndex = SingleFrameIndex(
            result.TargetSent,
            frame => MsgAttackPacket.TryParse(frame, out var attack) &&
                attack.Query.AttackType == MsgAttackType.BlueDamage);
        var incomingIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == ManiacDanceRuntimeSpellId &&
                animation.AttackerUid == PlayerUid);
        Assert.True(blueDamageIndex < incomingIndex);
    }

    [Fact]
    public async Task DueManiacDance12700IronShieldEqualDamageRetainsActiveZeroShieldAndDealsOne()
    {
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            ironShieldMatchesIncomingDamage: true);

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ManiacDanceRuntimeSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var damageBeforeShield = ApplyExpectedManiacDanceNoBreakthroughBonus(
            result.RawPhysicalDamage / 2);
        Assert.NotEqual(0U, damageBeforeShield);

        var blueDamage = AssertSingleManiacDanceBlueDamage(result.TargetSent);
        Assert.Equal(damageBeforeShield, unchecked((uint)blueDamage.Query.Damage));
        Assert.Equal(1U, target.Damage);
        Assert.Equal(99_999L, result.TargetRemainingHitpoints);
        Assert.Equal("0", result.TargetAttributes["IronShieldDamage"]);
        Assert.True(HasLegacyStatusFlag(result.TargetAttributes, IronShieldStatusFlag));
        Assert.DoesNotContain(
            result.TargetSent,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, IronShieldStatusFlag)));
    }

    [Fact]
    public async Task DueManiacDance12700IronShieldBreaksAndPassesOnlyOverflowDamage()
    {
        const uint ironShieldDamage = 100;
        var result = await RunManiacDanceDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            ironShieldDamage: ironShieldDamage);

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ManiacDanceRuntimeSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var damageBeforeShield = ApplyExpectedManiacDanceNoBreakthroughBonus(
            result.RawPhysicalDamage / 2);
        Assert.True(damageBeforeShield > ironShieldDamage);
        var expectedDamage = damageBeforeShield - ironShieldDamage;

        var blueDamage = AssertSingleManiacDanceBlueDamage(result.TargetSent);
        Assert.Equal(ironShieldDamage, unchecked((uint)blueDamage.Query.Damage));
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(100_000L - expectedDamage, result.TargetRemainingHitpoints);
        Assert.False(result.TargetAttributes.ContainsKey("IronShieldDamage"));
        Assert.False(result.TargetAttributes.ContainsKey("LegacyIronShieldExpiresAtUtc"));
        Assert.False(result.TargetAttributes.ContainsKey("LegacyIronShieldRemoveOnDead"));
        Assert.False(HasLegacyStatusFlag(result.TargetAttributes, IronShieldStatusFlag));

        var blueDamageIndex = SingleFrameIndex(
            result.TargetSent,
            frame => MsgAttackPacket.TryParse(frame, out var attack) &&
                attack.Query.AttackType == MsgAttackType.BlueDamage);
        var statusRemovalIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    !IsLegacyStatusFlagSet(entry.Values, IronShieldStatusFlag)));
        var incomingIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == ManiacDanceRuntimeSpellId &&
                animation.AttackerUid == PlayerUid);
        Assert.True(blueDamageIndex < statusRemovalIndex);
        Assert.True(statusRemovalIndex < incomingIndex);
    }

    private static async Task<ManiacDanceDefensiveStancePlayerResult>
        RunManiacDanceDefensiveStancePlayerAttackAsync(
            bool learnHeavensWonder,
            uint? infinitySpellDamagePercent = null,
            uint? ironShieldDamage = null,
            bool ironShieldMatchesIncomingDamage = false,
            PhysicalBreakthroughRuntimeFixture? breakthroughFixture = null)
    {
        var now = DateTimeOffset.Parse(
            "2026-07-19T19:00:00Z",
            CultureInfo.InvariantCulture);
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [spawn],
            CreatePhysicalBreakthroughRuneCatalogItems(breakthroughFixture),
            CreateManiacDanceRuntimeRow(),
            CreateCounterKillRuntimeRow(
                HeavensWonderRuntimeSpellId,
                coolDown: 0,
                coldTime: 1_000));
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterAttributes = CreateActiveManiacDanceAttributes(
            now.AddSeconds(10),
            nextAttackAt: now.AddTicks(-1));
        var casterStatusFlags = new List<int> { ManiacDanceRuntimeFlag };
        if (infinitySpellDamagePercent is { } infinityPercent)
        {
            casterStatusFlags.Add(InfinityStatusFlag);
            casterAttributes["InfinitySDamage"] = infinityPercent.ToString(CultureInfo.InvariantCulture);
            casterAttributes["InfinityDamage"] = infinityPercent.ToString(CultureInfo.InvariantCulture);
        }
        if (breakthroughFixture is { AttackerExclusionStatusFlag: > 0 })
        {
            casterStatusFlags.Add(breakthroughFixture.AttackerExclusionStatusFlag);
        }
        casterAttributes["StatusFlags"] = BuildStatusFlags(casterStatusFlags.ToArray());
        if (breakthroughFixture is not null)
        {
            casterAttributes["BattlePower"] = breakthroughFixture.AttackerBattlePower
                .ToString(CultureInfo.InvariantCulture);
            casterAttributes[PhysicalBreakthroughRefineryRollAttribute] = breakthroughFixture.RefineryRoll
                .ToString(CultureInfo.InvariantCulture);
            if (breakthroughFixture.AttackerBreakthroughIndex != 0)
            {
                casterAttributes["Dragon"] = CreatePhysicalBreakthroughChiPower(
                    LegacyChiAttributeType.Breakthrough,
                    breakthroughFixture.AttackerBreakthroughIndex);
            }
        }

        var casterSession = CreateSession(
            strength: 1_000,
            minHitPoints: 100_000,
            spellIds: [ManiacDanceRuntimeSpellId],
            attributes: casterAttributes,
            reborn: breakthroughFixture is { TacitStrikeLevel: > 0 }
                ? breakthroughFixture.RuneEligibilityReborn
                : 0,
            playerItems: CreatePhysicalBreakthroughAttackerRuneItems(breakthroughFixture));
        var targetStatusFlags = new List<int>();
        if (breakthroughFixture?.TargetHasDefensiveStance ?? true)
        {
            targetStatusFlags.Add(DefensiveStanceStatusFlag);
        }
        if (ironShieldDamage is not null || ironShieldMatchesIncomingDamage)
        {
            targetStatusFlags.Add(IronShieldStatusFlag);
        }
        var targetAttributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(targetStatusFlags.ToArray()),
            [ManiacDanceHeavensWonderRollAttribute] =
                0.ToString(CultureInfo.InvariantCulture),
        };
        if (breakthroughFixture is not null)
        {
            targetAttributes["BattlePower"] = breakthroughFixture.TargetBattlePower
                .ToString(CultureInfo.InvariantCulture);
            if (breakthroughFixture.TargetCounteractionIndex != 0)
            {
                targetAttributes["Dragon"] = CreatePhysicalBreakthroughChiPower(
                    LegacyChiAttributeType.Counteraction,
                    breakthroughFixture.TargetCounteractionIndex);
            }
        }
        if (ironShieldDamage is not null || ironShieldMatchesIncomingDamage)
        {
            targetAttributes["IronShieldDamage"] = (ironShieldDamage ?? 0U).ToString(CultureInfo.InvariantCulture);
            targetAttributes["LegacyIronShieldExpiresAtUtc"] = now.AddMinutes(1).ToString("O");
            targetAttributes["LegacyIronShieldRemoveOnDead"] = "true";
        }
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "ManiacDanceDefensiveStanceTarget",
            x: 831,
            y: 584,
            minHitPoints: 100_000,
            stamina: 100,
            spellIds: learnHeavensWonder ? [HeavensWonderRuntimeSpellId] : [],
            attributes: targetAttributes,
            reborn: breakthroughFixture is { BarrierLevel: > 0 }
                ? breakthroughFixture.RuneEligibilityReborn
                : 0,
            playerItems: CreatePhysicalBreakthroughTargetRuneItems(breakthroughFixture));
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            casterSession.PlayerSpells,
            worldData,
            ManiacDanceRuntimeSpellId,
            out _,
            out var damageSpell));
        var rawPhysicalDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            casterSession,
            targetSession,
            damageSpell,
            worldData,
            1U);
        var attackerBreakthroughIndex = LegacyPlayerStatusCalculator.CalculateFields(
            casterSession.Character,
            casterSession.PlayerItems,
            worldData,
            maxHitpoints: checked((uint)casterSession.Character.MinHitPoints),
            prestigeLevel: 0,
            combatGear: casterSession.CombatGear,
            alternateEquipmentActive: casterSession.AlternateEquipmentActive,
            playerSpells: casterSession.PlayerSpells)[LegacyPlayerStatusPacket.BreakthroughIndex];
        var targetCounteractionIndex = LegacyPlayerStatusCalculator.CalculateFields(
            targetSession.Character,
            targetSession.PlayerItems,
            worldData,
            maxHitpoints: checked((uint)targetSession.Character.MinHitPoints),
            prestigeLevel: 0,
            combatGear: targetSession.CombatGear,
            alternateEquipmentActive: targetSession.AlternateEquipmentActive,
            playerSpells: targetSession.PlayerSpells)[LegacyPlayerStatusPacket.CounteractionIndex];
        if (ironShieldMatchesIncomingDamage)
        {
            var updatedAttributes = new Dictionary<string, string>(targetSession.Character.Attributes)
            {
                ["IronShieldDamage"] = ApplyExpectedManiacDanceNoBreakthroughBonus(rawPhysicalDamage / 2)
                    .ToString(CultureInfo.InvariantCulture),
            };
            targetSession = targetSession with
            {
                Character = targetSession.Character with { Attributes = updatedAttributes },
            };
            sessions.Set(targetConnection, targetSession);
        }

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: new SobNpcRuntimeStateStore());
        Assert.True(await WorldPacketHandlers.TrySendDueLegacyManiacDanceAttackAsync(
            context,
            now,
            CancellationToken.None));

        Assert.True(sessions.TryGet(targetConnection, out var finalTargetSession));
        Assert.NotNull(finalTargetSession);
        return new ManiacDanceDefensiveStancePlayerResult(
            monster.Uid,
            rawPhysicalDamage,
            attackerBreakthroughIndex,
            targetCounteractionIndex,
            finalTargetSession.Character.MinHitPoints,
            finalTargetSession.Character.Attributes,
            finalTargetSession.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>(),
            casterSent,
            targetSent);
    }

    private static MsgAttackPacket AssertSingleManiacDanceBlueDamage(IReadOnlyList<byte[]> frames)
    {
        var frame = Assert.Single(
            frames,
            candidate => MsgAttackPacket.TryParse(ReadFrame(candidate), out var attack) &&
                attack.Query.AttackType == MsgAttackType.BlueDamage);
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(frame), out var packet));
        return packet;
    }

    private static void AssertManiacDanceHeavensWonderAnimationOrder(IReadOnlyList<byte[]> frames)
    {
        var procIndex = SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            frames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == ManiacDanceRuntimeSpellId &&
                animation.AttackerUid == PlayerUid);
        Assert.True(
            procIndex < incomingIndex,
            "Physical.OnPlayer must finish the nested BackDmg cast before ThreadPool sends the incoming ManiacDance animation.");
    }

    private static uint ApplyExpectedManiacDanceNoBreakthroughBonus(uint damage)
    {
        return damage + (damage * 50 / 100);
    }

    private sealed record ManiacDanceDefensiveStancePlayerResult(
        uint MonsterUid,
        uint RawPhysicalDamage,
        uint AttackerBreakthroughIndex,
        uint TargetCounteractionIndex,
        long TargetRemainingHitpoints,
        IReadOnlyDictionary<string, string> TargetAttributes,
        IReadOnlyDictionary<ushort, DateTimeOffset> TargetMagicCooldowns,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent);
}
