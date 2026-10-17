using System.Globalization;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task HandlerShurikenVortexActivePlayerDefensiveStanceHalvesBeforeSixthAndSendsBlockEffect()
    {
        var result = await RunShurikenVortexDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false);

        Assert.True(
            result.RawPhysicalDamage >= 12,
            "The fixture must keep the defensive half and Shuriken /6 observable.");
        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ShurikenVortexSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var expectedAfterStance = result.RawPhysicalDamage / 2;
        var expectedAfterNoBreakthrough = ApplyExpectedShurikenVortexNoBreakthroughBonus(
            expectedAfterStance);

        Assert.Equal(0U, result.AttackerBreakthroughIndex);
        Assert.Equal(expectedAfterNoBreakthrough / 6, target.Damage);
        Assert.NotEqual(expectedAfterStance / 6, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.True(target.Hit);
        Assert.Equal(100_000L - target.Damage, result.TargetRemainingHitpoints);
    }

    [Fact]
    public async Task HandlerShurikenVortexDefensiveStanceStillCastsHeavensWonderBeforeIncomingAnimation()
    {
        var result = await RunShurikenVortexDefensiveStancePlayerAttackAsync(
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
            animation => animation.SpellId == ShurikenVortexSpellId);
        var incomingTarget = Assert.Single(
            incoming.Targets,
            target => target.Uid == TargetPlayerUid);
        var expectedAfterNoBreakthrough = ApplyExpectedShurikenVortexNoBreakthroughBonus(
            result.RawPhysicalDamage / 2);
        Assert.Equal(expectedAfterNoBreakthrough / 6, incomingTarget.Damage);
        Assert.Equal(MsgAttackEffect.Block, incomingTarget.Effect);

        var procIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            result.TargetSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == ShurikenVortexSpellId &&
                animation.AttackerUid == PlayerUid);
        Assert.True(
            procIndex < incomingIndex,
            "Physical.OnPlayer must finish the nested BackDmg cast before CombatGearAttack sends the incoming Shuriken animation.");
        Assert.Contains(HeavensWonderRuntimeSpellId, result.TargetMagicCooldowns.Keys);
    }

    [Fact]
    public async Task HandlerShurikenVortexInfinityMultipliesAfterDefensiveStanceBeforeCallerSixth()
    {
        const uint infinitySpellDamagePercent = 175;
        var result = await RunShurikenVortexDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            infinitySpellDamagePercent);

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ShurikenVortexSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var expectedAfterStance = result.RawPhysicalDamage / 2;
        var expectedAfterInfinity = expectedAfterStance * infinitySpellDamagePercent / 100;
        var expectedAfterNoBreakthrough = ApplyExpectedShurikenVortexNoBreakthroughBonus(
            expectedAfterInfinity);

        Assert.Equal(0U, result.AttackerBreakthroughIndex);
        Assert.Equal(expectedAfterNoBreakthrough / 6, target.Damage);
        Assert.NotEqual(expectedAfterInfinity / 6, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(100_000L - target.Damage, result.TargetRemainingHitpoints);
    }

    [Fact]
    public async Task HandlerShurikenVortexNonzeroBreakthroughFailedRollAppliesFiftyPercentBeforeCallerSixth()
    {
        var result = await RunShurikenVortexDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            breakthroughFixture: new PhysicalBreakthroughRuntimeFixture(
                AttackerBreakthroughIndex: 1,
                RefineryRoll: 101));

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ShurikenVortexSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var expectedAfterStance = result.RawPhysicalDamage / 2;
        var expectedAfterFailedBreakthrough = ApplyExpectedShurikenVortexNoBreakthroughBonus(
            expectedAfterStance);

        Assert.Equal(1U, result.AttackerBreakthroughIndex);
        Assert.Equal(0U, result.TargetCounteractionIndex);
        Assert.Equal(expectedAfterFailedBreakthrough / 6, target.Damage);
        Assert.NotEqual(expectedAfterStance / 6, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.False(target.Effect.HasFlag(MsgAttackEffect.Penetration));
    }

    [Fact]
    public async Task HandlerShurikenVortexIronShieldAbsorbsBeforeCallerSixthAndRetainsRemainingShield()
    {
        const uint ironShieldDamage = 10_000;
        var result = await RunShurikenVortexDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            ironShieldDamage: ironShieldDamage);

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ShurikenVortexSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var damageBeforeShield = ApplyExpectedShurikenVortexNoBreakthroughBonus(
            result.RawPhysicalDamage / 2);
        Assert.InRange(damageBeforeShield, 1U, ironShieldDamage);

        var blueDamage = AssertSingleShurikenVortexBlueDamage(result.TargetSent);
        Assert.Equal(PlayerUid, blueDamage.Query.Uid);
        Assert.Equal(TargetPlayerUid, blueDamage.Query.OpponentUid);
        Assert.Equal(damageBeforeShield, unchecked((uint)blueDamage.Query.Damage));
        Assert.Equal(0U, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(100_000L, result.TargetRemainingHitpoints);
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
                animation.SpellId == ShurikenVortexSpellId &&
                animation.AttackerUid == PlayerUid);
        Assert.True(blueDamageIndex < incomingIndex);
    }

    [Fact]
    public async Task HandlerShurikenVortexIronShieldBreaksBeforeCallerSixthAndPassesOverflowDamage()
    {
        const uint ironShieldDamage = 12;
        var result = await RunShurikenVortexDefensiveStancePlayerAttackAsync(
            learnHeavensWonder: false,
            ironShieldDamage: ironShieldDamage);

        var incoming = Assert.Single(
            ParseSpellAnimations(result.TargetSent),
            animation => animation.SpellId == ShurikenVortexSpellId);
        var target = Assert.Single(incoming.Targets, target => target.Uid == TargetPlayerUid);
        var damageBeforeShield = ApplyExpectedShurikenVortexNoBreakthroughBonus(
            result.RawPhysicalDamage / 2);
        Assert.True(damageBeforeShield > ironShieldDamage);
        var expectedDamage = (damageBeforeShield - ironShieldDamage) / 6;

        var blueDamage = AssertSingleShurikenVortexBlueDamage(result.TargetSent);
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
                animation.SpellId == ShurikenVortexSpellId &&
                animation.AttackerUid == PlayerUid);
        Assert.True(blueDamageIndex < statusRemovalIndex);
        Assert.True(statusRemovalIndex < incomingIndex);
    }

    private static async Task<ShurikenVortexDefensiveStancePlayerResult>
        RunShurikenVortexDefensiveStancePlayerAttackAsync(
            bool learnHeavensWonder,
            uint? infinitySpellDamagePercent = null,
            uint? ironShieldDamage = null,
            PhysicalBreakthroughRuntimeFixture? breakthroughFixture = null)
    {
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
            CreateShurikenVortexRow(),
            CreateShurikenVortexDamageRow(),
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
        var casterStatusFlags = new List<int> { LegacyShurikenVortexFlag };
        if (infinitySpellDamagePercent is not null)
        {
            casterStatusFlags.Add(InfinityStatusFlag);
        }
        if (breakthroughFixture is { AttackerExclusionStatusFlag: > 0 })
        {
            casterStatusFlags.Add(breakthroughFixture.AttackerExclusionStatusFlag);
        }
        var casterAttributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(casterStatusFlags.ToArray()),
        };
        if (infinitySpellDamagePercent is { } infinityPercent)
        {
            casterAttributes["InfinitySDamage"] = infinityPercent.ToString(CultureInfo.InvariantCulture);
            casterAttributes["InfinityDamage"] = infinityPercent.ToString(CultureInfo.InvariantCulture);
        }
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
            spellIds: [ShurikenVortexSpellId],
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
        if (ironShieldDamage is not null)
        {
            targetStatusFlags.Add(IronShieldStatusFlag);
        }
        var targetAttributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(targetStatusFlags.ToArray()),
            [CounterKill16350BackDamageRollAttribute] = 0.ToString(CultureInfo.InvariantCulture),
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
        if (ironShieldDamage is { } shieldDamage)
        {
            targetAttributes["IronShieldDamage"] = shieldDamage.ToString(CultureInfo.InvariantCulture);
            targetAttributes["LegacyIronShieldExpiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O");
            targetAttributes["LegacyIronShieldRemoveOnDead"] = "true";
        }
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "ShurikenDefensiveStanceTarget",
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

        Assert.True(LegacyMagicRuntime.TryResolveSpellByRowId(
            worldData,
            ShurikenVortexDamageRowId,
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

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                casterSession,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates,
                SobNpcStates: new SobNpcRuntimeStateStore()),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: 0,
                X: checked((ushort)casterSession.Character.X),
                Y: checked((ushort)casterSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ShurikenVortexSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var finalTargetSession));
        Assert.NotNull(finalTargetSession);
        return new ShurikenVortexDefensiveStancePlayerResult(
            monster.Uid,
            rawPhysicalDamage,
            attackerBreakthroughIndex,
            targetCounteractionIndex,
            finalTargetSession.Character.MinHitPoints,
            finalTargetSession.Character.Attributes,
            finalTargetSession.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>(),
            targetSent);
    }

    private static MsgAttackPacket AssertSingleShurikenVortexBlueDamage(IReadOnlyList<byte[]> frames)
    {
        var frame = Assert.Single(
            frames,
            candidate => MsgAttackPacket.TryParse(ReadFrame(candidate), out var attack) &&
                attack.Query.AttackType == MsgAttackType.BlueDamage);
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(frame), out var packet));
        return packet;
    }

    private static uint ApplyExpectedShurikenVortexNoBreakthroughBonus(uint damage)
    {
        return damage + (damage * 50 / 100);
    }

    private sealed record ShurikenVortexDefensiveStancePlayerResult(
        uint MonsterUid,
        uint RawPhysicalDamage,
        uint AttackerBreakthroughIndex,
        uint TargetCounteractionIndex,
        long TargetRemainingHitpoints,
        IReadOnlyDictionary<string, string> TargetAttributes,
        IReadOnlyDictionary<ushort, DateTimeOffset> TargetMagicCooldowns,
        IReadOnlyList<byte[]> TargetSent);
}
