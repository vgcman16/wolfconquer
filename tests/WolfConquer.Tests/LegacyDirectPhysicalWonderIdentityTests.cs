using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectWonderIdentityPreparationStopsWhenRealCounterCasterIsRemovedOrRebound(bool replace)
    {
        var result = await DirectWonderIdentityRunAsync(replace);

        Assert.Equal(1, result.MutationCount);
        var wonder = Assert.Single(ParseSpellAnimations(result.TargetFrames),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(TargetPlayerUid, wonder.AttackerUid);
        Assert.Equal(0U, wonder.AttackedUid);
        Assert.Equal((0, 0), (wonder.X, wonder.Y));
        var counterHit = Assert.Single(wonder.Targets, target => target.Uid == PlayerUid);
        Assert.True(counterHit.Damage > 0);

        Assert.NotNull(result.CounterCasterBeforeMutation);
        Assert.Equal(TargetPlayerUid, result.CounterCasterBeforeMutation.Character.Uid);
        Assert.Equal(493, result.CounterCasterBeforeMutation.Character.MinMana);
        Assert.Equal("95", result.CounterCasterBeforeMutation.Character.Attributes["Stamina"]);
        Assert.True(result.CounterCasterBeforeMutation.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        Assert.Equal(PlayerUid, result.Attacker.Character.Uid);
        Assert.Equal(100_000L - counterHit.Damage, result.Attacker.Character.MinHitPoints);

        if (replace)
        {
            Assert.Same(result.Replacement, result.RegisteredTarget);
            Assert.Equal(77_777, result.RegisteredTarget!.Character.MinHitPoints);
            Assert.Equal("replacement", result.RegisteredTarget.Character.Attributes["DirectWonderIdentityMarker"]);
        }
        else
        {
            Assert.Null(result.RegisteredTarget);
        }

        // Source MsgAttackPacket.cs:645-659 holds the original Player references.
        // A connection occupant substituted during BackDmg is not that target.
        // Detect this before publishing the original incoming interaction.
        Assert.DoesNotContain(result.Frames, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical &&
            attack.Query.Uid == PlayerUid && attack.Query.OpponentUid == TargetPlayerUid);
        Assert.DoesNotContain(ParseMsgUpdates(result.Frames), update =>
            update.Uid == result.Replacement.Character.Uid);
    }

    private static async Task<DirectWonderIdentityResult> DirectWonderIdentityRunAsync(bool replace)
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 1_000));
        var attacker = CreateSession(mapId: 1_000, classCode: 9_001, strength: 300, spirit: 0,
            minHitPoints: 100_000, stamina: 100, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
            });
        var target = CreateSession(uid: TargetPlayerUid, name: "DirectWonderIdentityTarget", mapId: 1_000,
            x: 832, y: 584, classCode: 9_001, strength: 1_000, spirit: 0,
            minMana: 500, minHitPoints: 100_000, stamina: 100,
            spellIds: [HeavensWonderRuntimeSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
                [CounterKill16350BackDamageRollAttribute] = "0",
            });
        var replacement = CreateSession(uid: TargetPlayerUid + 1_000, name: "DirectWonderIdentityReplacement",
            mapId: 1_000, x: 832, y: 584, minHitPoints: 77_777, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "77777",
                ["DirectWonderIdentityMarker"] = "replacement",
            });
        var sessions = new WorldSessionStore();
        var frames = new ConcurrentQueue<byte[]>();
        var targetFrames = new ConcurrentQueue<byte[]>();
        var mutationCount = 0;
        WorldSession? counterCasterBeforeMutation = null;
        ConquerTcpConnection? targetConnection = null;
        var attackerConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                frames.Enqueue(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        targetConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9959),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                frames.Enqueue(bytes);
                targetFrames.Enqueue(bytes);
                var parsed = ReadFrame(bytes);
                if (parsed.Header.Opcode == 2239 &&
                    LegacySpellAnimationPacket.TryParse(parsed, out var animation) &&
                    animation.SpellId == HeavensWonderRuntimeSpellId &&
                    animation.AttackerUid == TargetPlayerUid &&
                    Interlocked.CompareExchange(ref mutationCount, 1, 0) == 0)
                {
                    // CounterKill16350.cs publishes this final animation after its
                    // receive/resource writes. The real rebind must survive return
                    // to BackDmg, not be overwritten by a later counter write.
                    Assert.True(sessions.TryGet(targetConnection!, out counterCasterBeforeMutation));
                    Assert.NotNull(counterCasterBeforeMutation);
                    if (replace)
                    {
                        sessions.Set(targetConnection!, replacement);
                    }
                    else
                    {
                        Assert.True(sessions.TryRemove(targetConnection!, out var removed));
                        Assert.Same(counterCasterBeforeMutation, removed);
                    }
                }

                return ValueTask.CompletedTask;
            });
        sessions.Set(attackerConnection, attacker);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(attackerConnection, TextWriter.Synchronized(log), attacker,
            WorldData: worldData, Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));

        Assert.True(sessions.TryGet(attackerConnection, out var currentAttacker));
        Assert.NotNull(currentAttacker);
        sessions.TryGet(targetConnection, out var registeredTarget);
        return new DirectWonderIdentityResult(currentAttacker, registeredTarget, replacement,
            counterCasterBeforeMutation, frames.ToArray(), targetFrames.ToArray(),
            Volatile.Read(ref mutationCount));
    }

    private sealed record DirectWonderIdentityResult(
        WorldSession Attacker,
        WorldSession? RegisteredTarget,
        WorldSession Replacement,
        WorldSession? CounterCasterBeforeMutation,
        byte[][] Frames,
        byte[][] TargetFrames,
        int MutationCount);
}
