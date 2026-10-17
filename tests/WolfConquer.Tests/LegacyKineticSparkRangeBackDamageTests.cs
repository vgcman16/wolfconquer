using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint KineticSparkRangeTargetUid = TargetPlayerUid + 10;
    private const uint KineticSparkRangeBoundaryUid = TargetPlayerUid + 11;
    private const int KineticSparkDefaultFirstChainBudget = 3;

    [Fact]
    public async Task HandlerKineticSparkRangeUsesPhysicalPrimaryStrictRangeChainAndReceiveBeforeAnimation()
    {
        var result = await RunKineticSparkRangeAttackAsync(
            new KineticSparkRangeTargetOptions(),
            includeBoundaryTarget: true);

        var incoming = AssertKineticSparkRangeAnimation(result);
        Assert.Equal(PlayerUid, incoming.AttackerUid);
        Assert.Equal(0U, incoming.AttackedUid);
        Assert.Equal((832, 584), (incoming.X, incoming.Y));
        Assert.Equal(
            [TargetPlayerUid, KineticSparkRangeTargetUid],
            incoming.Targets.Select(target => target.Uid).ToArray());
        Assert.DoesNotContain(
            incoming.Targets,
            target => target.Uid == KineticSparkRangeBoundaryUid);

        var primary = Assert.Single(
            incoming.Targets,
            target => target.Uid == TargetPlayerUid);
        var chained = Assert.Single(
            incoming.Targets,
            target => target.Uid == KineticSparkRangeTargetUid);
        Assert.Equal(result.ExpectedPrimaryPhysicalDamage, primary.Damage);
        Assert.Equal(result.ExpectedNormalRangeDamage, chained.Damage);
        Assert.NotEqual(primary.Damage, chained.Damage);
        Assert.Equal(
            100_000L - result.ExpectedNormalRangeDamage,
            result.Chained.FinalSession.Character.MinHitPoints);
        Assert.NotNull(result.Boundary);
        Assert.Equal(100_000, result.Boundary.FinalSession.Character.MinHitPoints);

        var receiveIndex = SingleFrameIndex(
            result.Chained.Sent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == KineticSparkRangeTargetUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var animationIndex = SingleFrameIndex(
            result.Chained.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == KineticSparkSpellId);
        Assert.True(
            receiveIndex < animationIndex,
            "KineticSpark.cs applies ReceiveAttack.Player.Execute before sending the shared animation.");
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerKineticSparkChainedRangeStatusReturnUsesExtraAttackWhilePrimaryPhysicalRemainsFixedOne(
        int statusFlag)
    {
        var result = await RunKineticSparkRangeAttackAsync(
            new KineticSparkRangeTargetOptions(
                StatusFlag: statusFlag,
                WildPhoenixCharges: 1,
                LearnsBackDamage: true),
            primaryStatusFlag: statusFlag,
            useRangeStatusDamage: true);

        AssertNoKineticSparkRangeBackDamageProc(result);
        var animation = AssertKineticSparkRangeAnimation(result);
        var primary = Assert.Single(
            animation.Targets,
            target => target.Uid == TargetPlayerUid);
        Assert.Equal(1U, primary.Damage);
        Assert.Equal(MsgAttackEffect.None, primary.Effect);
        Assert.Equal(99_999, result.Primary.FinalSession.Character.MinHitPoints);

        var chained = AssertKineticSparkRangeTarget(result);
        var expectedChainedDamage = ScaleKineticSparkRangeDamage(RangeStatusExpectedExtraAttack);
        Assert.Equal(expectedChainedDamage, chained.Damage);
        Assert.True(chained.Damage > 1);
        Assert.Equal(MsgAttackEffect.None, chained.Effect);
        Assert.Equal(
            100_000L - expectedChainedDamage,
            result.Chained.FinalSession.Character.MinHitPoints);
        Assert.Equal("1", result.Chained.RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerKineticSparkRangeWildPhoenixReturnPrecedesDefensiveStanceAndBackDamage()
    {
        var result = await RunKineticSparkRangeAttackAsync(
            new KineticSparkRangeTargetOptions(
                StatusFlag: DefensiveStanceStatusFlag,
                WildPhoenixCharges: 1,
                LearnsBackDamage: true));

        AssertNoKineticSparkRangeBackDamageProc(result);
        var target = AssertKineticSparkRangeTarget(result);
        Assert.Equal(
            ScaleKineticSparkRangeDamage(1),
            target.Damage);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(100_000, result.Chained.FinalSession.Character.MinHitPoints);
        Assert.Equal("0", result.Chained.RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerKineticSparkRangeDefensiveStanceReturnsBlockWithoutBackDamage()
    {
        var result = await RunKineticSparkRangeAttackAsync(
            new KineticSparkRangeTargetOptions(
                StatusFlag: DefensiveStanceStatusFlag,
                LearnsBackDamage: true));
        var reduction = result.ExpectedRawRangeDamage * 40 / 100;
        var blessedDamage = result.ExpectedRawRangeDamage <= reduction
            ? 1U
            : result.ExpectedRawRangeDamage - reduction;
        var expectedDamage = ScaleKineticSparkRangeDamage(blessedDamage);

        AssertNoKineticSparkRangeBackDamageProc(result);
        var target = AssertKineticSparkRangeTarget(result);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(
            100_000L - expectedDamage,
            result.Chained.FinalSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerKineticSparkRangeBackDamageCastsBeforeIncomingAnimationAndCarriesState()
    {
        var result = await RunKineticSparkRangeAttackAsync(
            new KineticSparkRangeTargetOptions(LearnsBackDamage: true),
            attackerHitpoints: 1);

        var proc = Assert.Single(
            ParseSpellAnimations(result.Chained.Sent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(KineticSparkRangeTargetUid, proc.AttackerUid);
        Assert.Contains(PlayerUid, proc.Targets.Select(target => target.Uid));
        Assert.DoesNotContain(KineticSparkRangeTargetUid, proc.Targets.Select(target => target.Uid));

        var incomingTarget = AssertKineticSparkRangeTarget(result);
        Assert.Equal(result.ExpectedNormalRangeDamage, incomingTarget.Damage);
        Assert.Equal(
            100_000L - result.ExpectedNormalRangeDamage,
            result.Chained.FinalSession.Character.MinHitPoints);

        var procIndex = SingleFrameIndex(
            result.Chained.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            result.Chained.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == KineticSparkSpellId);
        Assert.True(
            procIndex < incomingIndex,
            "Matching Range.OnPlayer invokes BackDmg before KineticSpark sends its shared animation.");

        Assert.Equal(0, result.FinalAttacker.Character.MinHitPoints);
        Assert.Contains(
            HeavensWonderRuntimeSpellId,
            result.Chained.FinalSession.MagicCooldowns?.Keys ?? []);
        Assert.DoesNotContain(
            HeavensWonderRuntimeSpellId,
            result.Primary.FinalSession.MagicCooldowns?.Keys ?? []);
    }

    [Fact]
    public async Task HandlerKineticSparkPrimaryPhysicalBackDamageCastsBeforeIncomingAnimation()
    {
        var result = await RunKineticSparkRangeAttackAsync(
            new KineticSparkRangeTargetOptions(),
            attackerHitpoints: 1,
            primaryLearnsBackDamage: true);

        var proc = Assert.Single(
            ParseSpellAnimations(result.Primary.Sent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, proc.AttackerUid);
        Assert.Contains(PlayerUid, proc.Targets.Select(target => target.Uid));

        var incoming = Assert.Single(
            ParseSpellAnimations(result.Primary.Sent),
            animation => animation.SpellId == KineticSparkSpellId);
        var primaryTarget = Assert.Single(
            incoming.Targets,
            target => target.Uid == TargetPlayerUid);
        Assert.Equal(result.ExpectedPrimaryPhysicalDamage, primaryTarget.Damage);
        Assert.Equal(
            100_000L - result.ExpectedPrimaryPhysicalDamage,
            result.Primary.FinalSession.Character.MinHitPoints);

        var procIndex = SingleFrameIndex(
            result.Primary.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            result.Primary.Sent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == KineticSparkSpellId);
        Assert.True(procIndex < incomingIndex);
        Assert.Equal(0, result.FinalAttacker.Character.MinHitPoints);
        Assert.Contains(
            HeavensWonderRuntimeSpellId,
            result.Primary.FinalSession.MagicCooldowns?.Keys ?? []);
    }

    private static LegacySpellAnimationPacket AssertKineticSparkRangeAnimation(
        KineticSparkRangeAttackResult result)
    {
        return Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == KineticSparkSpellId);
    }

    private static LegacySpellAnimationTarget AssertKineticSparkRangeTarget(
        KineticSparkRangeAttackResult result)
    {
        return Assert.Single(
            AssertKineticSparkRangeAnimation(result).Targets,
            target => target.Uid == KineticSparkRangeTargetUid);
    }

    private static void AssertNoKineticSparkRangeBackDamageProc(
        KineticSparkRangeAttackResult result)
    {
        var frames = result.CasterSent
            .Concat(result.Primary.Sent)
            .Concat(result.Chained.Sent)
            .Concat(result.Boundary?.Sent ?? []);
        Assert.DoesNotContain(
            ParseSpellAnimations(frames),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.False(
            result.Chained.FinalSession.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
    }

    private static async Task<KineticSparkRangeAttackResult> RunKineticSparkRangeAttackAsync(
        KineticSparkRangeTargetOptions chainedOptions,
        bool includeBoundaryTarget = false,
        long attackerHitpoints = 100_000,
        bool primaryLearnsBackDamage = false,
        int? primaryStatusFlag = null,
        bool useRangeStatusDamage = false)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            spawns: [],
            useRangeStatusDamage ? CreateRangeStatusPhysicalDamageItems() : [],
            CreateMagicRow(
                KineticSparkSpellId,
                sort: 67,
                useMana: 0,
                damage: 100,
                maxTargets: 5,
                range: 8,
                coldTime: 0),
            CreateCounterKillRuntimeRow(
                HeavensWonderRuntimeSpellId,
                coolDown: 0,
                coldTime: 1_000),
            CreateMagicRow(
                WildPhoenixSpellId,
                sort: 6,
                useMana: 0,
                damage: 4,
                coldTime: 0,
                duration: 32,
                damageOnHuman: 18,
                damage2: 7));
        var attackerSession = CreateSession(
            x: 830,
            y: 584,
            minHitPoints: attackerHitpoints,
            strength: 1_000,
            stamina: 100,
            spellIds: [KineticSparkSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(KineticSparkFlag),
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                ["LegacyPhysicalLuckyStrikeRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "99",
                ["LegacyPhysicalDashRoll"] = "99",
            },
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalIncreaseItemId, position: 10)
                : PlayerItemSnapshot.Empty);
        var primaryAttributes = new Dictionary<string, string>();
        if (primaryLearnsBackDamage)
        {
            primaryAttributes[CounterKill16350BackDamageRollAttribute] = "0";
        }

        if (primaryStatusFlag is { } statusFlag)
        {
            primaryAttributes["StatusFlags"] = BuildStatusFlags(statusFlag);
        }

        var primarySession = CreateSession(
            uid: TargetPlayerUid,
            name: "KineticSparkPrimary",
            x: 832,
            y: 584,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: primaryLearnsBackDamage ? [HeavensWonderRuntimeSpellId] : [],
            attributes: primaryAttributes.Count == 0 ? null : primaryAttributes,
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalDecreaseItemId, position: 11)
                : PlayerItemSnapshot.Empty);
        var chainedInitial = CreateKineticSparkRangeTargetSession(
            chainedOptions,
            useRangeStatusDamage);
        var boundaryInitial = includeBoundaryTarget
            ? CreateSession(
                uid: KineticSparkRangeBoundaryUid,
                name: "KineticSparkBoundary",
                x: 836,
                y: 584,
                minHitPoints: 100_000,
                strength: 1_000,
                stamina: 100,
                spellIds: [])
            : null;

        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var primarySent = new List<byte[]>();
        var chainedSent = new List<byte[]>();
        var boundarySent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var primaryConnection = CreateSendingConnection(primarySent);
        var chainedConnection = CreateSendingConnection(chainedSent);
        var boundaryConnection = includeBoundaryTarget
            ? CreateSendingConnection(boundarySent)
            : null;
        sessions.Set(casterConnection, attackerSession);
        sessions.Set(primaryConnection, primarySession);
        sessions.Set(chainedConnection, chainedInitial);
        if (boundaryConnection is not null && boundaryInitial is not null)
        {
            sessions.Set(boundaryConnection, boundaryInitial);
        }

        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attackerSession.PlayerSpells,
            worldData,
            KineticSparkSpellId,
            out _,
            out var spell));
        var expectedPrimaryPhysicalDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attackerSession,
            primarySession,
            spell,
            worldData,
            1U);
        var expectedRawRangeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attackerSession,
            chainedInitial,
            spell with { Id = 0 },
            worldData,
            1U);
        var expectedNormalRangeDamage = ScaleKineticSparkRangeDamage(expectedRawRangeDamage);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection,
                log,
                attackerSession,
                WorldData: worldData,
                Sessions: sessions),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 832,
                Y: 584,
                AttackType: MsgAttackType.Archer,
                Damage: 500,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalAttacker));
        Assert.NotNull(finalAttacker);
        Assert.True(sessions.TryGet(primaryConnection, out var finalPrimary));
        Assert.NotNull(finalPrimary);
        Assert.True(sessions.TryGet(chainedConnection, out var finalChained));
        Assert.NotNull(finalChained);
        finalChained.Character.Attributes.TryGetValue(
            RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute,
            out var remainingWildPhoenixCharges);

        KineticSparkRangeTargetRuntime? boundary = null;
        if (boundaryConnection is not null && boundaryInitial is not null)
        {
            Assert.True(sessions.TryGet(boundaryConnection, out var finalBoundary));
            Assert.NotNull(finalBoundary);
            boundary = new KineticSparkRangeTargetRuntime(
                boundaryInitial,
                finalBoundary,
                boundarySent,
                RemainingWildPhoenixCharges: null);
        }

        return new KineticSparkRangeAttackResult(
            casterSent,
            finalAttacker,
            new KineticSparkRangeTargetRuntime(
                primarySession,
                finalPrimary,
                primarySent,
                RemainingWildPhoenixCharges: null),
            new KineticSparkRangeTargetRuntime(
                chainedInitial,
                finalChained,
                chainedSent,
                remainingWildPhoenixCharges),
            boundary,
            expectedPrimaryPhysicalDamage,
            expectedRawRangeDamage,
            expectedNormalRangeDamage,
            log.ToString());
    }

    private static WorldSession CreateKineticSparkRangeTargetSession(
        KineticSparkRangeTargetOptions options,
        bool useRangeStatusDamage)
    {
        var attributes = new Dictionary<string, string>();
        var statusFlags = new List<int>();
        var spellIds = new List<ushort>();
        if (options.StatusFlag is { } statusFlag)
        {
            statusFlags.Add(statusFlag);
        }

        if (options.LearnsBackDamage)
        {
            spellIds.Add(HeavensWonderRuntimeSpellId);
            attributes[CounterKill16350BackDamageRollAttribute] =
                0.ToString(CultureInfo.InvariantCulture);
        }

        if (options.WildPhoenixCharges > 0)
        {
            spellIds.Add(WildPhoenixSpellId);
            statusFlags.Add(LegacyWildPhoenixFlag);
            attributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] =
                options.WildPhoenixCharges.ToString(CultureInfo.InvariantCulture);
        }

        if (statusFlags.Count > 0)
        {
            attributes["StatusFlags"] = BuildStatusFlags(statusFlags.ToArray());
        }

        return CreateSession(
            uid: KineticSparkRangeTargetUid,
            name: "KineticSparkRangeTarget",
            x: 835,
            y: 584,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: spellIds,
            attributes: attributes,
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalDecreaseItemId, position: 11)
                : PlayerItemSnapshot.Empty);
    }

    private static uint ScaleKineticSparkRangeDamage(uint damage)
    {
        return InvokePrivateUInt32(
            "ScaleLegacyKineticSparkChainedDamage",
            damage,
            KineticSparkDefaultFirstChainBudget);
    }

    private sealed record KineticSparkRangeTargetOptions(
        int? StatusFlag = null,
        uint WildPhoenixCharges = 0,
        bool LearnsBackDamage = false);

    private sealed record KineticSparkRangeTargetRuntime(
        WorldSession InitialSession,
        WorldSession FinalSession,
        IReadOnlyList<byte[]> Sent,
        string? RemainingWildPhoenixCharges);

    private sealed record KineticSparkRangeAttackResult(
        IReadOnlyList<byte[]> CasterSent,
        WorldSession FinalAttacker,
        KineticSparkRangeTargetRuntime Primary,
        KineticSparkRangeTargetRuntime Chained,
        KineticSparkRangeTargetRuntime? Boundary,
        uint ExpectedPrimaryPhysicalDamage,
        uint ExpectedRawRangeDamage,
        uint ExpectedNormalRangeDamage,
        string Log);
}
