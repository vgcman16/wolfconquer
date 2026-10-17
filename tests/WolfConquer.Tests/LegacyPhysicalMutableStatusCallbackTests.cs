using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("attacker", false)]
    [InlineData("attacker", true)]
    [InlineData("target", false)]
    [InlineData("target", true)]
    public async Task DirectOverlay313PublicationStopsAfterEitherOriginalIdentityIsLost(string actor, bool rebind)
    {
        var result = await DirectOverlayCallbackRunAsync(17_490, actor, rebind, initialCriticalZero: false);
        Assert.True(result.Mutated);
        Assert.Equal(new[] { PlayerUid }, result.FrontRecipients);
        Assert.Equal(66U, result.CommittedBreakthrough);
        Assert.Equal(1_000U, result.EntryCritical);
        Assert.Equal(100_000, result.TargetHpAtCallback);
        Assert.Empty(ParseSpellAnimations(result.Order));
        Assert.DoesNotContain(ParseMsgUpdates(result.Order).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints);
        Assert.DoesNotContain(ParseMsgUpdates(result.Order).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == StarArrowReceiveEnvelopeSolidFlag);
        Assert.DoesNotContain(result.Order, bytes => LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
        var lostUid = actor == "attacker" ? PlayerUid : TargetPlayerUid;
        if (rebind)
        {
            Assert.True(result.Sessions.TryGet(result.Connections[lostUid], out var replacement));
            Assert.Same(result.Replacement, replacement);
            Assert.NotNull(replacement);
            Assert.Equal(77_777, replacement.Character.MinHitPoints);
            Assert.Equal(77, replacement.Character.MinMana);
            Assert.Equal("33", replacement.Character.Attributes["Stamina"]);
            Assert.Equal("replacement-exact", replacement.Character.Attributes["DirectOverlayCallbackMarker"]);
        }
        else
        {
            Assert.False(result.Sessions.TryGet(result.Connections[lostUid], out _));
        }
        var retainedUid = actor == "attacker" ? TargetPlayerUid : PlayerUid;
        Assert.True(result.Sessions.TryGet(result.Connections[retainedUid], out var retained));
        Assert.NotNull(retained);
        Assert.Equal(100_000, retained.Character.MinHitPoints);
        if (retainedUid == TargetPlayerUid)
        {
            Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(retained).Counteraction);
            Assert.False(HasLegacyStatusFlag(retained.Character.Attributes, StarArrowReceiveEnvelopeSolidFlag));
        }
        DirectOverlayCallbackAssertNoOrdinary(result);
    }

    [Theory]
    [InlineData((ushort)17_490, 337U)]
    [InlineData((ushort)17_650, 252U)]
    public async Task DirectOverlay313ActualSameUidCriticalZeroEquipStillContinuesTheEnteredDashBlock(
        ushort spellId, uint expectedDamage)
    {
        var result = await DirectOverlayCallbackRunAsync(spellId, "equip", false, initialCriticalZero: false);
        Assert.True(result.Mutated);
        Assert.Equal(1_000U, result.EntryCritical);
        Assert.Equal(66U, result.CommittedBreakthrough);
        Assert.Equal(100_000, result.TargetHpAtCallback);
        Assert.Equal(3, result.FrontRecipients.Length);
        Assert.Equal(PlayerUid, result.FrontRecipients[0]);
        Assert.Equal(new[] { TargetPlayerUid, PlayerUid + 50 }.OrderBy(uid => uid),
            result.FrontRecipients.Skip(1).OrderBy(uid => uid));
        Assert.True(result.Sessions.TryGet(result.Connections[PlayerUid], out var caster));
        Assert.NotNull(caster);
        Assert.Equal(90_002U, Assert.Single(caster.PlayerItems.EquipmentItems).Uid);
        Assert.True(HasLegacyStatusFlag(caster.Character.Attributes, DirectOverlayFrontBreakFlag));
        var rawCaster = LegacyPlayerStatusCalculator.CalculateFields(
            caster.Character, caster.PlayerItems, result.World, 100_000, 0);
        Assert.Equal(0U, rawCaster[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
        Assert.Equal(60U, rawCaster[LegacyPlayerStatusPacket.BreakthroughIndex]);
        Assert.Equal(60U, LegacyPhysicalStatusOverlay.ForOwner(caster).Breakthrough ?? 60U);
        Assert.Equal(60U, result.PublicCasterStatus.Breakthrough);
        Assert.Equal(0U, result.PublicCasterStatus.CriticalStrike);
        Assert.True(result.Sessions.TryGet(result.Connections[TargetPlayerUid], out var target));
        Assert.NotNull(target);
        Assert.Equal(75U, result.PublicTargetStatus.Counteraction); // Independent Imp call still runs inside entered block.
        Assert.Equal(100_000 - expectedDamage, target.Character.MinHitPoints);
        var animation = Assert.Single(ParseSpellAnimations(result.Frames[PlayerUid]), packet => packet.SpellId == spellId);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, hit.Uid);
        Assert.Equal(expectedDamage, hit.Damage);
        Assert.Equal(MsgAttackEffect.Dash, hit.Effect);
        // Raw300 -> Dash450 -> rb2 Defense2 half225 -> +floor112 =337.
        // Ironbone's CALLER then floors337*75/100=252. Not a calculator oracle.
        Assert.DoesNotContain(result.Order, bytes => LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
        DirectOverlayCallbackAssertNoOrdinary(result);
    }

    [Theory]
    [InlineData((ushort)17_490, 225U)]
    [InlineData((ushort)17_650, 168U)]
    public async Task DirectOverlayInitialCriticalZeroSkipsBothMutationAndDashDespiteGuaranteedDash(
        ushort spellId, uint expectedDamage)
    {
        var result = await DirectOverlayCallbackRunAsync(spellId, "equip", false, initialCriticalZero: true);
        Assert.False(result.Mutated);
        Assert.Empty(result.FrontRecipients);
        Assert.Equal(50U, result.PublicTargetStatus.Counteraction);
        var animation = Assert.Single(ParseSpellAnimations(result.Frames[PlayerUid]), packet => packet.SpellId == spellId);
        var hit = Assert.Single(animation.Targets);
        Assert.Equal(expectedDamage, hit.Damage);
        Assert.Equal(MsgAttackEffect.None, hit.Effect);
        Assert.True(result.Sessions.TryGet(result.Connections[PlayerUid], out var caster));
        Assert.NotNull(caster);
        Assert.False(HasLegacyStatusFlag(caster.Character.Attributes, DirectOverlayFrontBreakFlag));
        DirectOverlayCallbackAssertNoOrdinary(result);
    }

    private static async Task<DirectOverlayCallbackResult> DirectOverlayCallbackRunAsync(
        ushort spellId, string mutation, bool rebind, bool initialCriticalZero)
    {
        const uint zeroHelmetId = DirectOverlayCasterHelmetId + 1;
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
        [
            spellId == 17_650
                ? CreateMagicRow(spellId, sort: 90, useMana: 0, damage: 30_500, range: 10,
                    damageOnHuman: 38, damage2: 30_205, damage3: 2, damageOnMonster: 7, gDamage: 38,
                    coolDown: 0, coldTime: 0)
                : CreateMagicRow(spellId, sort: 99, useMana: 0, damage: 30_250, range: 4,
                    damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085, gDamage: 0,
                    coolDown: 0, coldTime: 0),
        ], items:
        [
            DirectOverlayItem(DirectOverlayCasterHelmetId, "OverlayCallbackCritical", critical: initialCriticalZero ? 0U : 1_000U, breakthrough: 60),
            DirectOverlayItem(zeroHelmetId, "OverlayCallbackCriticalZero", breakthrough: 60),
            DirectOverlayItem(DirectOverlayTargetHelmetId, "OverlayCallbackTarget", counteraction: 50),
            DirectOverlayItem(DirectOverlayFrontBreakRuneId, "Front Break (+9)"),
            DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)"),
        ]);
        PlayerItemSnapshot Items(uint helmet, uint rune, bool replacement) => PlayerItemSnapshot.Empty with
        {
            EquipmentItems = [new PlayerItemSnapshotItem { Uid = 90_001, ItemId = helmet, Position = 1, Durability = 100, MaximumDurability = 100 }],
            InventoryItems = replacement ? [new PlayerItemSnapshotItem { Uid = 90_002, ItemId = zeroHelmetId, Durability = 100, MaximumDurability = 100 }] : [],
            RuneItems = [new PlayerItemSnapshotItem { Uid = 90_003, ItemId = rune, Position = 103 }],
        };
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2,
            strength: 300, spirit: 0, minHitPoints: 100_000, stamina: 100, spellIds: [spellId],
            playerItems: Items(DirectOverlayCasterHelmetId, DirectOverlayFrontBreakRuneId, true),
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "120",
                ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = "0",
                ["LegacyPhysicalRefineryRoll"] = "0", ["LegacyPhysicalPlayerCriticalRoll"] = "99",
                ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99", ["DashRate"] = "10000", ["LegacyPhysicalDashRoll"] = "0",
                ["LegacyPhysicalBreakthroughRefineryRoll"] = "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                    Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        var target = CreateSession(uid: TargetPlayerUid, mapId: 1_000, x: 839, y: 584,
            classCode: 2_005, reborn: 2, strength: 0, spirit: 0, minHitPoints: 100_000, spellIds: [],
            playerItems: Items(DirectOverlayTargetHelmetId, DirectOverlaySyntheticImpregnabilityRuneId, false),
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "100000", ["BattlePower"] = "120", ["LegacyPhysicalImpregnabilityRoll"] = "0",
                ["LegacySpeffectLevel.Solid"] = mutation == "equip" ? "0" : "4", ["LegacySpeffectRoll.Solid"] = "0",
            });
        var observer = CreateSession(uid: PlayerUid + 50, mapId: 1_000, x: 831, y: 588,
            strength: 0, spirit: 0, minHitPoints: 100_000, spellIds: []);
        var initial = new Dictionary<uint, WorldSession>
        {
            [PlayerUid] = caster, [TargetPlayerUid] = target, [PlayerUid + 50] = observer,
        };
        var sessions = new WorldSessionStore();
        var captures = initial.Keys.ToDictionary(uid => uid, _ => new ConcurrentQueue<byte[]>());
        var order = new ConcurrentQueue<byte[]>();
        var frontRecipients = new ConcurrentQueue<uint>();
        var connections = new Dictionary<uint, ConquerTcpConnection>();
        WorldPacketHandlerContext context = null!;
        var mutated = 0;
        uint entryCritical = 0, committedBreakthrough = 0;
        long targetHpAtCallback = 0;
        WorldSession? replacementSession = null;
        async ValueTask CaptureAsync(uint uid, ReadOnlyMemory<byte> memory)
        {
            var bytes = memory.ToArray();
            captures[uid].Enqueue(bytes);
            order.Enqueue(bytes);
            if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) || update.Uid != PlayerUid ||
                !update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is { } values && IsLegacyStatusFlagSet(values, DirectOverlayFrontBreakFlag)))
                return;
            frontRecipients.Enqueue(uid);
            if (uid != PlayerUid || Interlocked.CompareExchange(ref mutated, 1, 0) != 0) return;
            Assert.True(sessions.TryGet(connections[PlayerUid], out var currentCaster));
            Assert.True(sessions.TryGet(connections[TargetPlayerUid], out var currentTarget));
            Assert.NotNull(currentCaster);
            Assert.NotNull(currentTarget);
            entryCritical = LegacyPlayerStatusCalculator.CalculateFields(
                currentCaster.Character, currentCaster.PlayerItems, world, 100_000, 0)[LegacyPlayerStatusPacket.CriticalStrikeIndex];
            committedBreakthrough = LegacyPhysicalStatusOverlay.ForOwner(currentCaster).Breakthrough ?? 0;
            targetHpAtCallback = currentTarget.Character.MinHitPoints;
            Assert.Equal(context.TimeProvider!.GetUtcNow().AddSeconds(10),
                LegacyPhysicalStatusOverlay.ForOwner(currentCaster).FrontBreakExpiresAtUtc);
            if (mutation == "equip")
            {
                await PacketHandlers.HandleAsync(context with { Session = currentCaster },
                    ReadFrame(new MsgItemUsagePacket(90_002, 1, MsgItemUsageAction.Equip, 0, 0, 0, 0).ToFrameBytes()));
                return;
            }
            var lostUid = mutation == "attacker" ? PlayerUid : TargetPlayerUid;
            Assert.True(sessions.TryRemove(connections[lostUid], out var removed));
            Assert.NotNull(removed);
            if (!rebind) return;
            replacementSession = removed with
            {
                Character = removed.Character with
                {
                    Uid = removed.Character.Uid + 1_000, MinHitPoints = 77_777, MinMana = 77,
                    Attributes = new Dictionary<string, string>(removed.Character.Attributes)
                    {
                        ["Stamina"] = "33", ["DirectOverlayCallbackMarker"] = "replacement-exact",
                    },
                },
                PhysicalStatusOverlay = null, FineRainEquipmentAdmission = null,
            };
            sessions.Set(connections[lostUid], replacementSession);
        }
        foreach (var uid in initial.Keys)
        {
            connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (memory, _) => CaptureAsync(uid, memory));
            sessions.Set(connections[uid], initial[uid]);
        }
        using var log = new StringWriter();
        context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log), caster,
            WorldData: world, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), TimeProvider: new DirectOverlayFrozenClock(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121, AttackType: MsgAttackType.Physical,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        var casterStatus = new LegacyPlayerStatusPacket(new uint[LegacyPlayerStatusPacket.FieldCount]);
        var targetStatus = new LegacyPlayerStatusPacket(new uint[LegacyPlayerStatusPacket.FieldCount]);
        if (mutation == "equip")
        {
            Assert.True(sessions.TryGet(connections[PlayerUid], out var current));
            Assert.NotNull(current);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var start = captures[PlayerUid].Count;
                await PacketHandlers.HandleAsync(context with { Session = current },
                    ReadFrame(new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes()));
                var frame = Assert.Single(captures[PlayerUid].ToArray().Skip(start), bytes =>
                    LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
                Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out var status));
                if (uid == PlayerUid) casterStatus = status; else targetStatus = status;
            }
        }
        return new DirectOverlayCallbackResult(world, sessions, connections,
            captures.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()), order.ToArray(),
            frontRecipients.ToArray(), mutated != 0, entryCritical, committedBreakthrough, targetHpAtCallback,
            replacementSession, casterStatus, targetStatus);
    }

    private static void DirectOverlayCallbackAssertNoOrdinary(DirectOverlayCallbackResult result)
    {
        Assert.DoesNotContain(result.Order, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
    }

    private sealed record DirectOverlayCallbackResult(WorldDataCatalog World, WorldSessionStore Sessions,
        IReadOnlyDictionary<uint, ConquerTcpConnection> Connections, IReadOnlyDictionary<uint, byte[][]> Frames,
        byte[][] Order, uint[] FrontRecipients, bool Mutated, uint EntryCritical, uint CommittedBreakthrough,
        long TargetHpAtCallback, WorldSession? Replacement, LegacyPlayerStatusPacket PublicCasterStatus,
        LegacyPlayerStatusPacket PublicTargetStatus);
}
