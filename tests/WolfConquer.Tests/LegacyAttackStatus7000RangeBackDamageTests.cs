using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint AttackStatus7000BoundaryTargetUid = TargetPlayerUid + 30;
    private const uint AttackStatus7000BlockedTargetUid = TargetPlayerUid + 31;
    private const uint AttackStatus7000SecondCounterTargetUid = TargetPlayerUid + 32;
    private const uint AttackStatus7000PhysicalIncreaseItemId = 201_004;
    private const uint AttackStatus7000RevengeTailDamage = 5_000;
    private const string AttackStatus7000RevengeTailChangeAttribute = "LegacyRevengeTailChange";

    [Fact]
    public async Task HandlerAttackStatus7000RangeUsesStrictPlayerGeometryGateScalingAndReceiveProgressBeforeSharedAnimation()
    {
        var result = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(TargetPlayerUid, X: 835, Y: 584),
            new AttackStatus7000TargetOptions(
                AttackStatus7000BoundaryTargetUid,
                X: 837,
                Y: 584),
            new AttackStatus7000TargetOptions(
                AttackStatus7000BlockedTargetUid,
                X: 834,
                Y: 584,
                Invisible: true),
        ]);

        var incoming = AssertAttackStatus7000RangeAnimation(result);
        Assert.Equal(PlayerUid, incoming.AttackerUid);
        Assert.Equal(0U, incoming.AttackedUid);
        Assert.Equal((840, 584), (incoming.X, incoming.Y));
        Assert.Equal(
            [result.Monster.Uid, TargetPlayerUid],
            incoming.Targets.Select(target => target.Uid).ToArray());
        Assert.DoesNotContain(
            incoming.Targets,
            target => target.Uid is AttackStatus7000BoundaryTargetUid or AttackStatus7000BlockedTargetUid);

        var playerTarget = AssertAttackStatus7000RangePlayerTarget(result, TargetPlayerUid);
        Assert.Equal(result.ExpectedRangeDamage[TargetPlayerUid], playerTarget.Damage);
        Assert.NotEqual(result.HundredPercentRangeDamage[TargetPlayerUid], playerTarget.Damage);
        Assert.Equal(MsgAttackEffect.None, playerTarget.Effect);
        Assert.Equal(
            100_000L - result.ExpectedRangeDamage[TargetPlayerUid],
            result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
        Assert.Equal(
            100_000,
            result.Targets[AttackStatus7000BoundaryTargetUid].FinalSession.Character.MinHitPoints);
        Assert.Equal(
            100_000,
            result.Targets[AttackStatus7000BlockedTargetUid].FinalSession.Character.MinHitPoints);

        var targetFrames = result.Targets[TargetPlayerUid].Sent;
        var receiveIndex = SingleFrameIndex(
            targetFrames,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var targetAnimationIndex = SingleFrameIndex(
            targetFrames,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == AttackStatus7000SpellId);
        var progressIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellPacket.TryParse(frame, out var packet) &&
                packet.Id == AttackStatus7000SpellId);
        var casterAnimationIndex = SingleFrameIndex(
            result.CasterSent,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                animation.SpellId == AttackStatus7000SpellId);

        Assert.True(
            receiveIndex < targetAnimationIndex,
            "AttackStatus.cs case 7000 applies ReceiveAttack.Player.Execute before its shared animation.");
        Assert.True(
            progressIndex < casterAnimationIndex,
            "AttackStatus.cs case 7000 calls UpdateSpell.CheckUpdate before its shared animation.");
    }

    [Theory]
    [InlineData(LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData(LegacyPhysicalSpellsManiacDanceFlag)]
    public async Task HandlerAttackStatus7000RangeStatusReturnPrecedesWildPhoenixAndBackDamage(
        int statusFlag)
    {
        var result = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(
                TargetPlayerUid,
                X: 835,
                Y: 584,
                StatusFlag: statusFlag,
                WildPhoenixCharges: 1,
                LearnsBackDamage: true),
        ],
        attackerPhysicalDamageIncrease: 500);

        AssertNoAttackStatus7000RangeBackDamageProc(result);
        var target = AssertAttackStatus7000RangePlayerTarget(result, TargetPlayerUid);
        Assert.Equal(500U, target.Damage);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(99_500, result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
        Assert.Equal("1", result.Targets[TargetPlayerUid].RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerAttackStatus7000RangeWildPhoenixReturnPrecedesDefensiveStanceAndBackDamage()
    {
        var result = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(
                TargetPlayerUid,
                X: 835,
                Y: 584,
                StatusFlag: DefensiveStanceStatusFlag,
                WildPhoenixCharges: 1,
                LearnsBackDamage: true),
        ]);

        AssertNoAttackStatus7000RangeBackDamageProc(result);
        var target = AssertAttackStatus7000RangePlayerTarget(result, TargetPlayerUid);
        Assert.Equal(1U, target.Damage);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(99_999, result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
        Assert.Equal("0", result.Targets[TargetPlayerUid].RemainingWildPhoenixCharges);
    }

    [Fact]
    public async Task HandlerAttackStatus7000RangeDefensiveStanceReturnsBlockWithoutBackDamage()
    {
        var result = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(
                TargetPlayerUid,
                X: 835,
                Y: 584,
                StatusFlag: DefensiveStanceStatusFlag,
                LearnsBackDamage: true),
        ]);
        var rawDamage = result.ExpectedRangeDamage[TargetPlayerUid];
        var reduction = rawDamage * 40 / 100;
        var expectedDamage = rawDamage <= reduction ? 1U : rawDamage - reduction;

        AssertNoAttackStatus7000RangeBackDamageProc(result);
        var target = AssertAttackStatus7000RangePlayerTarget(result, TargetPlayerUid);
        Assert.Equal(expectedDamage, target.Damage);
        Assert.Equal(MsgAttackEffect.Block, target.Effect);
        Assert.Equal(
            100_000L - expectedDamage,
            result.Targets[TargetPlayerUid].FinalSession.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerAttackStatus7000RangeBackDamageCastsBeforeSharedAnimationAndCarriesStateAcrossTargets()
    {
        var result = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(
                TargetPlayerUid,
                // Keep both defenders within Seizer's strict range but outside each
                // other's Heaven's Wonder radius to isolate caller state carry.
                X: 824,
                Y: 584,
                LearnsBackDamage: true),
            new AttackStatus7000TargetOptions(
                AttackStatus7000SecondCounterTargetUid,
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

        var incoming = AssertAttackStatus7000RangeAnimation(result);
        Assert.Equal(
            new[] { TargetPlayerUid, AttackStatus7000SecondCounterTargetUid }.Order().ToArray(),
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
                animation.SpellId == AttackStatus7000SpellId);
        Assert.True(
            procIndex < incomingIndex,
            "Matching Range.OnPlayer invokes BackDmg before AttackStatus.cs sends spell 7000's shared animation.");

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

    [Fact]
    public async Task HandlerAttackStatus7000RangeRevengeTailCountersThenPreservesIncomingHit()
    {
        var result = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(
                TargetPlayerUid,
                X: 835,
                Y: 584,
                RevengeTailCharges: 2),
        ]);
        var incomingDamage = result.ExpectedRangeDamage[TargetPlayerUid];
        Assert.True(incomingDamage < AttackStatus7000RevengeTailDamage);

        var revengeTail = Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == RevengeTailAttachStatusSpellId);
        Assert.Equal(TargetPlayerUid, revengeTail.AttackerUid);
        Assert.Equal(0U, revengeTail.AttackedUid);
        Assert.Equal((835, 584), (revengeTail.X, revengeTail.Y));
        Assert.Equal(1, revengeTail.BombFlag);
        var counterTarget = Assert.Single(revengeTail.Targets);
        Assert.Equal(PlayerUid, counterTarget.Uid);
        Assert.Equal(AttackStatus7000RevengeTailDamage, counterTarget.Damage);
        Assert.True(counterTarget.Hit);

        var finalTarget = result.Targets[TargetPlayerUid].FinalSession;
        Assert.Equal(
            100_000L - AttackStatus7000RevengeTailDamage,
            result.FinalAttacker.Character.MinHitPoints);
        Assert.Equal(100_000L - incomingDamage, finalTarget.Character.MinHitPoints);
        Assert.Equal(
            "1",
            finalTarget.Character.Attributes[AttackStatus7000RevengeTailChangeAttribute]);
        Assert.True(HasLegacyStatusFlag(finalTarget.Character.Attributes, RevengeTailStatusFlag));

        var counterReceiveIndex = SingleAttackStatus7000TimelineIndex(
            result.SendTimeline,
            sent => sent.RecipientUid == PlayerUid &&
                LegacyMsgUpdatePacket.TryParse(ReadFrame(sent.Bytes), out var update) &&
                update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var revengeTailIndex = SingleAttackStatus7000TimelineIndex(
            result.SendTimeline,
            sent => sent.RecipientUid == PlayerUid &&
                LegacySpellAnimationPacket.TryParse(ReadFrame(sent.Bytes), out var animation) &&
                animation.SpellId == RevengeTailAttachStatusSpellId);
        var incomingReceiveIndex = SingleAttackStatus7000TimelineIndex(
            result.SendTimeline,
            sent => sent.RecipientUid == TargetPlayerUid &&
                LegacyMsgUpdatePacket.TryParse(ReadFrame(sent.Bytes), out var update) &&
                update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var sharedAnimationIndex = SingleAttackStatus7000TimelineIndex(
            result.SendTimeline,
            sent => sent.RecipientUid == PlayerUid &&
                LegacySpellAnimationPacket.TryParse(ReadFrame(sent.Bytes), out var animation) &&
                animation.SpellId == AttackStatus7000SpellId);
        Assert.True(
            counterReceiveIndex < revengeTailIndex,
            "BackDmg.cs applies RevengeTail's ReceiveAttack.Player.Execute before sending its bomb animation.");
        Assert.True(
            revengeTailIndex < incomingReceiveIndex,
            "BackDmg.cs sends the defender's RevengeTail bomb before AttackStatus.cs applies the incoming hit.");
        Assert.True(
            incomingReceiveIndex < sharedAnimationIndex,
            "AttackStatus.cs applies the incoming hit before sending spell 7000's shared animation.");
    }

    [Fact]
    public async Task HandlerAttackStatus7000RangeRevengeTailUsesStrictDamageThreshold()
    {
        var baseline = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(TargetPlayerUid, X: 835, Y: 584),
        ]);
        var incomingDamage = baseline.ExpectedRangeDamage[TargetPlayerUid];

        var result = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(
                TargetPlayerUid,
                X: 835,
                Y: 584,
                RevengeTailCharges: 1),
        ],
        revengeTailDamage: incomingDamage);

        Assert.DoesNotContain(
            result.SendTimeline,
            sent => LegacySpellAnimationPacket.TryParse(ReadFrame(sent.Bytes), out var animation) &&
                animation.SpellId == RevengeTailAttachStatusSpellId);
        Assert.Equal(100_000, result.FinalAttacker.Character.MinHitPoints);
        var finalTarget = result.Targets[TargetPlayerUid].FinalSession;
        Assert.Equal(100_000L - incomingDamage, finalTarget.Character.MinHitPoints);
        Assert.Equal(
            "1",
            finalTarget.Character.Attributes[AttackStatus7000RevengeTailChangeAttribute]);
        Assert.True(HasLegacyStatusFlag(finalTarget.Character.Attributes, RevengeTailStatusFlag));
    }

    [Fact]
    public async Task HandlerAttackStatus7000RangeRevengeTailCarriesDeadAttackerAcrossTargets()
    {
        var result = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(
                TargetPlayerUid,
                X: 824,
                Y: 584,
                RevengeTailCharges: 1),
            new AttackStatus7000TargetOptions(
                AttackStatus7000SecondCounterTargetUid,
                X: 836,
                Y: 584,
                RevengeTailCharges: 1),
        ],
        attackerHitpoints: AttackStatus7000RevengeTailDamage);
        Assert.All(
            result.ExpectedRangeDamage.Values,
            incomingDamage => Assert.True(incomingDamage < AttackStatus7000RevengeTailDamage));

        var revengeTail = Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == RevengeTailAttachStatusSpellId);
        var counterTarget = Assert.Single(revengeTail.Targets);
        Assert.Equal(PlayerUid, counterTarget.Uid);
        Assert.Equal(AttackStatus7000RevengeTailDamage, counterTarget.Damage);
        Assert.Equal(0, result.FinalAttacker.Character.MinHitPoints);

        var consumedTarget = result.Targets[revengeTail.AttackerUid].FinalSession;
        Assert.Equal(
            "0",
            consumedTarget.Character.Attributes[AttackStatus7000RevengeTailChangeAttribute]);
        Assert.False(HasLegacyStatusFlag(consumedTarget.Character.Attributes, RevengeTailStatusFlag));
        var untouchedTarget = Assert.Single(
            result.Targets.Values,
            target => target.Options.Uid != revengeTail.AttackerUid).FinalSession;
        Assert.Equal(
            "1",
            untouchedTarget.Character.Attributes[AttackStatus7000RevengeTailChangeAttribute]);
        Assert.True(HasLegacyStatusFlag(untouchedTarget.Character.Attributes, RevengeTailStatusFlag));

        var incoming = AssertAttackStatus7000RangeAnimation(result);
        Assert.Equal(
            new[] { TargetPlayerUid, AttackStatus7000SecondCounterTargetUid }.Order().ToArray(),
            incoming.Targets
                .Where(target => result.Targets.ContainsKey(target.Uid))
                .Select(target => target.Uid)
                .Order()
                .ToArray());
        Assert.All(
            result.Targets.Values,
            target => Assert.Equal(
                100_000L - result.ExpectedRangeDamage[target.Options.Uid],
                target.FinalSession.Character.MinHitPoints));
    }

    [Fact]
    public async Task HandlerAttackStatus7000RangeReceivesFirstPlayerBeforeNextPlayersBackDamageAnimation()
    {
        var result = await RunAttackStatus7000RangeAttackAsync(
        [
            new AttackStatus7000TargetOptions(
                TargetPlayerUid,
                X: 824,
                Y: 584,
                LearnsBackDamage: true),
            new AttackStatus7000TargetOptions(
                AttackStatus7000SecondCounterTargetUid,
                X: 836,
                Y: 584,
                LearnsBackDamage: true),
        ]);

        var playerOrder = AssertAttackStatus7000RangeAnimation(result).Targets
            .Where(target => result.Targets.ContainsKey(target.Uid))
            .Select(target => target.Uid)
            .ToArray();
        Assert.Equal(2, playerOrder.Length);
        var firstPlayerUid = playerOrder[0];
        var secondPlayerUid = playerOrder[1];

        var firstReceiveIndex = SingleAttackStatus7000TimelineIndex(
            result.SendTimeline,
            sent => sent.RecipientUid == firstPlayerUid &&
                LegacyMsgUpdatePacket.TryParse(ReadFrame(sent.Bytes), out var update) &&
                update.Uid == firstPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var secondReceiveIndex = SingleAttackStatus7000TimelineIndex(
            result.SendTimeline,
            sent => sent.RecipientUid == secondPlayerUid &&
                LegacyMsgUpdatePacket.TryParse(ReadFrame(sent.Bytes), out var update) &&
                update.Uid == secondPlayerUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints));
        var secondBackDamageIndex = SingleAttackStatus7000TimelineIndex(
            result.SendTimeline,
            sent => sent.RecipientUid == PlayerUid &&
                LegacySpellAnimationPacket.TryParse(ReadFrame(sent.Bytes), out var animation) &&
                animation.SpellId == HeavensWonderRuntimeSpellId &&
                animation.AttackerUid == secondPlayerUid);
        var incomingAnimationIndex = SingleAttackStatus7000TimelineIndex(
            result.SendTimeline,
            sent => sent.RecipientUid == PlayerUid &&
                LegacySpellAnimationPacket.TryParse(ReadFrame(sent.Bytes), out var animation) &&
                animation.SpellId == AttackStatus7000SpellId);

        Assert.True(result.FinalAttacker.Character.MinHitPoints > 0);
        Assert.True(firstReceiveIndex < incomingAnimationIndex);
        Assert.True(secondReceiveIndex < incomingAnimationIndex);
        Assert.True(
            firstReceiveIndex < secondBackDamageIndex,
            "AttackStatus.cs case 7000 applies each player's ReceiveAttack.Player.Execute before Range.OnPlayer processes the next player.");
    }

    private static LegacySpellAnimationPacket AssertAttackStatus7000RangeAnimation(
        AttackStatus7000RangeAttackResult result)
    {
        return Assert.Single(
            ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == AttackStatus7000SpellId);
    }

    private static LegacySpellAnimationTarget AssertAttackStatus7000RangePlayerTarget(
        AttackStatus7000RangeAttackResult result,
        uint uid)
    {
        return Assert.Single(
            AssertAttackStatus7000RangeAnimation(result).Targets,
            target => target.Uid == uid);
    }

    private static void AssertNoAttackStatus7000RangeBackDamageProc(
        AttackStatus7000RangeAttackResult result)
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

    private static int SingleAttackStatus7000TimelineIndex(
        IReadOnlyList<AttackStatus7000SentFrame> timeline,
        Func<AttackStatus7000SentFrame, bool> predicate)
    {
        return Assert.Single(
            Enumerable.Range(0, timeline.Count),
            index => predicate(timeline[index]));
    }

    private static async Task<AttackStatus7000RangeAttackResult> RunAttackStatus7000RangeAttackAsync(
        IReadOnlyList<AttackStatus7000TargetOptions> targetOptions,
        long attackerHitpoints = 100_000,
        uint attackerPhysicalDamageIncrease = 0,
        uint revengeTailDamage = AttackStatus7000RevengeTailDamage)
    {
        var monsterFamily = CreateHighLifeMonsterFamily();
        var spawn = CreateSingleInstanceMonsterSpawn(
            generatorId: 1,
            boundX: 836,
            boundY: 583,
            monsterTypeId: monsterFamily.Id,
            sourceMonsterName: monsterFamily.Name);
        var physicalIncreaseItem = attackerPhysicalDamageIncrease == 0
            ? null
            : CreateAttackStatus7000PhysicalDamageIncreaseItem(attackerPhysicalDamageIncrease);
        var worldData = CreateMagicCatalogCore(
            monsterFamily,
            [spawn],
            [],
            [
                CreateAttackStatus7000Row(experience: 1),
                CreateAttackStatus7000Row(level: 1, experience: 100),
                CreateCounterKillRuntimeRow(
                    HeavensWonderRuntimeSpellId,
                    coolDown: 0,
                    coldTime: 1_000),
                CreateMagicRow(
                    RevengeTailAttachStatusSpellId,
                    sort: 6,
                    useMana: 0,
                    damage: revengeTailDamage,
                    coldTime: 0),
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
            items: physicalIncreaseItem is null ? [] : [physicalIncreaseItem]);
        var monster = CreateRuntimeMonsterInstance(worldData, spawn, monsterFamily);
        var attackerItems = physicalIncreaseItem is null
            ? PlayerItemSnapshot.Empty
            : new PlayerItemSnapshot(
                InventoryItems: [],
                RelicFuseItems: [],
                MythsoulBagItems: [],
                EquipmentItems:
                [
                    new PlayerItemSnapshotItem
                    {
                        ItemId = AttackStatus7000PhysicalIncreaseItemId,
                        Durability = 100,
                        MaximumDurability = 100,
                        Position = 10,
                    },
                ],
                RuneItems: []);
        var attackerSession = CreateSession(
            x: 830,
            y: 584,
            minHitPoints: attackerHitpoints,
            minMana: 50,
            strength: 1_000,
            stamina: 100,
            spellIds: [AttackStatus7000SpellId],
            playerItems: attackerItems);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var casterSent = new List<byte[]>();
        var sendTimeline = new AttackStatus7000SendTimeline();
        using var log = new StringWriter();
        var casterConnection = CreateAttackStatus7000SendingConnection(
            casterSent,
            sendTimeline,
            PlayerUid);
        sessions.Set(casterConnection, attackerSession);

        var targetEntries = new Dictionary<uint, AttackStatus7000TargetRuntime>();
        foreach (var options in targetOptions)
        {
            var entry = CreateAttackStatus7000TargetRuntime(options, sendTimeline);
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
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref attackerSession);
        context = combatSpellOwner.Context;
        Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(
            attackerSession.PlayerSpells,
            worldData,
            AttackStatus7000SpellId,
            out _,
            out var spell));
        var expectedRangeDamage = targetEntries.ToDictionary(
            entry => entry.Key,
            entry => InvokePrivateUInt32(
                "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
                attackerSession,
                entry.Value.InitialSession,
                spell,
                worldData,
                1U));
        var hundredPercentRangeDamage = targetEntries.ToDictionary(
            entry => entry.Key,
            entry => InvokePrivateUInt32(
                "CalculateLegacyPlayerPlayerPhysicalSpellDamage",
                attackerSession,
                entry.Value.InitialSession,
                spell with { Damage = 100 },
                worldData,
                1U));

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                monster,
                legacyEncodedQuery: false,
                spellId: AttackStatus7000SpellId,
                x: 840,
                y: 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalAttacker));
        Assert.NotNull(finalAttacker);
        var finalTargets = new Dictionary<uint, AttackStatus7000TargetRuntime>();
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

        return new AttackStatus7000RangeAttackResult(
            monster,
            casterSent,
            sendTimeline.Snapshot(),
            finalAttacker,
            finalTargets,
            expectedRangeDamage,
            hundredPercentRangeDamage,
            log.ToString());
    }

    private static ItemType CreateAttackStatus7000PhysicalDamageIncreaseItem(uint amount)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = AttackStatus7000PhysicalIncreaseItemId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "AttackStatus7000PhysicalIncrease";
        fields[13] = amount.ToString(CultureInfo.InvariantCulture);
        return new ItemType(
            checked((int)AttackStatus7000PhysicalIncreaseItemId),
            fields[1],
            fields);
    }

    private static AttackStatus7000TargetRuntime CreateAttackStatus7000TargetRuntime(
        AttackStatus7000TargetOptions options,
        AttackStatus7000SendTimeline sendTimeline)
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

        if (options.RevengeTailCharges > 0)
        {
            spellIds.Add(RevengeTailAttachStatusSpellId);
            statusFlags.Add(RevengeTailStatusFlag);
            attributes[AttackStatus7000RevengeTailChangeAttribute] =
                options.RevengeTailCharges.ToString(CultureInfo.InvariantCulture);
        }

        if (options.WildPhoenixCharges > 0)
        {
            spellIds.Add(WildPhoenixSpellId);
            statusFlags.Add(LegacyWildPhoenixFlag);
            attributes[RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007";
            attributes[RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] =
                options.WildPhoenixCharges.ToString(CultureInfo.InvariantCulture);
        }

        if (options.Invisible)
        {
            attributes["Invisible"] = "1";
        }

        if (statusFlags.Count > 0)
        {
            attributes["StatusFlags"] = BuildStatusFlags(statusFlags.ToArray());
        }

        var session = CreateSession(
            uid: options.Uid,
            name: $"AttackStatus7000Target{options.Uid}",
            x: options.X,
            y: options.Y,
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: spellIds,
            attributes: attributes);
        var sent = new List<byte[]>();
        return new AttackStatus7000TargetRuntime(
            options,
            CreateAttackStatus7000SendingConnection(sent, sendTimeline, options.Uid),
            session,
            session,
            sent,
            RemainingWildPhoenixCharges: null);
    }

    private static ConquerTcpConnection CreateAttackStatus7000SendingConnection(
        List<byte[]> sent,
        AttackStatus7000SendTimeline sendTimeline,
        uint recipientUid)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Add(bytes);
                sendTimeline.Add(recipientUid, bytes);
                return ValueTask.CompletedTask;
            });
    }

    private sealed record AttackStatus7000TargetOptions(
        uint Uid,
        int X,
        int Y,
        int? StatusFlag = null,
        uint WildPhoenixCharges = 0,
        bool LearnsBackDamage = false,
        uint RevengeTailCharges = 0,
        bool Invisible = false);

    private sealed record AttackStatus7000TargetRuntime(
        AttackStatus7000TargetOptions Options,
        ConquerTcpConnection Connection,
        WorldSession InitialSession,
        WorldSession FinalSession,
        IReadOnlyList<byte[]> Sent,
        string? RemainingWildPhoenixCharges);

    private sealed record AttackStatus7000RangeAttackResult(
        LegacyMonsterInstance Monster,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<AttackStatus7000SentFrame> SendTimeline,
        WorldSession FinalAttacker,
        IReadOnlyDictionary<uint, AttackStatus7000TargetRuntime> Targets,
        IReadOnlyDictionary<uint, uint> ExpectedRangeDamage,
        IReadOnlyDictionary<uint, uint> HundredPercentRangeDamage,
        string Log);

    private sealed record AttackStatus7000SentFrame(uint RecipientUid, byte[] Bytes);

    private sealed class AttackStatus7000SendTimeline
    {
        private readonly object _gate = new();
        private readonly List<AttackStatus7000SentFrame> _frames = [];

        public void Add(uint recipientUid, byte[] bytes)
        {
            lock (_gate)
            {
                _frames.Add(new AttackStatus7000SentFrame(recipientUid, bytes));
            }
        }

        public IReadOnlyList<AttackStatus7000SentFrame> Snapshot()
        {
            lock (_gate)
            {
                return _frames.ToArray();
            }
        }
    }
}
