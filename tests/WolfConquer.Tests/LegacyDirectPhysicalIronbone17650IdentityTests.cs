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
    // Optional, separate integration horizon: NOT part of the original27 rows.
    [Theory]
    [InlineData("refine", "attacker", "remove")]
    [InlineData("refine", "attacker", "rebind")]
    [InlineData("refine", "target", "remove")]
    [InlineData("refine", "target", "rebind")]
    [InlineData("opening", "target", "remove")]
    [InlineData("opening", "target", "rebind")]
    public async Task DirectIronbone17650IdentityLossDuringPreparationOrOpeningStopsOuterReceive(
        string phase, string participant, string mutation)
    {
        var result = await DirectIronbone17650IdentityRunAsync(phase, participant, mutation);
        Assert.Equal(1, result.MutationCount);
        Assert.NotNull(result.CasterBeforeMutation);
        Assert.NotNull(result.TargetBeforeMutation);
        Assert.Equal(PlayerUid, result.CasterBeforeMutation.Character.Uid);
        Assert.Equal(TargetPlayerUid, result.TargetBeforeMutation.Character.Uid);
        Assert.Equal(100_000, result.CasterBeforeMutation.Character.MinHitPoints);
        Assert.Equal(phase == "opening" ? 50_000 : 100_000,
            result.TargetBeforeMutation.Character.MinHitPoints);

        var affected = participant == "attacker" ? result.Caster : result.Target;
        var other = participant == "attacker" ? result.Target : result.Caster;
        Assert.NotNull(other);
        Assert.Equal(participant == "attacker" ? TargetPlayerUid : PlayerUid, other.Character.Uid);
        Assert.Equal(participant == "attacker" && phase == "opening" ? 50_000 : 100_000,
            other.Character.MinHitPoints);
        if (mutation == "remove")
        {
            Assert.Null(affected);
        }
        else
        {
            Assert.Same(result.Replacement, affected);
            Assert.NotNull(affected);
            Assert.Equal(77_777, affected.Character.MinHitPoints);
            Assert.Equal(77, affected.Character.MinMana);
            Assert.Equal("33", affected.Character.Attributes["Stamina"]);
            Assert.Equal("replacement", affected.Character.Attributes["DirectIronbone17650Identity"]);
        }
        Assert.NotNull(result.Observer);
        Assert.Equal(100_000, result.Observer.Character.MinHitPoints);
        Assert.DoesNotContain(ParseMsgUpdates(result.Order), update =>
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        if (participant == "attacker")
        {
            Assert.Empty(ParseSpellAnimations(result.Order));
            Assert.Empty(result.PublicationRecipients);
        }
        else
        {
            // Target loss rejects only that receive/hit. Source17650 still
            // publishes its valid-endpoint EMPTY cast while caster survives.
            // A removed connection is no viewer; a replacement at832 is now
            // a legitimate viewer, without receiving damage or a hit entry.
            Assert.Equal(mutation == "remove" ? new[] { PlayerUid } :
                new[] { TargetPlayerUid, PlayerUid, TargetPlayerUid }, result.PublicationRecipients);
            var animations = ParseSpellAnimations(result.Order);
            Assert.Equal(mutation == "remove" ? 1 : 3, animations.Count);
            Assert.All(animations, animation =>
            {
                Assert.Equal(17_650, animation.SpellId);
                Assert.Equal(PlayerUid, animation.AttackerUid);
                Assert.Equal(0U, animation.AttackedUid);
                Assert.Equal((840, 584), ((int)animation.X, (int)animation.Y));
                Assert.Empty(animation.Targets);
            });
            Assert.All(result.Order.Where(bytes => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out _)),
                bytes => Assert.Equal((ushort)2239, ReadFrame(bytes).Header.Opcode));
        }
        Assert.DoesNotContain(result.Order, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Physical);

        if (phase == "refine")
        {
            // Original attacker AND target must survive each awaited send:
            // target first, but no remaining caster/target-only-observer fan-out.
            Assert.Equal(new[] { TargetPlayerUid }, result.RefineRecipients);
            var refineBytes = Assert.Single(result.Order, bytes =>
                LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out _));
            Assert.Equal((ushort)2319, ReadFrame(refineBytes).Header.Opcode);
            Assert.True(LegacyMsgRefineEffectPacket.TryParse(ReadFrame(refineBytes), out var refine));
            Assert.Equal(PlayerUid, refine.Id);
            Assert.Equal(0U, refine.Param);
            Assert.Equal(LegacyRefineEffect.LuckyStrike, refine.Effect);
            Assert.DoesNotContain(ParseMsgUpdates(result.Order).SelectMany(update => update.Updates),
                entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == StarArrowReceiveEnvelopeSolidFlag);
            Assert.DoesNotContain(ParseMsgUpdates(result.Order).SelectMany(update => update.Updates),
                entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeSolidFlag));
        }
        else
        {
            Assert.Empty(result.RefineRecipients);
            var archiveBytes = Assert.Single(result.TargetFrames, bytes =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == StarArrowReceiveEnvelopeSolidFlag));
            Assert.Equal((ushort)2176, ReadFrame(archiveBytes).Header.Opcode);
            // Bloodthirst is forced on this low-HP defender AFTER Solid. No
            // downstream heal/HP write may restore or overwrite its registration.
        }
    }

    private static async Task<DirectIronbone17650IdentityResult> DirectIronbone17650IdentityRunAsync(
        string phase, string participant, string mutation)
    {
        const uint helmetId = 111_766;
        const uint observerUid = TargetPlayerUid + 176;
        var refinement = phase == "refine";
        var world = CreateMagicCatalogCore(CreateMonsterFamily(), spawns: [], sobNpcs: [],
            magicRows:
            [
                CreateMagicRow(17_650, sort: 90, useMana: 0, damage: 30_500,
                    experience: 1_000_000, range: 10, useStamina: 0, coolDown: 0, coldTime: 0,
                    damageOnHuman: 38, damage2: 30_205, damage3: 2, damageOnMonster: 7, gDamage: 38),
            ],
            items: [PhysicalPlayerEffectsCreateHelmet(helmetId, refinement ? 1_000U : 0U, immunity: 0)]);
        var caster = CreateSession(mapId: 1_000, classCode: 2_005, strength: 300, spirit: 0,
            minHitPoints: 100_000, minMana: 500, stamina: 100, spellIds: [17_650],
            playerItems: PhysicalPlayerEffectsEquipHelmet(helmetId),
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "120",
                ["LegacyCombatGearAttackSpellIndex"] = "0", ["LegacyCombatGearAttackRateRoll"] = "0",
                ["LegacyPhysicalRefineryRoll"] = "0", ["LegacyPhysicalPlayerLuckyBranchRoll"] = "0",
                ["LegacyPhysicalLuckyStrikeRoll"] = "0", ["LegacyPhysicalPlayerCriticalRoll"] = "0",
                ["PerfectionStatus.LuckyStrike"] = "100",
            }) with
        {
            CombatGear = CombatGearSnapshot.Empty with
            {
                Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                    Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
            },
        };
        var target = CreateSession(uid: TargetPlayerUid, name: "DirectIronbone17650IdentityTarget",
            mapId: 1_000, x: 839, y: 584, classCode: 2_005, strength: 0, spirit: 0,
            minHitPoints: refinement ? 100_000 : 50_000, spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["MaxHitpoints"] = "100000", ["BattlePower"] = "120",
                ["LegacySpeffectLevel.Solid"] = "4", ["LegacySpeffectRoll.Solid"] = "0",
                ["LegacySpeffectLevel.Bloodthirst"] = "4", ["LegacySpeffectRoll.Bloodthirst"] = "0",
                ["PerfectionStatus.StrikeLock"] = "0", ["LegacyPhysicalPlayerStrikeLockRoll"] = "0",
            });
        var observer = CreateSession(uid: observerUid, name: "DirectIronbone17650IdentityObserver",
            mapId: 1_000, x: 856, y: 584, classCode: 2_005, strength: 0, spirit: 0,
            minHitPoints: 100_000, spellIds: [], attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "100000",
            });
        var replacement = CreateSession(uid: TargetPlayerUid + 2_765, name: "DirectIronbone17650IdentityReplacement",
            mapId: 1_000, x: 832, y: 584, minHitPoints: 77_777, minMana: 77, stamina: 33, spellIds: [],
            attributes: new Dictionary<string, string>
            {
                ["MaxHitpoints"] = "77777", ["DirectIronbone17650Identity"] = "replacement",
            });
        var fields = LegacyPlayerStatusCalculator.CalculateFields(caster.Character, caster.PlayerItems, world, 100_000, prestigeLevel: 0);
        var targetFields = LegacyPlayerStatusCalculator.CalculateFields(target.Character, target.PlayerItems, world,
            checked((uint)target.Character.MinHitPoints), prestigeLevel: 0);
        Assert.Equal(300U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(301U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(refinement ? 1_000U : 0U, fields[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
        Assert.Equal(0U, fields[LegacyPlayerStatusPacket.BreakthroughIndex]);
        Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.ImmunityIndex]);
        var sessions = new WorldSessionStore();
        var order = new ConcurrentQueue<byte[]>();
        var targetFrames = new ConcurrentQueue<byte[]>();
        var refineRecipients = new ConcurrentQueue<uint>();
        var publicationRecipients = new ConcurrentQueue<uint>();
        var connections = new Dictionary<uint, ConquerTcpConnection>();
        var mutationCount = 0;
        WorldSession? casterBeforeMutation = null;
        WorldSession? targetBeforeMutation = null;
        ValueTask Capture(uint uid, ReadOnlyMemory<byte> memory)
        {
            var bytes = memory.ToArray();
            order.Enqueue(bytes);
            if (uid == TargetPlayerUid)
            {
                targetFrames.Enqueue(bytes);
            }
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) && animation.SpellId == 17_650)
            {
                publicationRecipients.Enqueue(uid);
            }
            var refine = LegacyMsgRefineEffectPacket.TryParse(ReadFrame(bytes), out var refinePacket);
            if (refine)
            {
                refineRecipients.Enqueue(uid);
            }
            var solid = LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == StarArrowReceiveEnvelopeSolidFlag);
            if (uid == TargetPlayerUid && (refinement
                    ? refine && refinePacket.Effect == LegacyRefineEffect.LuckyStrike
                    : solid) && Interlocked.CompareExchange(ref mutationCount, 1, 0) == 0)
            {
                Assert.True(sessions.TryGet(connections[PlayerUid], out casterBeforeMutation));
                Assert.True(sessions.TryGet(connections[TargetPlayerUid], out targetBeforeMutation));
                Assert.NotNull(casterBeforeMutation);
                Assert.NotNull(targetBeforeMutation);
                var connection = connections[participant == "attacker" ? PlayerUid : TargetPlayerUid];
                if (mutation == "remove")
                {
                    Assert.True(sessions.TryRemove(connection, out var removed));
                    Assert.Same(participant == "attacker" ? casterBeforeMutation : targetBeforeMutation, removed);
                }
                else
                {
                    sessions.Set(connection, replacement);
                }
            }
            return ValueTask.CompletedTask;
        }
        foreach (var (uid, session) in new Dictionary<uint, WorldSession>
                 { [PlayerUid] = caster, [TargetPlayerUid] = target, [observerUid] = observer })
        {
            connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (frame, _) => Capture(uid, frame));
            sessions.Set(connections[uid], session);
        }
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log), caster,
            WorldData: world, Sessions: sessions, MonsterStates: new MonsterRuntimeStateStore(),
            SobNpcStates: new SobNpcRuntimeStateStore(), ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context, ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
            Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121, AttackType: MsgAttackType.Physical,
            Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        sessions.TryGet(connections[PlayerUid], out var finalCaster);
        sessions.TryGet(connections[TargetPlayerUid], out var finalTarget);
        Assert.True(sessions.TryGet(connections[observerUid], out var finalObserver));
        Assert.NotNull(finalObserver);
        return new DirectIronbone17650IdentityResult(finalCaster, finalTarget, finalObserver, replacement,
            order.ToArray(), targetFrames.ToArray(), refineRecipients.ToArray(),
            casterBeforeMutation, targetBeforeMutation, Volatile.Read(ref mutationCount), publicationRecipients.ToArray());
    }

    private sealed record DirectIronbone17650IdentityResult(
        WorldSession? Caster, WorldSession? Target, WorldSession? Observer, WorldSession Replacement,
        byte[][] Order, byte[][] TargetFrames, uint[] RefineRecipients,
        WorldSession? CasterBeforeMutation, WorldSession? TargetBeforeMutation, int MutationCount,
        uint[] PublicationRecipients);
}
