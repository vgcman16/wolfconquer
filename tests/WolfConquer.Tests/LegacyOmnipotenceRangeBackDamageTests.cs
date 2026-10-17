using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint OmnipotenceSecondTargetUid = TargetPlayerUid + 1;
    private const uint OmnipotenceBoundaryTargetUid = TargetPlayerUid + 2;

    [Fact]
    public async Task HandlerOmnipotenceUsesRangeDamageStrictCategoryOrderAndReceiveProgressBeforeAnimation()
    {
        var result = await RunOmnipotenceRangeAttackAsync(
        [
            new OmnipotenceTargetOptions(TargetPlayerUid, X: 834, Y: 584),
        ]);

        var incoming = AssertOmnipotenceAnimation(result);
        Assert.Equal(
            [result.Monster.Uid, TargetPlayerUid, checked((uint)result.SobNpc.Id)],
            incoming.Targets.Select(target => target.Uid).ToArray());
        Assert.DoesNotContain(
            incoming.Targets,
            target => target.Uid == OmnipotenceBoundaryTargetUid);

        var playerTarget = Assert.Single(
            incoming.Targets,
            target => target.Uid == TargetPlayerUid);
        Assert.Equal(result.ExpectedNormalRangeDamage, playerTarget.Damage);
        Assert.NotEqual(result.LegacyFixedTenPercentDamage, playerTarget.Damage);
        Assert.Equal(MsgAttackEffect.None, playerTarget.Effect);
        Assert.Equal(
            100_000L - result.ExpectedNormalRangeDamage,
            result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);

        var targetFrames = result.Targets[TargetPlayerUid].Sent;
        var receiveIndex = SingleFrameIndex(
            targetFrames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var targetAnimationIndex = SingleFrameIndex(
            targetFrames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == TestOmnipotenceSpellId);
        var progressIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == TestOmnipotenceSpellId);
        var casterAnimationIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == TestOmnipotenceSpellId);

        Assert.True(receiveIndex < targetAnimationIndex);
        Assert.True(progressIndex < casterAnimationIndex);
    }

    [Fact]
    public async Task HandlerOmnipotenceBackDamageCastsOnceBeforeSharedAnimationAndCarriesDeadAttackerState()
    {
        var result = await RunOmnipotenceRangeAttackAsync(
        [
            new OmnipotenceTargetOptions(
                TargetPlayerUid,
                X: 824,
                Y: 584,
                LearnsBackDamage: true),
            new OmnipotenceTargetOptions(
                OmnipotenceSecondTargetUid,
                X: 836,
                Y: 584,
                LearnsBackDamage: true),
        ],
        attackerHitpoints: 1);

        var procs = ParseSpellAnimations(result.CasterSent)
            .Where(animation => animation.SpellId == HeavensWonderRuntimeSpellId)
            .ToArray();
        Assert.True(
            procs.Length == 1,
            "Expected one Heaven's Wonder counter animation; observed " +
            string.Join(
                "; ",
                procs.Select(animation =>
                    $"caster={animation.AttackerUid} targets=[{string.Join(',', animation.Targets.Select(target => target.Uid))}]")) +
            Environment.NewLine + result.Log);
        var proc = procs[0];
        Assert.Contains(PlayerUid, proc.Targets.Select(target => target.Uid));

        var incoming = AssertOmnipotenceAnimation(result);
        Assert.Equal(result.Monster.Uid, incoming.Targets[0].Uid);
        Assert.Equal(checked((uint)result.SobNpc.Id), incoming.Targets[^1].Uid);
        Assert.Equal(
            new[] { TargetPlayerUid, OmnipotenceSecondTargetUid }.Order().ToArray(),
            incoming.Targets
                .Skip(1)
                .SkipLast(1)
                .Select(target => target.Uid)
                .Order()
                .ToArray());
        Assert.All(
            incoming.Targets.Where(target =>
                target.Uid is TargetPlayerUid or OmnipotenceSecondTargetUid),
            target => Assert.Equal(result.ExpectedNormalRangeDamage, target.Damage));

        var procIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == TestOmnipotenceSpellId);
        Assert.True(procIndex < incomingIndex);

        Assert.Equal(0, result.FinalAttacker.Character.MinHitPoints);
        Assert.Equal(
            1,
            result.Targets.Values.Count(target =>
                target.FinalSession.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true));
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerOmnipotenceRangeStatusReturnUsesZeroBaseExtraAttackBeforeWildPhoenixAndBackDamage(
        int statusFlag)
    {
        var result = await RunOmnipotenceRangeAttackAsync(
        [
            new OmnipotenceTargetOptions(
                TargetPlayerUid,
                X: 834,
                Y: 584,
                StatusFlag: statusFlag,
                WildPhoenixCharges: 1,
                LearnsBackDamage: true),
        ],
        useRangeStatusDamage: true);

        AssertNoOmnipotenceBackDamageProc(result);
        var target = AssertOmnipotencePlayerTarget(result, TargetPlayerUid);
        Assert.Equal(RangeStatusExpectedExtraAttack, target.Damage);
        Assert.True(target.Damage > 1);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(
            100_000L - RangeStatusExpectedExtraAttack,
            result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
        Assert.Equal("1", result.Targets[TargetPlayerUid].RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerOmnipotenceWildPhoenixReturnPrecedesDefensiveStanceAndBackDamage()
    {
        var result = await RunOmnipotenceRangeAttackAsync(
        [
            new OmnipotenceTargetOptions(
                TargetPlayerUid,
                X: 834,
                Y: 584,
                StatusFlag: DefensiveStanceStatusFlag,
                WildPhoenixCharges: 1,
                LearnsBackDamage: true),
        ]);

        AssertNoOmnipotenceBackDamageProc(result);
        var target = AssertOmnipotencePlayerTarget(result, TargetPlayerUid);
        Assert.Equal(1U, target.Damage);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(99_999, result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
        Assert.Equal("0", result.Targets[TargetPlayerUid].RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerOmnipotenceDefensiveStanceReturnsBlockWithoutBackDamage()
    {
        var result = await RunOmnipotenceRangeAttackAsync(
        [
            new OmnipotenceTargetOptions(
                TargetPlayerUid,
                X: 834,
                Y: 584,
                StatusFlag: DefensiveStanceStatusFlag,
                LearnsBackDamage: true),
        ]);
        var reduction = result.ExpectedNormalRangeDamage * 40 / 100;
        var expectedDamage = result.ExpectedNormalRangeDamage <= reduction
            ? 1U
            : result.ExpectedNormalRangeDamage - reduction;

        AssertNoOmnipotenceBackDamageProc(result);
        var target = AssertOmnipotencePlayerTarget(result, TargetPlayerUid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(
            100_000L - expectedDamage,
            result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
    }

    private static LegacySpellAnimationPacket AssertOmnipotenceAnimation(
        OmnipotenceRangeAttackResult result)
    {
        return Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == TestOmnipotenceSpellId);
    }

    private static LegacySpellAnimationTarget AssertOmnipotencePlayerTarget(
        OmnipotenceRangeAttackResult result,
        uint uid)
    {
        return Assert.Single(
            AssertOmnipotenceAnimation(result).Targets,
            target => target.Uid == uid);
    }

    private static void AssertNoOmnipotenceBackDamageProc(OmnipotenceRangeAttackResult result)
    {
        Assert.DoesNotContain(
            ParseSpellAnimations(
                result.CasterSent.Concat(result.Targets.Values.SelectMany(target => target.Sent))),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.All(
            result.Targets.Values,
            target => Assert.False(
                target.FinalSession.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true));
    }

    private static async Task<OmnipotenceRangeAttackResult> RunOmnipotenceRangeAttackAsync(
        IReadOnlyList<OmnipotenceTargetOptions> targetOptions,
        long attackerHitpoints = 100_000,
        bool useRangeStatusDamage = false)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 832,
            boundY: 584,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var sobNpc = CreateMoveLineSobNpc(id: 65_860, x: 836, y: 584);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [sobNpc],
            [
                CreateOmnipotenceRangeRow(),
                CreateOmnipotenceRangeRow(level: 1, experience: 100),
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
                    damage2: 7),
            ],
            items: useRangeStatusDamage ? CreateRangeStatusPhysicalDamageItems() : []);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        const byte useSpellSoul = 3;
        var attackerSession = CreateSession(
            x: 830,
            y: 584,
            minHitPoints: attackerHitpoints,
            minMana: 50,
            strength: 1_000,
            stamina: 100,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    TestOmnipotenceSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: useSpellSoul),
            ],
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalIncreaseItemId, position: 10)
                : PlayerItemSnapshot.Empty);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        sessions.Set(casterConnection, attackerSession);

        var targetEntries = new Dictionary<uint, OmnipotenceTargetRuntime>();
        foreach (var options in targetOptions)
        {
            var entry = CreateOmnipotenceTargetRuntime(options, useRangeStatusDamage);
            sessions.Set(entry.Connection, entry.InitialSession);
            targetEntries.Add(options.Uid, entry);
        }

        var boundaryEntry = CreateOmnipotenceTargetRuntime(
            new OmnipotenceTargetOptions(
                OmnipotenceBoundaryTargetUid,
                X: 837,
                Y: 584),
            useRangeStatusDamage: false);
        sessions.Set(boundaryEntry.Connection, boundaryEntry.InitialSession);

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            attackerSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attackerSession.PlayerSpells,
            worldData,
            TestOmnipotenceSpellId,
            out _,
            out var spell));
        var formulaTarget = targetEntries[targetOptions[0].Uid].InitialSession;
        var expectedNormalRangeDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attackerSession,
            formulaTarget,
            spell with { Id = 0 },
            worldData,
            1U);
        var legacyFixedTenPercentDamage = InvokePrivateUInt32(
            "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
            attackerSession,
            formulaTarget,
            spell,
            worldData,
            1U);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: TestOmnipotenceSpellId,
                x: 840,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalAttacker));
        Assert.NotNull(finalAttacker);
        var finalTargets = new Dictionary<uint, OmnipotenceTargetRuntime>();
        foreach (var (uid, entry) in targetEntries)
        {
            Assert.True(sessions.TryGet(entry.Connection, out var finalTarget));
            Assert.NotNull(finalTarget);
            finalTarget.Character.Attributes.TryGetValue(
                RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute,
                out var remainingWildPhoenixCharges);
            finalTargets.Add(
                uid,
                entry with
                {
                    FinalSession = finalTarget,
                    RemainingWildPhoenixCharges = remainingWildPhoenixCharges,
                });
        }

        Assert.True(sessions.TryGet(boundaryEntry.Connection, out var finalBoundaryTarget));
        Assert.NotNull(finalBoundaryTarget);
        if (!ParseSpellAnimations(casterSent).Any(animation =>
                animation.SpellId == HeavensWonderRuntimeSpellId))
        {
            Assert.Equal(100_000, finalBoundaryTarget.Character.MinHitPoints);
        }

        return new OmnipotenceRangeAttackResult(
            monster,
            sobNpc,
            casterSent,
            finalAttacker,
            finalTargets,
            expectedNormalRangeDamage,
            legacyFixedTenPercentDamage,
            log.ToString());
    }

    private static OmnipotenceTargetRuntime CreateOmnipotenceTargetRuntime(
        OmnipotenceTargetOptions options,
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

        var session = CreateSession(
            uid: options.Uid,
            name: $"OmnipotenceTarget{options.Uid}",
            x: options.X,
            y: options.Y,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: spellIds,
            attributes: attributes,
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalDecreaseItemId, position: 11)
                : PlayerItemSnapshot.Empty);
        var sent = new List<byte[]>();
        return new OmnipotenceTargetRuntime(
            options,
            CreateSendingConnection(sent),
            session,
            session,
            sent,
            RemainingWildPhoenixCharges: null);
    }

    private static MagicType CreateOmnipotenceRangeRow(byte level = 0, uint experience = 0)
    {
        return CreateMagicRow(
            TestOmnipotenceSpellId,
            sort: 85,
            level: level,
            useMana: 7,
            damage: 30_100,
            experience: experience,
            range: 7,
            coldTime: 0);
    }

    private sealed record OmnipotenceTargetOptions(
        uint Uid,
        int X,
        int Y,
        int? StatusFlag = null,
        uint WildPhoenixCharges = 0,
        bool LearnsBackDamage = false);

    private sealed record OmnipotenceTargetRuntime(
        OmnipotenceTargetOptions Options,
        ConquerTcpConnection Connection,
        WorldSession InitialSession,
        WorldSession FinalSession,
        IReadOnlyList<byte[]> Sent,
        string? RemainingWildPhoenixCharges);

    private sealed record OmnipotenceRangeAttackResult(
        LegacyMonsterInstance Monster,
        SobNpc SobNpc,
        IReadOnlyList<byte[]> CasterSent,
        WorldSession FinalAttacker,
        IReadOnlyDictionary<uint, OmnipotenceTargetRuntime> Targets,
        uint ExpectedNormalRangeDamage,
        uint LegacyFixedTenPercentDamage,
        string Log);
}
