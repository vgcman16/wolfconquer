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
    private const ushort DirectOverlaySpellId = 17_490;
    private const uint DirectOverlayCasterHelmetId = 111_040;
    private const uint DirectOverlayTargetHelmetId = 111_050;
    private const uint DirectOverlayFrontBreakRuneId = 4_036_709;
    // Fixture-local valid yellow-rune ID, NOT an asserted supplied DB row.
    // The source names Impregnability, but no supplied itemtype row was found.
    private const uint DirectOverlaySyntheticImpregnabilityRuneId = 4_039_909;
    private const int DirectOverlayFrontBreakFlag = 313;

    [Theory]
    [InlineData("front-single")]
    [InlineData("front-persistence-and-guard")]
    [InlineData("front-critical-zero")]
    [InlineData("impregnability-stacks")]
    [InlineData("impregnability-strict-miss")]
    [InlineData("impregnability-lethal-reset")]
    public async Task DirectOverlayPhysicalRunesRetainMutableStatusUntilActualEquipmentRebuild(string scenario)
    {
        var result = await DirectOverlayRunAsync(scenario);
        var impregnability = scenario.StartsWith("impregnability-", StringComparison.Ordinal);
        uint[] expected = scenario switch
        {
            "front-critical-zero" => [450],
            "front-persistence-and-guard" => [582, 582, 450],
            "front-equipment-reset" => [582, 450],
            "impregnability-stacks" => [291, 225],
            "impregnability-lethal-reset" => [291],
            "impregnability-strict-miss" or "impregnability-equipment-reset" => [291, 291],
            _ => [582],
        };
        Assert.Equal(expected, result.Hits.Select(hit => hit.Damage));
        uint[] expectedStatus = scenario switch
        {
            "front-critical-zero" => [60],
            "front-persistence-and-guard" => [66, 66, 66],
            "front-equipment-reset" => [66, 60],
            "impregnability-stacks" => [75, 112],
            "impregnability-strict-miss" => [50, 50],
            "impregnability-equipment-reset" => [75, 75],
            "impregnability-lethal-reset" => [50],
            _ => [66],
        };
        Assert.Equal(expectedStatus, result.PublicStatusValues);
        for (var index = 0; index < result.Hits.Length; index++)
        {
            var hit = result.Hits[index];
            Assert.Equal(result.Opponents[index], hit.Uid);
            Assert.True(hit.Hit);
            Assert.Equal(expected[index] is 582 or 291 ? MsgAttackEffect.Penetration : MsgAttackEffect.None, hit.Effect);
        }
        // Literal source arithmetic, not a call to the private calculator:
        // FrontBreak9 maps10: rawBT60->66. CA65 and then63 penetrate;
        // CA70 must not (flag313 prevents a second augmentation66->72).
        // Base300 ->+30%=390 ->floor(390/.67)=582, no no-break bonus.
        // Impregnability9: CA50->75->112 on two successes. Defender MUST be
        // Reborn2 to equip the rune, so Defense2 halves:390->195/.67=>291;
        // failed breakthrough instead yields300->150 +50%=225.
        // Strict chance20 vs roll20 misses; reset starts the next50->75 anew.
        Assert.Equal(!impregnability && scenario != "front-critical-zero",
            HasLegacyStatusFlag(result.Caster.Character.Attributes, DirectOverlayFrontBreakFlag));
        if (scenario.EndsWith("equipment-reset", StringComparison.Ordinal))
        {
            var equipped = impregnability ? result.Targets[TargetPlayerUid] : result.Caster;
            Assert.Equal(90_002U, Assert.Single(equipped.PlayerItems.EquipmentItems).Uid);
            Assert.Contains(equipped.PlayerItems.InventoryItems, item => item.Uid == 90_001);
            Assert.True(result.ObservedActualEquipRefresh);
        }
        foreach (var (uid, target) in result.Targets)
        {
            var total = result.Hits.Where(hit => hit.Uid == uid).Sum(hit => (long)hit.Damage);
            var initialHp = scenario == "impregnability-lethal-reset" ? 5 : 100_000;
            Assert.Equal(Math.Max(0, initialHp - total), target.Character.MinHitPoints);
        }
        if (scenario == "impregnability-lethal-reset")
            Assert.True(HasLegacyStatusFlag(result.Targets[TargetPlayerUid].Character.Attributes, 5));
        Assert.Equal(500, result.Caster.Character.MinMana);
        Assert.Equal("100", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal(0, result.Caster.Character.Experience);
        Assert.Equal(0, Assert.Single(result.Caster.PlayerSpells.Spells).Experience);
        Assert.DoesNotContain(result.Frames, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) && attack.Query.AttackType == MsgAttackType.Physical);
        Assert.DoesNotContain(result.Frames, bytes => LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
    }

    private static async Task<DirectOverlayResult> DirectOverlayRunAsync(string scenario)
    {
        var impregnability = scenario.StartsWith("impregnability-", StringComparison.Ordinal);
        var reset = scenario.EndsWith("equipment-reset", StringComparison.Ordinal);
        var criticalZero = scenario == "front-critical-zero";
        var casterItem = DirectOverlayItem(DirectOverlayCasterHelmetId, "DirectOverlayCasterHelmet",
            critical: criticalZero ? 0U : 1_000U, breakthrough: impregnability ? 100U : 60U);
        var targetItem = DirectOverlayItem(DirectOverlayTargetHelmetId, "DirectOverlayTargetHelmet",
            counteraction: impregnability ? 50U : 65U);
        var targetItemTwo = DirectOverlayItem(DirectOverlayTargetHelmetId + 1, "DirectOverlayTargetHelmetTwo", counteraction: 63);
        var targetItemThree = DirectOverlayItem(DirectOverlayTargetHelmetId + 2, "DirectOverlayTargetHelmetThree", counteraction: 70);
        var frontRune = DirectOverlayItem(DirectOverlayFrontBreakRuneId, "Front Break (+9)");
        var impregRune = DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)");
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
        [
            CreateMagicRow(DirectOverlaySpellId, sort: 99, useMana: 0, damage: 30_250,
                range: 4, coolDown: 0, coldTime: 0, damageOnHuman: 0, damage2: 33,
                damage3: 2, damageOnMonster: 30_085, gDamage: 0),
        ], items: [casterItem, targetItem, targetItemTwo, targetItemThree, frontRune, impregRune]);
        PlayerItemSnapshot Items(uint itemId, uint? runeId = null, bool withReplacement = false) =>
            PlayerItemSnapshot.Empty with
            {
                EquipmentItems = [new PlayerItemSnapshotItem
                {
                    Uid = 90_001, ItemId = itemId, Position = 1, Durability = 100, MaximumDurability = 100,
                }],
                InventoryItems = withReplacement ? [new PlayerItemSnapshotItem
                {
                    Uid = 90_002, ItemId = itemId, Position = 0, Durability = 100, MaximumDurability = 100,
                }] : [],
                RuneItems = runeId is { } rune ? [new PlayerItemSnapshotItem
                {
                    Uid = 90_003, ItemId = rune, Position = 103,
                }] : [],
            };
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, strength: 300, spirit: 0,
            reborn: impregnability ? 0 : 2, minHitPoints: 100_000, stamina: 100, spellIds: [DirectOverlaySpellId],
            playerItems: Items(DirectOverlayCasterHelmetId,
                impregnability ? null : DirectOverlayFrontBreakRuneId, withReplacement: reset && !impregnability),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "100",
                ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = "99",
                ["LegacyPhysicalRefineryRoll"] = "0", ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99",
                ["LegacyPhysicalPlayerCriticalRoll"] = "99", ["DashRate"] = "0",
                ["LegacyPhysicalDashRoll"] = "0", ["LegacyPhysicalBreakthroughRefineryRoll"] = "0",
                // New deterministic roll seam only; this is NOT desired stat input.
                ["LegacyPhysicalImpregnabilityRoll"] = scenario == "impregnability-strict-miss" ? "20" : "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0,
                    Equip: 1, Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        WorldSession Target(uint uid, long x, long y, uint helmetId) => CreateSession(uid: uid,
            name: "DirectOverlayTarget" + uid.ToString(CultureInfo.InvariantCulture),
            mapId: 1_000, x: x, y: y, classCode: 2_005, strength: 0, spirit: 0,
            reborn: impregnability ? 2 : 0,
            minHitPoints: scenario == "impregnability-lethal-reset" ? 5 : 100_000, spellIds: [],
            playerItems: Items(helmetId, impregnability ? DirectOverlaySyntheticImpregnabilityRuneId : null,
                withReplacement: reset && impregnability),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100000", ["BattlePower"] = "200",
                ["LegacyPhysicalImpregnabilityRoll"] = scenario == "impregnability-strict-miss" ? "20" : "0",
            });
        var targets = new Dictionary<uint, WorldSession>
        {
            [TargetPlayerUid] = Target(TargetPlayerUid, 839, 584, DirectOverlayTargetHelmetId),
        };
        if (scenario == "front-persistence-and-guard")
        {
            // Three orthogonal rays share no off-ray target within width2.
            targets[TargetPlayerUid + 1] = Target(TargetPlayerUid + 1, 830, 593, DirectOverlayTargetHelmetId + 1);
            targets[TargetPlayerUid + 2] = Target(TargetPlayerUid + 2, 821, 584, DirectOverlayTargetHelmetId + 2);
        }
        var casterFields = LegacyPlayerStatusCalculator.CalculateFields(caster.Character, caster.PlayerItems, world, 100_000, 0);
        Assert.Equal(300U, casterFields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(301U, casterFields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(criticalZero ? 0U : 1_000U, casterFields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
        Assert.Equal(impregnability ? 100U : 60U, casterFields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        foreach (var (uid, target) in targets)
        {
            var fields = LegacyPlayerStatusCalculator.CalculateFields(target.Character, target.PlayerItems, world, 100_000, 0);
            Assert.Equal(0U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
            Assert.Equal(0U, fields[LegacyPlayerStatusPacket.ImmunityIndex]);
            Assert.Equal(impregnability ? 50U : uid == TargetPlayerUid ? 65U : uid == TargetPlayerUid + 1 ? 63U : 70U,
                fields[LegacyPlayerStatusPacket.CounteractionIndex]);
        }
        var sessions = new WorldSessionStore();
        var frames = new ConcurrentQueue<byte[]>();
        var casterFrames = new ConcurrentQueue<byte[]>();
        var connections = new Dictionary<uint, ConquerTcpConnection>();
        foreach (var uid in targets.Keys.Append(PlayerUid))
        {
            connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    frames.Enqueue(bytes);
                    if (uid == PlayerUid) casterFrames.Enqueue(bytes);
                    return ValueTask.CompletedTask;
                });
            sessions.Set(connections[uid], uid == PlayerUid ? caster : targets[uid]);
        }
        using var log = new StringWriter();
        var clock = new DirectOverlayFrozenClock();
        var baseContext = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log), caster,
            WorldData: world, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), TimeProvider: clock,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        uint[] opponents = scenario == "front-persistence-and-guard"
            ? [TargetPlayerUid, TargetPlayerUid + 1, TargetPlayerUid + 2]
            : reset || impregnability && scenario != "impregnability-lethal-reset"
                ? [TargetPlayerUid, TargetPlayerUid] : [TargetPlayerUid];
        var hits = new List<LegacySpellAnimationTarget>();
        var statusValues = new List<uint>();
        var actualEquip = false;
        for (var index = 0; index < opponents.Length; index++)
        {
            if (index == 1 && reset)
            {
                var actorUid = impregnability ? TargetPlayerUid : PlayerUid;
                Assert.True(sessions.TryGet(connections[actorUid], out var actor));
                Assert.NotNull(actor);
                var before = frames.Count;
                await PacketHandlers.HandleAsync(baseContext with { Connection = connections[actorUid], Session = actor },
                    ReadFrame(new MsgItemUsagePacket(Id: 90_002, DwParam: 1, Action: MsgItemUsageAction.Equip,
                        Timestamp: 0, DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes()));
                Assert.True(sessions.TryGet(connections[actorUid], out var equippedActor));
                Assert.NotNull(equippedActor);
                Assert.Equal(90_002U, Assert.Single(equippedActor.PlayerItems.EquipmentItems).Uid);
                actualEquip = frames.ToArray().Skip(before).Any(bytes =>
                    LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
                if (!impregnability)
                    Assert.True(HasLegacyStatusFlag(equippedActor.Character.Attributes, DirectOverlayFrontBreakFlag));
            }
            Assert.True(sessions.TryGet(connections[PlayerUid], out var activeCaster));
            Assert.NotNull(activeCaster);
            var frameStart = casterFrames.Count;
            await PacketHandlers.HandleAsync(baseContext with { Session = activeCaster },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: opponents[index],
                    X: 120, Y: 121, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
            var animation = Assert.Single(ParseSpellAnimations(casterFrames.ToArray().Skip(frameStart).ToArray()),
                packet => packet.SpellId == DirectOverlaySpellId);
            hits.Add(Assert.Single(animation.Targets));
            // Actual status query exposes source mutable Status, not the
            // private overlay or a test-computed expected calculator result.
            Assert.True(sessions.TryGet(connections[PlayerUid], out var queryCaster));
            Assert.NotNull(queryCaster);
            var queriedUid = impregnability ? TargetPlayerUid : PlayerUid;
            var statusStart = casterFrames.Count;
            await PacketHandlers.HandleAsync(baseContext with { Session = queryCaster },
                ReadFrame(new LegacyPlayerStatusQueryPacket(queriedUid).ToFrameBytes()));
            var statusBytes = Assert.Single(casterFrames.ToArray().Skip(statusStart), bytes =>
                LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
            Assert.Equal(queriedUid, status.Uid);
            statusValues.Add(impregnability ? status.Counteraction : status.Breakthrough);
        }
        Assert.True(sessions.TryGet(connections[PlayerUid], out var finalCaster));
        Assert.NotNull(finalCaster);
        var finalTargets = targets.Keys.ToDictionary(uid => uid, uid =>
        {
            Assert.True(sessions.TryGet(connections[uid], out var target));
            Assert.NotNull(target);
            return target;
        });
        return new DirectOverlayResult(finalCaster, finalTargets, opponents, hits.ToArray(),
            statusValues.ToArray(), frames.ToArray(), actualEquip);
    }

    private static ItemType DirectOverlayItem(uint id, string name,
        uint critical = 0, uint breakthrough = 0, uint counteraction = 0)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[39] = critical.ToString(CultureInfo.InvariantCulture);
        fields[44] = breakthrough.ToString(CultureInfo.InvariantCulture);
        fields[45] = counteraction.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)id), name, fields);
    }

    private sealed class DirectOverlayFrozenClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed record DirectOverlayResult(WorldSession Caster, IReadOnlyDictionary<uint, WorldSession> Targets,
        uint[] Opponents, LegacySpellAnimationTarget[] Hits, uint[] PublicStatusValues,
        byte[][] Frames, bool ObservedActualEquipRefresh);
}
