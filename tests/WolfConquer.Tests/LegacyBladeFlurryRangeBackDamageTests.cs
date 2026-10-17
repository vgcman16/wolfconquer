using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint BladeFlurryFarTargetUid = TargetPlayerUid + 20;
    private const uint BladeFlurrySecondCounterTargetUid = TargetPlayerUid + 21;
    private const uint BladeFlurryClosePlayerDamagePercent = 185;

    [Fact]
    public async Task HandlerBladeFlurryRangeUsesFixedClosePercentRowFarPercentAndReceiveProgressBeforeSharedAnimation()
    {
        var result = await RunBladeFlurryRangeAttackAsync(
        [
            new BladeFlurryTargetOptions(TargetPlayerUid, X: 832, Y: 584),
            new BladeFlurryTargetOptions(BladeFlurryFarTargetUid, X: 835, Y: 584),
        ]);

        var incoming = AssertBladeFlurryAnimation(result);
        Assert.Equal(PlayerUid, incoming.AttackerUid);
        Assert.Equal(0U, incoming.AttackedUid);
        Assert.Equal((840, 584), (incoming.X, incoming.Y));
        Assert.Equal(
            new[] { result.Monster.Uid, TargetPlayerUid, BladeFlurryFarTargetUid }.Order().ToArray(),
            incoming.Targets.Select(target => target.Uid).Order().ToArray());

        var closeTarget = AssertBladeFlurryPlayerTarget(result, TargetPlayerUid);
        var farTarget = AssertBladeFlurryPlayerTarget(result, BladeFlurryFarTargetUid);
        Assert.Equal(result.ExpectedRangeDamage[TargetPlayerUid], closeTarget.Damage);
        Assert.Equal(result.ExpectedRangeDamage[BladeFlurryFarTargetUid], farTarget.Damage);
        Assert.NotEqual(closeTarget.Damage, farTarget.Damage);
        Assert.Equal(MsgAttackEffect.None, closeTarget.Effect);
        Assert.Equal(MsgAttackEffect.None, farTarget.Effect);
        Assert.Equal(
            100_000L - result.ExpectedRangeDamage[TargetPlayerUid],
            result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
        Assert.Equal(
            100_000L - result.ExpectedRangeDamage[BladeFlurryFarTargetUid],
            result.Targets[BladeFlurryFarTargetUid].FinalSession.Character.MinHitPoints);

        var casterAnimationIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == TestBladeFlurrySpellId);
        foreach (var uid in new[] { TargetPlayerUid, BladeFlurryFarTargetUid })
        {
            var targetFrames = result.Targets[uid].Sent;
            var receiveIndex = SingleFrameIndex(
                targetFrames,
                frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                    update.Uid == uid &&
                    update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
            var targetAnimationIndex = SingleFrameIndex(
                targetFrames,
                frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                    animation.SpellId == TestBladeFlurrySpellId);
            Assert.True(
                receiveIndex < targetAnimationIndex,
                "BladeFlurry.cs applies ReceiveAttack.Player.Execute before sending its shared animation.");
        }

        var progressIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == TestBladeFlurrySpellId);
        Assert.True(
            progressIndex < casterAnimationIndex,
            "BladeFlurry.cs calls UpdateSpell.CheckUpdate before sending its shared animation.");
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerBladeFlurryRangeStatusReturnUsesZeroBaseExtraAttackBeforeWildPhoenixAndBackDamage(
        int statusFlag)
    {
        var result = await RunBladeFlurryRangeAttackAsync(
        [
            new BladeFlurryTargetOptions(
                TargetPlayerUid,
                X: 835,
                Y: 584,
                StatusFlag: statusFlag,
                WildPhoenixCharges: 1,
                LearnsBackDamage: true),
        ],
        useRangeStatusDamage: true);

        AssertNoBladeFlurryBackDamageProc(result);
        var target = AssertBladeFlurryPlayerTarget(result, TargetPlayerUid);
        Assert.Equal(RangeStatusExpectedExtraAttack, target.Damage);
        Assert.True(target.Damage > 1);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(
            100_000L - RangeStatusExpectedExtraAttack,
            result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
        Assert.Equal("1", result.Targets[TargetPlayerUid].RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerBladeFlurryRangeWildPhoenixReturnPrecedesDefensiveStanceAndBackDamage()
    {
        var result = await RunBladeFlurryRangeAttackAsync(
        [
            new BladeFlurryTargetOptions(
                TargetPlayerUid,
                X: 835,
                Y: 584,
                StatusFlag: DefensiveStanceStatusFlag,
                WildPhoenixCharges: 1,
                LearnsBackDamage: true),
        ]);

        AssertNoBladeFlurryBackDamageProc(result);
        var target = AssertBladeFlurryPlayerTarget(result, TargetPlayerUid);
        Assert.Equal(1U, target.Damage);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(99_999, result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
        Assert.Equal("0", result.Targets[TargetPlayerUid].RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerBladeFlurryRangeDefensiveStanceReturnsBlockWithoutBackDamage()
    {
        var result = await RunBladeFlurryRangeAttackAsync(
        [
            new BladeFlurryTargetOptions(
                TargetPlayerUid,
                X: 835,
                Y: 584,
                StatusFlag: DefensiveStanceStatusFlag,
                LearnsBackDamage: true),
        ]);
        var rawDamage = result.ExpectedRangeDamage[TargetPlayerUid];
        var reduction = rawDamage * 40 / 100;
        var expectedDamage = rawDamage <= reduction ? 1U : rawDamage - reduction;

        AssertNoBladeFlurryBackDamageProc(result);
        var target = AssertBladeFlurryPlayerTarget(result, TargetPlayerUid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(
            100_000L - expectedDamage,
            result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerBladeFlurryRangeBackDamageCastsBeforeOneSharedAnimationAndCarriesStateAcrossTargets()
    {
        var result = await RunBladeFlurryRangeAttackAsync(
        [
            new BladeFlurryTargetOptions(
                TargetPlayerUid,
                // Keep the defenders outside each other's Heaven's Wonder radius so this
                // fixture isolates BladeFlurry caller state carry from recursive BackDmg.
                X: 824,
                Y: 584,
                LearnsBackDamage: true),
            new BladeFlurryTargetOptions(
                BladeFlurrySecondCounterTargetUid,
                X: 836,
                Y: 584,
                LearnsBackDamage: true),
        ],
        attackerHitpoints: 1);

        var proc = Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Contains(PlayerUid, proc.Targets.Select(target => target.Uid));
        Assert.Contains(proc.AttackerUid, result.Targets.Keys);

        var incoming = AssertBladeFlurryAnimation(result);
        Assert.Equal(
            new[] { TargetPlayerUid, BladeFlurrySecondCounterTargetUid }.Order().ToArray(),
            incoming.Targets
                .Where(target => result.Targets.ContainsKey(target.Uid))
                .Select(target => target.Uid)
                .Order()
                .ToArray());
        Assert.All(
            incoming.Targets.Where(target => result.Targets.ContainsKey(target.Uid)),
            target => Assert.Equal(result.ExpectedRangeDamage[target.Uid], target.Damage));

        var procIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId);
        var incomingIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == TestBladeFlurrySpellId);
        Assert.True(
            procIndex < incomingIndex,
            "Matching Range.OnPlayer invokes BackDmg before BladeFlurry sends its shared animation.");

        Assert.Equal(0, result.FinalAttacker.Character.MinHitPoints);
        Assert.Equal(
            1,
            result.Targets.Values.Count(target =>
                target.FinalSession.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true));
        Assert.All(
            result.Targets.Values,
            target => Assert.Equal(
                100_000L - result.ExpectedRangeDamage[target.Options.Uid],
                target.FinalSession.Character.MinHitPoints));
    }

    private static LegacySpellAnimationPacket AssertBladeFlurryAnimation(
        BladeFlurryRangeAttackResult result)
    {
        return Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == TestBladeFlurrySpellId);
    }

    private static LegacySpellAnimationTarget AssertBladeFlurryPlayerTarget(
        BladeFlurryRangeAttackResult result,
        uint uid)
    {
        return Assert.Single(
            AssertBladeFlurryAnimation(result).Targets,
            target => target.Uid == uid);
    }

    private static void AssertNoBladeFlurryBackDamageProc(BladeFlurryRangeAttackResult result)
    {
        var frames = result.CasterSent.Concat(
            result.Targets.Values.SelectMany(target => target.Sent));
        Assert.DoesNotContain(
            ParseSpellAnimations(frames),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.All(
            result.Targets.Values,
            target => Assert.False(
                target.FinalSession.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true));
    }

    private static async Task<BladeFlurryRangeAttackResult> RunBladeFlurryRangeAttackAsync(
        IReadOnlyList<BladeFlurryTargetOptions> targetOptions,
        long attackerHitpoints = 100_000,
        bool useRangeStatusDamage = false)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 836,
            boundY: 583,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [
                CreateBladeFlurryRangeRow(),
                CreateBladeFlurryRangeRow(level: 1, experience: 100),
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
        var attackerSession = CreateSession(
            x: 830,
            y: 584,
            minHitPoints: attackerHitpoints,
            minMana: 50,
            strength: 1_000,
            stamina: 100,
            spellIds: [TestBladeFlurrySpellId],
            playerItems: useRangeStatusDamage
                ? CreateRangeStatusEquipment(RangeStatusPhysicalIncreaseItemId, position: 10)
                : PlayerItemSnapshot.Empty);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var casterSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        sessions.Set(casterConnection, attackerSession);

        var targetEntries = new Dictionary<uint, BladeFlurryTargetRuntime>();
        foreach (var options in targetOptions)
        {
            var entry = CreateBladeFlurryTargetRuntime(options, useRangeStatusDamage);
            sessions.Set(entry.Connection, entry.InitialSession);
            targetEntries.Add(options.Uid, entry);
        }

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            attackerSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attackerSession.PlayerSpells,
            worldData,
            TestBladeFlurrySpellId,
            out _,
            out var spell));
        var expectedRangeDamage = targetEntries.ToDictionary(
            entry => entry.Key,
            entry =>
            {
                var options = entry.Value.Options;
                var distance = Math.Max(
                    Math.Abs(attackerSession.Character.X - options.X),
                    Math.Abs(attackerSession.Character.Y - options.Y));
                var rangeSpell = distance < 3
                    ? spell with
                    {
                        Id = 0,
                        Damage = BladeFlurryClosePlayerDamagePercent,
                    }
                    : spell with { Id = 0 };
                return InvokePrivateUInt32(
                    "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
                    attackerSession,
                    entry.Value.InitialSession,
                    rangeSpell,
                    worldData,
                    1U);
            });

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: TestBladeFlurrySpellId,
                x: 840,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalAttacker));
        Assert.NotNull(finalAttacker);
        var finalTargets = new Dictionary<uint, BladeFlurryTargetRuntime>();
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

        return new BladeFlurryRangeAttackResult(
            monster,
            casterSent,
            finalAttacker,
            finalTargets,
            expectedRangeDamage,
            log.ToString());
    }

    private static BladeFlurryTargetRuntime CreateBladeFlurryTargetRuntime(
        BladeFlurryTargetOptions options,
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
            name: $"BladeFlurryTarget{options.Uid}",
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
        return new BladeFlurryTargetRuntime(
            options,
            CreateSendingConnection(sent),
            session,
            session,
            sent,
            RemainingWildPhoenixCharges: null);
    }

    private static MagicType CreateBladeFlurryRangeRow(byte level = 0, uint experience = 0)
    {
        return CreateMagicRow(
            TestBladeFlurrySpellId,
            sort: 68,
            level: level,
            useMana: 7,
            damage: 120,
            experience: experience,
            range: 7,
            coldTime: 0);
    }

    private sealed record BladeFlurryTargetOptions(
        uint Uid,
        int X,
        int Y,
        int? StatusFlag = null,
        uint WildPhoenixCharges = 0,
        bool LearnsBackDamage = false);

    private sealed record BladeFlurryTargetRuntime(
        BladeFlurryTargetOptions Options,
        ConquerTcpConnection Connection,
        WorldSession InitialSession,
        WorldSession FinalSession,
        IReadOnlyList<byte[]> Sent,
        string? RemainingWildPhoenixCharges);

    private sealed record BladeFlurryRangeAttackResult(
        LegacyMonsterInstance Monster,
        IReadOnlyList<byte[]> CasterSent,
        WorldSession FinalAttacker,
        IReadOnlyDictionary<uint, BladeFlurryTargetRuntime> Targets,
        IReadOnlyDictionary<uint, uint> ExpectedRangeDamage,
        string Log);
}
