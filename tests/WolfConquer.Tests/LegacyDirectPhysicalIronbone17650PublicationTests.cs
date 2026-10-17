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
    private const int DirectIronbone17650PublicationTargetCount = 32;

    [Theory]
    [InlineData("role", "remove")]
    [InlineData("role", "rebind")]
    [InlineData("self", "remove")]
    [InlineData("self", "rebind")]
    public async Task DirectIronbone17650PublicationRefreshesOriginalAttackerAfterEveryAwaitedSend(
        string phase, string mutation)
    {
        var result = await DirectIronbone17650PublicationRunAsync(phase, mutation);
        Assert.Equal(1, result.MutationCount);
        Assert.NotNull(result.CasterBeforeMutation);
        Assert.Equal(PlayerUid, result.CasterBeforeMutation.Character.Uid);
        Assert.Equal(100_000, result.CasterBeforeMutation.Character.MinHitPoints);
        if (mutation == "remove")
        {
            Assert.Null(result.Caster);
        }
        else
        {
            Assert.Same(result.Replacement, result.Caster);
            Assert.NotNull(result.Caster);
            Assert.Equal(77_777, result.Caster.Character.MinHitPoints);
            Assert.Equal(77, result.Caster.Character.MinMana);
            Assert.Equal("33", result.Caster.Character.Attributes["Stamina"]);
            Assert.Equal("replacement", result.Caster.Character.Attributes["DirectIronbone17650Publication"]);
        }
        Assert.Equal(DirectIronbone17650PublicationTargetCount, result.Targets.Count);
        Assert.All(result.Targets.Values, target => Assert.Equal(99_663, target.Character.MinHitPoints));
        Assert.All(result.Events, item => Assert.True(item.AllTargetsReceived));
        if (phase == "role")
        {
            var initialPeer = Assert.Single(result.Events);
            Assert.NotEqual(PlayerUid, initialPeer.RecipientUid);
            Assert.Contains(initialPeer.RecipientUid, result.Targets.Keys);
            Assert.Equal(30, initialPeer.Animation.Targets.Count);
            // Stops the remaining31 peers, second Role chunk and ALL Send
            // chunks/self deliveries; must not adopt the replacement caster.
        }
        else
        {
            var roleCount = 2 * DirectIronbone17650PublicationTargetCount;
            Assert.Equal(roleCount + 1, result.Events.Length);
            DirectIronbone17650AssertRoleChunks(result.Events.Take(roleCount).ToArray(), result.Targets.Keys);
            Assert.Equal(PlayerUid, result.Events[^1].RecipientUid);
            Assert.Equal(30, result.Events[^1].Animation.Targets.Count);
            // All Role chunks are already delivered. First Send chunk's self
            // callback stops all later peers, second chunk and second self.
        }
        Assert.DoesNotContain(result.Order.Skip(result.MutationFrameIndex + 1), bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        DirectIronbone17650AssertPublicationFrames(result);
    }

    [Fact]
    public async Task DirectIronbone17650PaginationPublishesAllRoleChunksBeforeAnySendChunk()
    {
        var result = await DirectIronbone17650PublicationRunAsync();
        Assert.Equal(0, result.MutationCount);
        Assert.NotNull(result.Caster);
        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        Assert.All(result.Targets.Values, target => Assert.Equal(99_663, target.Character.MinHitPoints));
        Assert.All(result.Events, item => Assert.True(item.AllTargetsReceived));
        var targetUids = result.Targets.Keys.OrderBy(uid => uid).ToArray();
        var roleCount = 2 * DirectIronbone17650PublicationTargetCount;
        var sendChunkCount = DirectIronbone17650PublicationTargetCount + 1;
        Assert.Equal(roleCount + (2 * sendChunkCount), result.Events.Length);
        Assert.Equal(roleCount, Array.FindIndex(result.Events, item => item.RecipientUid == PlayerUid));
        DirectIronbone17650AssertRoleChunks(result.Events.Take(roleCount).ToArray(), targetUids);
        for (var chunk = 0; chunk < 2; chunk++)
        {
            var sends = result.Events.Skip(roleCount + (chunk * sendChunkCount)).Take(sendChunkCount).ToArray();
            Assert.Equal(PlayerUid, sends[0].RecipientUid);
            Assert.Equal(targetUids, sends.Skip(1).Select(item => item.RecipientUid).OrderBy(uid => uid));
            Assert.All(sends, item => Assert.Equal(chunk == 0 ? 30 : 2, item.Animation.Targets.Count));
        }
        foreach (var uid in targetUids.Append(PlayerUid))
        {
            var delivered = result.Events.Where(item => item.RecipientUid == uid).ToArray();
            Assert.Equal(uid == PlayerUid ? new[] { 30, 2 } : new[] { 30, 2, 30, 2 },
                delivered.Select(item => item.Animation.Targets.Count));
            Assert.Equal(targetUids, delivered.Take(2).SelectMany(item => item.Animation.Targets)
                .Select(hit => hit.Uid).OrderBy(value => value));
            if (uid != PlayerUid)
            {
                Assert.Equal(delivered[0].Animation.Targets, delivered[2].Animation.Targets);
                Assert.Equal(delivered[1].Animation.Targets, delivered[3].Animation.Targets);
            }
        }
        Assert.Equal(0L, result.Caster.Character.Experience);
        Assert.All(result.Caster.PlayerSpells.Spells, spell => Assert.Equal(0, spell.Experience));
        Assert.DoesNotContain(result.Order, bytes => LegacySpellPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(ParseMsgUpdates(result.Order).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.Experience);
        DirectIronbone17650AssertPublicationFrames(result);
    }

    private static void DirectIronbone17650AssertRoleChunks(
        DirectIronbone17650PublicationEvent[] events, IEnumerable<uint> targetUids)
    {
        var expected = targetUids.OrderBy(uid => uid).ToArray();
        Assert.Equal(2 * expected.Length, events.Length);
        for (var chunk = 0; chunk < 2; chunk++)
        {
            var role = events.Skip(chunk * expected.Length).Take(expected.Length).ToArray();
            Assert.Equal(expected, role.Select(item => item.RecipientUid).OrderBy(uid => uid));
            Assert.All(role, item => Assert.Equal(chunk == 0 ? 30 : 2, item.Animation.Targets.Count));
        }
    }

    private static void DirectIronbone17650AssertPublicationFrames(DirectIronbone17650PublicationResult result)
    {
        Assert.DoesNotContain(result.Order, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);
        Assert.All(result.Events, item =>
        {
            Assert.Equal(PlayerUid, item.Animation.AttackerUid);
            Assert.Equal(0U, item.Animation.AttackedUid);
            Assert.Equal(DirectIronbone17650SpellId, item.Animation.SpellId);
            Assert.Equal((840, 584), ((int)item.Animation.X, (int)item.Animation.Y));
            Assert.All(item.Animation.Targets, hit =>
            {
                Assert.Equal(337U, hit.Damage);
                Assert.True(hit.Hit);
                Assert.Equal(MsgAttackEffect.None, hit.Effect);
            });
        });
        Assert.All(result.Order.Where(bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _)),
            bytes => Assert.Equal((ushort)2239, ReadFrame(bytes).Header.Opcode));
    }

    private static async Task<DirectIronbone17650PublicationResult> DirectIronbone17650PublicationRunAsync(
        string? phase = null, string? mutation = null)
    {
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), spawns: [], sobNpcs: [], magicRows:
        [
            CreateMagicRow(DirectIronbone17650SpellId, sort: 90, useMana: 0, damage: 30_500,
                experience: 1_000_000, range: 10, useStamina: 0, coolDown: 0, coldTime: 0,
                damageOnHuman: 38, damage2: 30_205, damage3: 2, damageOnMonster: 7, gDamage: 38),
        ]);
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, strength: 300, spirit: 0,
            minHitPoints: 100_000, stamina: 100, spellIds: [DirectIronbone17650SpellId],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0", ["BattlePower"] = "120", ["MaxHitpoints"] = "100000",
                ["LegacyCombatGearAttackSpellIndex"] = "0", ["LegacyCombatGearAttackRateRoll"] = "0",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                    Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        var targets = Enumerable.Range(0, DirectIronbone17650PublicationTargetCount).ToDictionary(
            index => TargetPlayerUid + checked((uint)index), index => CreateSession(
                uid: TargetPlayerUid + index, name: "DirectIronbone17650PublicationTarget" + index,
                mapId: 1_000, x: 839, y: 584, classCode: 2_005, strength: 0, spirit: 0,
                minHitPoints: 100_000, spellIds: [], attributes: new Dictionary<string, string>
                {
                    ["BattlePower"] = "120", ["MaxHitpoints"] = "100000",
                }));
        var fields = LegacyPlayerStatusCalculator.CalculateFields(caster.Character, caster.PlayerItems, world, 100_000, prestigeLevel: 0);
        Assert.Equal(300U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(301U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        var replacement = CreateSession(uid: TargetPlayerUid + 10_765, name: "DirectIronbone17650PublicationReplacement",
            mapId: 1_000, x: 832, y: 584, minHitPoints: 77_777, minMana: 77, stamina: 33, spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "77777", ["DirectIronbone17650Publication"] = "replacement",
            });
        var sessions = new WorldSessionStore();
        var connections = new Dictionary<uint, ConquerTcpConnection>();
        var order = new ConcurrentQueue<byte[]>();
        var events = new ConcurrentQueue<DirectIronbone17650PublicationEvent>();
        var mutationCount = 0;
        var mutationFrameIndex = -1;
        WorldSession? casterBeforeMutation = null;
        ValueTask Capture(uint uid, ReadOnlyMemory<byte> memory)
        {
            var bytes = memory.ToArray();
            order.Enqueue(bytes);
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                animation.SpellId == DirectIronbone17650SpellId)
            {
                var allReceived = targets.Keys.All(targetUid => sessions.TryGet(connections[targetUid], out var current) &&
                    current?.Character.MinHitPoints == 99_663);
                events.Enqueue(new DirectIronbone17650PublicationEvent(uid, animation, allReceived));
                if ((phase == "role" && uid != PlayerUid || phase == "self" && uid == PlayerUid) &&
                    Interlocked.CompareExchange(ref mutationCount, 1, 0) == 0)
                {
                    mutationFrameIndex = order.Count - 1;
                    Assert.True(sessions.TryGet(connections[PlayerUid], out casterBeforeMutation));
                    Assert.NotNull(casterBeforeMutation);
                    if (mutation == "remove")
                    {
                        Assert.True(sessions.TryRemove(connections[PlayerUid], out var removed));
                        Assert.Same(casterBeforeMutation, removed);
                    }
                    else
                    {
                        sessions.Set(connections[PlayerUid], replacement);
                    }
                }
            }
            return ValueTask.CompletedTask;
        }
        foreach (var (uid, session) in targets.Append(new KeyValuePair<uint, WorldSession>(PlayerUid, caster)))
        {
            connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (frame, _) => Capture(uid, frame));
            sessions.Set(connections[uid], session);
        }
        using var log = new StringWriter();
        var terrain = new LegacyGameMapTerrainQuery([new LegacyDMapTerrain(1_000,
            "fixture://ironbone17650-publication.dmap", 900, 620, new DirectIronbone17650Cells(0))]);
        var context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log), caster,
            WorldData: world, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), Terrain: terrain, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121, AttackType: MsgAttackType.Physical,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        sessions.TryGet(connections[PlayerUid], out var finalCaster);
        var finalTargets = targets.Keys.ToDictionary(uid => uid, uid =>
        {
            Assert.True(sessions.TryGet(connections[uid], out var target));
            Assert.NotNull(target);
            return target;
        });
        return new DirectIronbone17650PublicationResult(finalCaster, finalTargets, replacement,
            order.ToArray(), events.ToArray(), casterBeforeMutation, Volatile.Read(ref mutationCount), mutationFrameIndex);
    }

    private sealed record DirectIronbone17650PublicationEvent(
        uint RecipientUid, LegacySpellAnimationPacket Animation, bool AllTargetsReceived);
    private sealed record DirectIronbone17650PublicationResult(
        WorldSession? Caster, IReadOnlyDictionary<uint, WorldSession> Targets, WorldSession Replacement,
        byte[][] Order, DirectIronbone17650PublicationEvent[] Events, WorldSession? CasterBeforeMutation,
        int MutationCount, int MutationFrameIndex);
}
