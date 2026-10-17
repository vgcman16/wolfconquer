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
    [InlineData("shuriken-vortex", LegacyPhysicalSpellsShurikenVortexFlag)]
    [InlineData("maniac-dance", LegacyPhysicalSpellsManiacDanceFlag)]
    [InlineData("wild-phoenix", DefensiveStanceStatusFlag)]
    [InlineData("normal-revenge-tail", 0)]
    public async Task HandlerDirectPhysicalSendsPreparedInteractionBeforeOpeningReceiveAndHitpointMutation(
        string scenario,
        int targetStatusFlag)
    {
        var result = await RunDirectPhysicalReceiveEnvelopeAsync(scenario, targetStatusFlag);
        var incomingObservations = result.Observations.Where(observation => observation.Incoming).ToArray();
        Assert.Equal(2, incomingObservations.Length);
        foreach (var observation in incomingObservations)
        {
            // MsgAttackPacket.cs:649-659 sends InteractionCreate after Physical.OnPlayer,
            // but before ReceiveAttack.Player's CombatGear, speffect, Ninja, and HP work.
            Assert.False(HasLegacyStatusFlag(
                observation.Sessions[TargetPlayerUid].Character.Attributes,
                StarArrowReceiveEnvelopeSolidFlag));
            Assert.False(HasLegacyStatusFlag(
                observation.Sessions[PlayerUid].Character.Attributes,
                StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag));
            Assert.Equal(100_000, observation.Sessions[TargetPlayerUid].Character.MinHitPoints);
            Assert.Equal(result.ExpectsTail ? 95_000 : 100_000,
                observation.Sessions[PlayerUid].Character.MinHitPoints);
            Assert.Equal(result.ExpectsTail ? "1" : "2",
                observation.Sessions[TargetPlayerUid].Character.Attributes["LegacyRevengeTailChange"]);
        }

        var incomingDeliveryIndices = new List<int>();
        foreach (var frames in new[] { result.CasterSent, result.TargetSent })
        {
            var attack = AssertSinglePhysicalAttack(frames, TargetPlayerUid);
            Assert.Equal(PlayerUid, attack.Query.Uid);
            Assert.Equal(MsgAttackEffect.None, attack.Query.Effect);
            // InteractionCreate preserves the query coordinates; these are not
            // the target's authoritative 832/584 position.
            Assert.Equal((120, 121), ((int)attack.Query.X, (int)attack.Query.Y));
            // Physical's literal 1 is displayed as floor(1 * 22 / 27) = 0.
            // HookMoon later changes receive damage to 10, not displayed damage to 8.
            Assert.Equal(0, attack.Query.Damage);
            var incomingIndex = SingleFrameIndex(frames, frame =>
                MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Physical &&
                packet.Query.OpponentUid == TargetPlayerUid);
            Assert.Equal((ushort)2135, ReadFrame(frames[incomingIndex]).Header.Opcode);
            incomingDeliveryIndices.Add(Array.IndexOf(result.DeliveryOrder, frames[incomingIndex]));
        }

        var solidStatusIndex = SingleFrameIndex(result.TargetSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null && IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeSolidFlag)));
        var sageStatusIndex = SingleFrameIndex(result.CasterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag)));
        var solidArchiveIndex = AssertStarArrowReceiveEnvelopeArchive(
            result.TargetSent, TargetPlayerUid, StarArrowReceiveEnvelopeSolidFlag, duration: 5, damage: 2_400);
        var sageArchiveIndex = AssertStarArrowReceiveEnvelopeArchive(
            result.CasterSent, PlayerUid, StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag, duration: 10, damage: 0);
        var hitpointIndices = AssertDirectPhysicalReceiveEnvelopeHitpoints(result.TargetSent);
        var firstHitpointDelivery = Array.IndexOf(result.DeliveryOrder, result.TargetSent[hitpointIndices[0]]);
        foreach (var hookFrame in new[]
        {
            result.TargetSent[solidStatusIndex],
            result.TargetSent[solidArchiveIndex],
            result.CasterSent[sageStatusIndex],
            result.CasterSent[sageArchiveIndex],
        })
        {
            var hookDelivery = Array.IndexOf(result.DeliveryOrder, hookFrame);
            Assert.All(incomingDeliveryIndices, index => Assert.True(index < hookDelivery));
            Assert.True(hookDelivery < firstHitpointDelivery);
        }

        var openingObservations = result.Observations.Where(observation => !observation.Incoming).ToArray();
        Assert.Equal(2, openingObservations.Length);
        Assert.All(openingObservations, observation =>
            Assert.Equal(100_000, observation.Sessions[TargetPlayerUid].Character.MinHitPoints));
        Assert.Equal(99_990, result.Target.Character.MinHitPoints);
        Assert.Equal(result.ExpectsTail ? 95_000 : 100_000, result.Caster.Character.MinHitPoints);
        Assert.Equal("2400", result.Target.Character.Attributes["Solid"]);
        Assert.True(HasLegacyStatusFlag(result.Target.Character.Attributes, StarArrowReceiveEnvelopeSolidFlag));
        Assert.True(HasLegacyStatusFlag(
            result.Caster.Character.Attributes, StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag));
        Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.BloodthirstElan);
        Assert.Contains("Chance / 100", result.Log);

        Assert.DoesNotContain(ParseSpellAnimations(result.DeliveryOrder),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.DoesNotContain(HeavensWonderRuntimeSpellId,
            (result.Target.MagicCooldowns ?? new Dictionary<ushort, DateTimeOffset>()).Keys);
        if (result.ExpectsTail)
        {
            foreach (var frames in new[] { result.CasterSent, result.TargetSent })
            {
                var tail = Assert.Single(ParseSpellAnimations(frames),
                    animation => animation.SpellId == RevengeTailAttachStatusSpellId);
                Assert.Equal(TargetPlayerUid, tail.AttackerUid);
                Assert.Equal(5_000U, Assert.Single(tail.Targets).Damage);
                var tailIndex = SingleFrameIndex(frames, frame =>
                    LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
                    animation.SpellId == RevengeTailAttachStatusSpellId);
                Assert.True(Array.IndexOf(result.DeliveryOrder, frames[tailIndex]) < incomingDeliveryIndices.Min());
            }

            var counterHitpoints = Enumerable.Range(0, result.CasterSent.Length).Where(index =>
                LegacyMsgUpdatePacket.TryParse(ReadFrame(result.CasterSent[index]), out var update) &&
                update.Uid == PlayerUid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints))
                .ToArray();
            Assert.NotEmpty(counterHitpoints);
            Assert.All(counterHitpoints, index =>
                Assert.True(Array.IndexOf(result.DeliveryOrder, result.CasterSent[index]) < incomingDeliveryIndices.Min()));
        }
        else
        {
            Assert.DoesNotContain(ParseSpellAnimations(result.DeliveryOrder),
                animation => animation.SpellId == RevengeTailAttachStatusSpellId);
            var expectsConsumedPhoenix = scenario == "wild-phoenix";
            Assert.Equal(expectsConsumedPhoenix ? "0" : "1", result.Target.Character.Attributes[
                RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute]);
            Assert.Equal(!expectsConsumedPhoenix,
                HasLegacyStatusFlag(result.Target.Character.Attributes, LegacyWildPhoenixFlag));
            if (expectsConsumedPhoenix)
            {
                var phoenixIndex = AssertStarArrowReceiveEnvelopeArchive(
                    result.TargetSent, TargetPlayerUid, LegacyWildPhoenixFlag, duration: 32, damage: 18_007);
                Assert.True(Array.IndexOf(result.DeliveryOrder, result.TargetSent[phoenixIndex]) < incomingDeliveryIndices.Min());
            }
            else
            {
                Assert.DoesNotContain(ParseMsgUpdates(result.DeliveryOrder).SelectMany(update => update.Updates),
                    entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == LegacyWildPhoenixFlag);
            }
        }
    }

    private static async Task<DirectPhysicalReceiveEnvelopeResult> RunDirectPhysicalReceiveEnvelopeAsync(
        string scenario,
        int targetStatusFlag)
    {
        var expectsTail = scenario == "normal-revenge-tail";
        var worldData = CreateMagicCatalogCore(
            CreateMonsterFamily(), spawns: [], sobNpcs: [],
            magicRows:
            [
                CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, useMana: 0, damage: 5_000, coldTime: 0),
                CreateMagicRow(WildPhoenixSpellId, sort: 6, useMana: 0, damage: 4,
                    coldTime: 0, duration: 32, damageOnHuman: 18, damage2: 7),
                CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 1_000),
            ],
            gouyuImmortals: [CreateStarArrowReceiveEnvelopeGouyuImmortal()]);
        var caster = CreateSession(mapId: 1_000, strength: 301, classCode: 5_005, minHitPoints: 100_000,
            spellIds: [], attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["MaxHitpoints"] = "100000",
                ["StatusFlags"] = BuildStatusFlags(LegacyHookMoonCasterFlag),
                ["HookMoonCaster"] = "true",
                ["HookMoonPower"] = "1000",
                ["LegacyNinjaLevels"] = "17",
                ["LegacyNinjaSageModeRoll"] = "0",
            });
        var targetFlags = new List<int> { RevengeTailStatusFlag };
        if (targetStatusFlag != 0)
        {
            targetFlags.Add(targetStatusFlag);
        }
        if (!expectsTail)
        {
            targetFlags.Add(LegacyWildPhoenixFlag);
        }
        var targetSpells = new List<PlayerSpellSnapshotEntry> { CreatePlayerSpell(RevengeTailAttachStatusSpellId) };
        if (!expectsTail)
        {
            targetSpells.Add(CreatePlayerSpell(WildPhoenixSpellId));
            targetSpells.Add(CreatePlayerSpell(HeavensWonderRuntimeSpellId));
        }
        var target = CreateSession(uid: TargetPlayerUid, name: "DirectPhysicalReceiveTarget", mapId: 1_000,
            x: 832, y: 584, minHitPoints: 100_000, spellEntries: targetSpells,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StatusFlags"] = BuildStatusFlags(targetFlags.ToArray()),
                ["MaxHitpoints"] = "100000",
                ["AjustDefense"] = "1000000",
                ["LegacySpeffectLevel.Solid"] = "4",
                ["LegacySpeffectRoll.Solid"] = "0",
                ["LegacySpeffectLevel.Bloodthirst"] = "4",
                ["LegacySpeffectRoll.Bloodthirst"] = "0",
                ["LegacyRevengeTailChange"] = "2",
                [CounterKill16350BackDamageRollAttribute] = "0",
                [RuntimeLegacyCombatGearWildPhoenixPackedDamageAttribute] = "18007",
                [RuntimeLegacyCombatGearWildPhoenixChargeCountAttribute] = expectsTail ? "0" : "1",
            });
        var sessions = new WorldSessionStore();
        var casterSent = new ConcurrentQueue<byte[]>();
        var targetSent = new ConcurrentQueue<byte[]>();
        var deliveryOrder = new ConcurrentQueue<byte[]>();
        var observations = new ConcurrentQueue<DirectPhysicalReceiveEnvelopeObservation>();
        var casterConnection = CreateDirectPhysicalReceiveEnvelopeConnection(
            PlayerUid, casterSent, deliveryOrder, sessions, observations);
        var targetConnection = CreateDirectPhysicalReceiveEnvelopeConnection(
            TargetPlayerUid, targetSent, deliveryOrder, sessions, observations);
        sessions.Set(casterConnection, caster);
        sessions.Set(targetConnection, target);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(
            casterConnection, TextWriter.Synchronized(log), caster,
            WorldData: worldData, Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: TargetPlayerUid, X: 120, Y: 121,
                AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnection, out var finalTarget));
        Assert.NotNull(finalTarget);
        return new DirectPhysicalReceiveEnvelopeResult(finalCaster, finalTarget,
            casterSent.ToArray(), targetSent.ToArray(), deliveryOrder.ToArray(), observations.ToArray(),
            expectsTail, log.ToString());
    }

    private static ConquerTcpConnection CreateDirectPhysicalReceiveEnvelopeConnection(
        uint recipientUid,
        ConcurrentQueue<byte[]> sent,
        ConcurrentQueue<byte[]> deliveryOrder,
        WorldSessionStore sessions,
        ConcurrentQueue<DirectPhysicalReceiveEnvelopeObservation> observations)
    {
        return new ConquerTcpConnection(localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Enqueue(bytes);
                deliveryOrder.Enqueue(bytes);
                var parsedFrame = ReadFrame(bytes);
                var incoming = MsgAttackPacket.TryParse(parsedFrame, out var attack) &&
                    attack.Query.AttackType == MsgAttackType.Physical && attack.Query.OpponentUid == TargetPlayerUid;
                var openingArchive = LegacyMsgUpdatePacket.TryParse(parsedFrame, out var update) &&
                    update.Uid == recipientUid && update.Updates.Any(entry =>
                        entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                        (entry.Flag == StarArrowReceiveEnvelopeSolidFlag ||
                         entry.Flag == StarArrowReceiveEnvelopeNinjaSageSpeedModeFlag));
                if (incoming || openingArchive)
                {
                    observations.Enqueue(new DirectPhysicalReceiveEnvelopeObservation(incoming,
                        sessions.Snapshot().ToDictionary(active => checked((uint)active.Session.Character.Uid),
                            active => active.Session)));
                }
                return ValueTask.CompletedTask;
            });
    }

    private static int[] AssertDirectPhysicalReceiveEnvelopeHitpoints(byte[][] frames)
    {
        var indices = Enumerable.Range(0, frames.Length).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) && update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints))
            .ToArray();
        Assert.Equal(2, indices.Length);
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[indices[0]]), out var first));
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[indices[1]]), out var second));
        Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], first.Updates.Select(entry => entry.Type));
        Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
            second.Updates.Select(entry => entry.Type));
        Assert.Equal(100_000UL, first.Updates[0].Value);
        Assert.Equal(100_000UL, second.Updates[0].Value);
        Assert.Equal(99_990UL, second.Updates[1].Value);
        return indices;
    }

    private sealed record DirectPhysicalReceiveEnvelopeObservation(
        bool Incoming,
        IReadOnlyDictionary<uint, WorldSession> Sessions);

    private sealed record DirectPhysicalReceiveEnvelopeResult(
        WorldSession Caster,
        WorldSession Target,
        byte[][] CasterSent,
        byte[][] TargetSent,
        byte[][] DeliveryOrder,
        DirectPhysicalReceiveEnvelopeObservation[] Observations,
        bool ExpectsTail,
        string Log);
}
