using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort DirectArrowSpellId = 18_220;
    private const uint DirectArrowGearId = 401;
    private const int DirectArrowWaterComponentFlag = 269;
    private const int DirectArrowTop4ComponentFlag = 64;
    private const int DirectArrowWaterCompositeFlag = 333;
    private const int DirectArrowFireCompositeFlag = 332;
    private const int DirectArrowSobUid = 65_182;
    private const uint DirectArrowInitialHp = 100_000;

    [Theory]
    [InlineData("single-soul-zero")]
    [InlineData("single-soul-three")]
    [InlineData("counter-two")]
    [InlineData("counter-three")]
    [InlineData("components-without-composite")]
    [InlineData("fixed-line-boundaries")]
    [InlineData("outer-valid-empty-line")]
    [InlineData("mixed-player-anchor")]
    [InlineData("mixed-monster-anchor")]
    public async Task DirectArrowPhysicalReplacementUsesSource18220CompositeAndReceiveContract(string scenario)
    {
        var result = await DirectArrowRunAsync(scenario);
        var noComposite = scenario == "components-without-composite";
        var emptyLine = scenario == "outer-valid-empty-line";
        var mixed = scenario.StartsWith("mixed-", StringComparison.Ordinal);
        var geometry = scenario == "fixed-line-boundaries";
        var hasTargets = !noComposite && !emptyLine;
        var restored = hasTargets && (mixed || geometry || scenario is "counter-two" or "counter-three");
        var expectedArrow = !hasTargets || restored ? 0U : 1U;
        Assert.Equal(expectedArrow, DirectArrowCounter(result.Caster));
        // These are independent BitVector indexes. Water|Top4Weekly is333,
        // not the conjunction of standalone269 and64; removing333 leaves both.
        Assert.True(HasLegacyStatusFlag(result.Caster.Character.Attributes, DirectArrowWaterComponentFlag));
        Assert.True(HasLegacyStatusFlag(result.Caster.Character.Attributes, DirectArrowTop4ComponentFlag));
        Assert.Equal(!noComposite && (!hasTargets || restored),
            HasLegacyStatusFlag(result.Caster.Character.Attributes, DirectArrowWaterCompositeFlag));
        Assert.Equal(hasTargets, HasLegacyStatusFlag(result.Caster.Character.Attributes, DirectArrowFireCompositeFlag));
        Assert.False(HasLegacyStatusFlag(result.Caster.Character.Attributes, 268)); // standalone Fire was never added.

        // CheckCombatGear consumes even a selected but unavailable fixed17400;
        // it must not fall back to ordinary Physical2135 in the component control.
        Assert.DoesNotContain(result.DeliveryOrder, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical && attack.Query.Uid == PlayerUid);
        var casterAnimations = ParseSpellAnimations(result.CasterFrames)
            .Where(animation => animation.SpellId == DirectArrowSpellId).ToArray();
        var peerAnimations = ParseSpellAnimations(result.TargetFrames[TargetPlayerUid])
            .Where(animation => animation.SpellId == DirectArrowSpellId).ToArray();
        if (!hasTargets)
        {
            Assert.Empty(casterAnimations);
            Assert.Empty(peerAnimations);
            Assert.All(result.Targets.Values, target => Assert.Equal(100_000, target.Character.MinHitPoints));
            Assert.Empty(result.Observations);
        }
        else
        {
            uint[] expectedUids = mixed ? [result.MonsterUid, TargetPlayerUid, DirectArrowSobUid] :
                geometry ? [TargetPlayerUid, TargetPlayerUid + 1, TargetPlayerUid + 2] : [TargetPlayerUid];
            uint[] expectedDamage = mixed ? [1_200, 450, 1_200] :
                geometry ? [450, 450, 450] : [450];
            foreach (var animation in new[] { Assert.Single(casterAnimations), Assert.Single(peerAnimations) })
            {
                Assert.Equal(PlayerUid, animation.AttackerUid);
                Assert.Equal(0U, animation.AttackedUid);
                Assert.Equal(DirectArrowSpellId, animation.SpellId);
                Assert.Equal(scenario == "single-soul-zero" ? (byte)0 : (byte)3, animation.LevelHu);
                Assert.Equal((scenario == "mixed-monster-anchor" ? 832 : 839, 584),
                    ((int)animation.X, (int)animation.Y));
                if (geometry)
                {
                    // Source View iteration and local ConditionalWeakTable
                    // snapshots do not promise order among peer Players.
                    Assert.Equal(expectedUids.OrderBy(uid => uid),
                        animation.Targets.Select(target => target.Uid).OrderBy(uid => uid));
                }
                else
                {
                    Assert.Equal(expectedUids, animation.Targets.Select(target => target.Uid));
                }
                Assert.Equal(expectedDamage, animation.Targets.Select(target => target.Damage));
                Assert.All(animation.Targets, target =>
                {
                    Assert.True(target.Hit);
                    Assert.Equal(MsgAttackEffect.None, target.Effect);
                });
            }
            Assert.All(result.Targets, pair => Assert.Equal(
                pair.Key == TargetPlayerUid || geometry && pair.Key <= TargetPlayerUid + 2 ? 99_550 : 100_000,
                pair.Value.Character.MinHitPoints));
            var animationObservations = result.Observations.Where(item => item.Phase == "animation").ToArray();
            Assert.NotEmpty(animationObservations);
            Assert.All(animationObservations, item =>
            {
                Assert.True(item.Water);
                Assert.False(item.Fire);
                Assert.Equal(restored ? 3U : 1U, item.Arrow);
                Assert.Equal(99_550, item.PlayerHitpoints);
                Assert.Equal(mixed ? 98_800U : DirectArrowInitialHp, item.MonsterHitpoints);
                Assert.Equal(mixed ? 98_800 : 100_000, item.SobHitpoints);
            });
        }

        // Player.SendUpdate(flags,...,scren:false):4713-4723 is OWNER ONLY.
        // Do not reuse a helper that broadcasts these two typed icon updates.
        var fireUpdates = DirectArrowTypedUpdates(result.CasterFrames, 137, DirectArrowFireCompositeFlag);
        var waterUpdates = DirectArrowTypedUpdates(result.CasterFrames,
            (uint)LegacyMsgUpdateDataType.AppendIcon, DirectArrowWaterCompositeFlag);
        Assert.Equal(hasTargets ? 1 : 0, fireUpdates.Length);
        Assert.Equal(restored ? 1 : 0, waterUpdates.Length);
        Assert.All(fireUpdates, entry => DirectArrowAssertTypedUpdate(entry, 5));
        Assert.All(waterUpdates, entry => DirectArrowAssertTypedUpdate(entry, 2));
        foreach (var frames in result.TargetFrames.Values)
        {
            Assert.Empty(DirectArrowTypedUpdates(frames, 137, DirectArrowFireCompositeFlag));
            Assert.Empty(DirectArrowTypedUpdates(frames,
                (uint)LegacyMsgUpdateDataType.AppendIcon, DirectArrowWaterCompositeFlag));
        }
        if (hasTargets)
        {
            var lastAnimation = Array.FindLastIndex(result.DeliveryOrder, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                animation.SpellId == DirectArrowSpellId);
            var fireIndex = DirectArrowTypedUpdateIndex(result.DeliveryOrder, 137, DirectArrowFireCompositeFlag);
            Assert.True(lastAnimation >= 0 && fireIndex > lastAnimation);
            var fireObservation = Assert.Single(result.Observations, item => item.Phase == "fire");
            Assert.False(fireObservation.Water);
            Assert.True(fireObservation.Fire);
            if (restored)
            {
                var waterIndex = DirectArrowTypedUpdateIndex(result.DeliveryOrder,
                    (uint)LegacyMsgUpdateDataType.AppendIcon, DirectArrowWaterCompositeFlag);
                Assert.True(waterIndex > fireIndex);
                var waterObservation = Assert.Single(result.Observations, item => item.Phase == "water");
                Assert.True(waterObservation.Water);
                Assert.True(waterObservation.Fire);
                Assert.Equal(3U, waterObservation.Arrow); // reset occurs AFTER this SendUpdate.
            }
        }
        if (mixed)
        {
            Assert.Equal(98_800U, result.MonsterHitpoints);
            Assert.Equal(98_800, result.SobHitpoints);
            var monsterObservations = result.Observations.Where(item => item.Phase == "monster").ToArray();
            Assert.NotEmpty(monsterObservations);
            Assert.All(monsterObservations, item =>
            {
                Assert.Equal(98_800U, item.MonsterHitpoints);
                Assert.Equal(100_000, item.PlayerHitpoints);
                Assert.Equal(100_000, item.SobHitpoints);
            });
            var playerObservations = result.Observations.Where(item => item.Phase == "player").ToArray();
            Assert.NotEmpty(playerObservations);
            Assert.All(playerObservations, item =>
            {
                Assert.Equal(98_800U, item.MonsterHitpoints);
                Assert.Equal(99_550, item.PlayerHitpoints);
                Assert.Equal(100_000, item.SobHitpoints);
            });
            var lastMonsterHp = Array.FindLastIndex(result.DeliveryOrder,
                bytes => DirectArrowHasHitpoints(bytes, result.MonsterUid));
            var firstPlayerHp = Array.FindIndex(result.DeliveryOrder,
                bytes => DirectArrowHasHitpoints(bytes, TargetPlayerUid));
            var firstAnimation = Array.FindIndex(result.DeliveryOrder, bytes =>
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                animation.SpellId == DirectArrowSpellId);
            Assert.True(lastMonsterHp >= 0 && firstPlayerHp > lastMonsterHp && firstAnimation > firstPlayerHp);
        }
        Assert.Equal(0, result.Caster.Character.Experience);
        Assert.Equal(0, Assert.Single(result.Caster.PlayerSpells.Spells).Experience);
        Assert.DoesNotContain(result.DeliveryOrder, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder), packet =>
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Experience));
        Assert.Equal(500, result.Caster.Character.MinMana);
        Assert.Equal("100", result.Caster.Character.Attributes["Stamina"]);
    }

    private static uint DirectArrowCounter(WorldSession session) => uint.Parse(
        session.Character.Attributes["ArrowBlades"], CultureInfo.InvariantCulture);

    private static LegacyMsgUpdateEntry[] DirectArrowTypedUpdates(byte[][] frames, uint type, int flag) =>
        ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid).SelectMany(packet => packet.Updates)
            .Where(entry => (uint)entry.Type == type && entry.Flag == checked((uint)flag)).ToArray();

    private static int DirectArrowTypedUpdateIndex(byte[][] frames, uint type, int flag) =>
        Array.FindIndex(frames, bytes => LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == PlayerUid && packet.Updates.Any(entry =>
                (uint)entry.Type == type && entry.Flag == checked((uint)flag)));

    private static void DirectArrowAssertTypedUpdate(LegacyMsgUpdateEntry entry, uint damage)
    {
        Assert.Equal(0UL, entry.Value);
        Assert.Equal(0U, entry.Time);
        Assert.Equal(damage, entry.Damage);
        Assert.Equal(0U, entry.SpellLevel);
    }

    private static bool DirectArrowHasHitpoints(byte[] bytes, uint uid) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == uid &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints);

    private static async Task<DirectArrowResult> DirectArrowRunAsync(string scenario)
    {
        var mixed = scenario.StartsWith("mixed-", StringComparison.Ordinal);
        var geometry = scenario == "fixed-line-boundaries";
        var emptyLine = scenario == "outer-valid-empty-line";
        var soul = scenario == "single-soul-zero" ? (byte)0 : (byte)3;
        var arrow = scenario == "counter-two" ? 2U : scenario == "counter-three" ? 3U : 0U;
        var family = CreateMonsterFamily() with
        {
            Attributes = new Dictionary<string, string>(CreateMonsterFamily().Attributes)
            {
                ["life"] = "100000", ["level"] = "121", ["defense"] = "0", ["defense2"] = "10000",
                ["Boss"] = "1", ["attack_range"] = "0",
            },
        };
        var spawn = CreateSingleInstanceMonsterSpawn(generatorId: 31_822, boundX: 832, boundY: 584)
            with { MapId = 1_000 };
        var sob = CreateMoveLineSobNpc(DirectArrowSobUid, x: 844, y: 584) with
        {
            MapId = 1_000, Life = 100_000, MaxLife = 100_000,
        };
        // Exact supplied magictype.txt row3019 damage/rate/range shape. Range20
        // does not replace the branch's fixed line15 and width4. Actual raw
        // damage30300, Human30080, D2=5, D3=8000, Monster100 include zero in
        // player MIN =>base300; MAX300 adds300% =>1200 for monster/SOB.
        var row = CreateMagicRow(DirectArrowSpellId, sort: 103, useMana: 0, damage: 30_300,
            range: 20, maxTargets: 4, needLevel: 40, useStamina: 0, coolDown: 0, coldTime: 0,
            duration: 10, damageOnHuman: 30_080, damage2: 5, damage3: 8_000,
            damageOnMonster: 100, gDamage: 100);
        Assert.Equal(new[] { "30300", "100", "10", "4", "20", "30080", "5", "8000", "100" },
            new[] { 10, 12, 13, 14, 15, 35, 36, 37, 44 }.Select(index => row.Fields[index]));
        var catalog = CreateMagicCatalogCore(family, mixed ? [spawn] : [], mixed ? [sob] : [], [row]);
        var monster = CreateRuntimeMonsterInstance(catalog, spawn, family);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobStates = new SobNpcRuntimeStateStore();
        var casterFlags = new List<int> { DirectArrowWaterComponentFlag, DirectArrowTop4ComponentFlag };
        if (scenario != "components-without-composite")
        {
            casterFlags.Add(DirectArrowWaterCompositeFlag);
        }
        var caster = CreateSession(mapId: 1_000, classCode: 4_005, strength: 300, spirit: 0,
            minHitPoints: DirectArrowInitialHp, stamina: 100,
            spellEntries: [CreatePlayerSpell(DirectArrowSpellId) with { SoulLevel = soul, UseSpellSoul = soul }],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "120",
                ["StatusFlags"] = BuildStatusFlags(casterFlags.ToArray()),
                ["ArrowBlades"] = arrow.ToString(CultureInfo.InvariantCulture),
                ["StarFlow"] = "0", ["LegacyCombatGearAttackSpellIndex"] = "0",
                ["LegacyCombatGearAttackRateRoll"] = "99",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1,
                Items = [new CombatGearSnapshotItem(DirectArrowGearId, Level: 1, Exp: 0,
                    Equip: 1, Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        var fields = LegacyPlayerStatusCalculator.CalculateFields(caster.Character, caster.PlayerItems,
            catalog, DirectArrowInitialHp, prestigeLevel: 0);
        Assert.Equal(300U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(301U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        WorldSession CreateTarget(uint uid, long x, long y) => CreateSession(uid: uid,
            name: "DirectArrowTarget" + uid.ToString(CultureInfo.InvariantCulture), mapId: 1_000, x: x, y: y,
            classCode: 2_005, strength: 0, spirit: 0, minHitPoints: DirectArrowInitialHp, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100000", ["BattlePower"] = "120",
            });
        var targets = new Dictionary<uint, WorldSession>
        {
            [TargetPlayerUid] = CreateTarget(TargetPlayerUid, emptyLine ? 848 : 839, 584),
        };
        if (geometry)
        {
            // Horizontal anchor839,584. Hard source-distance15 precedes
            // line-width4: exact15/4 included, next16/5 excluded.
            targets[TargetPlayerUid + 1] = CreateTarget(TargetPlayerUid + 1, 845, 584);
            targets[TargetPlayerUid + 2] = CreateTarget(TargetPlayerUid + 2, 840, 588);
            targets[TargetPlayerUid + 3] = CreateTarget(TargetPlayerUid + 3, 840, 589);
            targets[TargetPlayerUid + 4] = CreateTarget(TargetPlayerUid + 4, 846, 584);
        }
        var sessions = new WorldSessionStore();
        var casterFrames = new ConcurrentQueue<byte[]>();
        var targetFrames = targets.Keys.ToDictionary(uid => uid, _ => new ConcurrentQueue<byte[]>());
        var deliveryOrder = new ConcurrentQueue<byte[]>();
        var observations = new ConcurrentQueue<DirectArrowObservation>();
        ConquerTcpConnection? casterConnection = null;
        var connections = new Dictionary<uint, ConquerTcpConnection>();
        ValueTask Capture(ConcurrentQueue<byte[]> recipientFrames, ReadOnlyMemory<byte> frame)
        {
            var bytes = frame.ToArray();
            recipientFrames.Enqueue(bytes);
            deliveryOrder.Enqueue(bytes);
            string? phase = null;
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update))
            {
                if (update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints))
                {
                    phase = update.Uid == monster.Uid && mixed ? "monster" :
                        update.Uid == TargetPlayerUid ? "player" : null;
                }
                if (update.Uid == PlayerUid && update.Updates.Any(entry =>
                    (uint)entry.Type == 137 && entry.Flag == DirectArrowFireCompositeFlag))
                {
                    phase = "fire";
                }
                if (update.Uid == PlayerUid && update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.AppendIcon && entry.Flag == DirectArrowWaterCompositeFlag))
                {
                    phase = "water";
                }
            }
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                animation.SpellId == DirectArrowSpellId)
            {
                phase = "animation";
            }
            if (phase is not null)
            {
                Assert.True(sessions.TryGet(casterConnection!, out var currentCaster));
                Assert.NotNull(currentCaster);
                Assert.True(sessions.TryGet(connections[TargetPlayerUid], out var currentTarget));
                Assert.NotNull(currentTarget);
                var monsterHp = DirectArrowInitialHp;
                if (mixed)
                {
                    Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var currentMonster));
                    monsterHp = currentMonster.Hitpoints;
                }
                observations.Enqueue(new DirectArrowObservation(phase, monsterHp,
                    currentTarget.Character.MinHitPoints, mixed ? sobStates.GetVisible(sob).Life : 100_000,
                    HasLegacyStatusFlag(currentCaster.Character.Attributes, DirectArrowWaterCompositeFlag),
                    HasLegacyStatusFlag(currentCaster.Character.Attributes, DirectArrowFireCompositeFlag),
                    DirectArrowCounter(currentCaster)));
            }
            return ValueTask.CompletedTask;
        }
        casterConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) => Capture(casterFrames, frame));
        sessions.Set(casterConnection, caster);
        foreach (var (uid, target) in targets)
        {
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
                sendAsync: (frame, _) => Capture(targetFrames[uid], frame));
            connections.Add(uid, connection);
            sessions.Set(connection, target);
        }
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, TextWriter.Synchronized(log), caster,
            WorldData: catalog, Sessions: sessions, MonsterStates: monsterStates, SobNpcStates: sobStates,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var opponent = scenario == "mixed-monster-anchor" ? monster.Uid : TargetPlayerUid;
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: opponent,
                X: 120, Y: 121, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        var finalTargets = new Dictionary<uint, WorldSession>();
        foreach (var (uid, connection) in connections)
        {
            Assert.True(sessions.TryGet(connection, out var target));
            Assert.NotNull(target);
            finalTargets.Add(uid, target);
        }
        var finalMonsterHp = DirectArrowInitialHp;
        if (mixed)
        {
            Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var currentMonster));
            finalMonsterHp = currentMonster.Hitpoints;
        }
        return new DirectArrowResult(finalCaster, finalTargets, monster.Uid, finalMonsterHp,
            mixed ? sobStates.GetVisible(sob).Life : 100_000, casterFrames.ToArray(),
            targetFrames.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
            deliveryOrder.ToArray(), observations.ToArray());
    }

    private sealed record DirectArrowObservation(string Phase, uint MonsterHitpoints, long PlayerHitpoints,
        int SobHitpoints, bool Water, bool Fire, uint Arrow);

    private sealed record DirectArrowResult(WorldSession Caster, IReadOnlyDictionary<uint, WorldSession> Targets,
        uint MonsterUid, uint MonsterHitpoints, int SobHitpoints, byte[][] CasterFrames,
        IReadOnlyDictionary<uint, byte[][]> TargetFrames, byte[][] DeliveryOrder, DirectArrowObservation[] Observations);
}
