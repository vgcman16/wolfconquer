using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int PounceGateCounterSolidFlag = 0x164;

    [Fact]
    public async Task HandlerPounceGateCounterCompletesBeforeLaterReceiveAndCarriesRefreshedCaster()
    {
        var result = await RunPounceGateCounterAsync();
        var counter = AssertPounceGateCounterAnimation(result);
        var casterHit = Assert.Single(counter.Targets, target => target.Uid == PlayerUid);
        var laterHit = Assert.Single(counter.Targets, target => target.Uid == result.LaterUid);
        Assert.True(casterHit.Damage > 0);
        Assert.True(laterHit.Damage > 0);
        var incoming = AssertPounceGateCounterIncoming(result);
        var incomingHit = Assert.Single(incoming.Targets);
        Assert.Equal(result.LaterUid, incomingHit.Uid);
        Assert.True(incomingHit.Damage > 0);
        Assert.True(incomingHit.Hit);
        Assert.Equal(MsgAttackEffect.None, incomingHit.Effect);

        Assert.Equal(100_000L - casterHit.Damage, result.Caster.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Caster.Character.Attributes, PounceGateCounterSolidFlag));
        Assert.Equal("2400", result.Caster.Character.Attributes["Solid"]);
        Assert.Equal("50", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal((840L, 584L), (result.Caster.Character.X, result.Caster.Character.Y));
        Assert.Equal(100_000L - laterHit.Damage - incomingHit.Damage, result.Later.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Later.Character.Attributes, PounceGateCounterSolidFlag));
        Assert.Equal("2400", result.Later.Character.Attributes["Solid"]);

        // HellVortex also hits the later Pounce candidate, so its second Solid archive
        // is the Pounce receive hook, not the receive nested inside the counter.
        var solidIndices = PounceGateCounterSolidArchiveIndices(result.LaterSent, result.LaterUid);
        Assert.Equal(2, solidIndices.Length);
        var observations = result.SolidObservations.Where(observed => observed.Uid == result.LaterUid).ToArray();
        Assert.Equal(2, observations.Length);
        var beforePounceHit = observations[1];
        Assert.Equal(100_000L - casterHit.Damage, beforePounceHit.Caster.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(beforePounceHit.Caster.Character.Attributes, PounceGateCounterSolidFlag));
        Assert.Equal("2400", beforePounceHit.Caster.Character.Attributes["Solid"]);
        Assert.Equal(100_000L - laterHit.Damage, beforePounceHit.Target.Character.MinHitPoints);
        var counterIndex = PounceGateCounterAnimationIndex(result.LaterSent, HellVortexSpellId);
        var incomingIndex = PounceGateCounterAnimationIndex(result.LaterSent, PounceRuntimeSpellId);
        var hitpointIndices = AssertPounceGateCounterHitpointPairs(
            result.LaterSent, result.LaterUid,
            100_000U - laterHit.Damage,
            100_000U - laterHit.Damage - incomingHit.Damage);
        Assert.True(hitpointIndices[1] < counterIndex);
        Assert.True(counterIndex < solidIndices[1]);
        Assert.True(solidIndices[1] < hitpointIndices[2]);
        Assert.True(hitpointIndices[3] < incomingIndex);
        var casterHitpoints = AssertPounceGateCounterHitpointPairs(
            result.CasterSent, PlayerUid, 100_000U - casterHit.Damage);
        Assert.True(casterHitpoints[1] < PounceGateCounterAnimationIndex(result.CasterSent, HellVortexSpellId));
        AssertPounceGateCounterBlockedTarget(result);
    }

    [Fact]
    public async Task HandlerPounceGateCounterKillingLaterCandidatePreventsAnotherIncomingReceive()
    {
        var result = await RunPounceGateCounterAsync(laterHitpoints: 1);
        var counter = AssertPounceGateCounterAnimation(result);
        Assert.True(Assert.Single(counter.Targets, target => target.Uid == result.LaterUid).Damage >= 1);
        Assert.Empty(AssertPounceGateCounterIncoming(result).Targets);
        Assert.Equal(0, result.Later.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Later.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Single(PounceGateCounterSolidArchiveIndices(result.LaterSent, result.LaterUid));
        Assert.Single(result.SolidObservations, observed => observed.Uid == result.LaterUid);
        var hitpointIndices = AssertExistingNormalDeathCombatPhases(result.LaterSent, result.LaterUid,
            (100_000, 1), (100_000, 1), (100_000, 1), (100_000, 0));
        Assert.All(hitpointIndices, index =>
            Assert.True(index < PounceGateCounterAnimationIndex(result.LaterSent, HellVortexSpellId)));
        var casterHit = Assert.Single(counter.Targets, target => target.Uid == PlayerUid);
        Assert.Equal(100_000L - casterHit.Damage, result.Caster.Character.MinHitPoints);
        AssertPounceGateCounterBlockedTarget(result);
    }

    [Theory]
    [InlineData("wrong-instance")]
    [InlineData("dead-hp")]
    [InlineData("dead-flag")]
    [InlineData("ghost-flag")]
    public async Task HandlerPounceFencedCounterCandidateDoesNotRetaliateOrReceive(string fence)
    {
        var result = await RunPounceGateCounterAsync(fence: fence);
        Assert.DoesNotContain(ParseSpellAnimations(result.CasterSent), animation => animation.SpellId == HellVortexSpellId);
        var incoming = AssertPounceGateCounterIncoming(result);
        var incomingHit = Assert.Single(incoming.Targets);
        Assert.Equal(result.LaterUid, incomingHit.Uid);
        Assert.True(incomingHit.Damage > 0);
        Assert.Equal(100_000, result.Caster.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(result.Caster.Character.Attributes, PounceGateCounterSolidFlag));
        Assert.Equal(100_000L - incomingHit.Damage, result.Later.Character.MinHitPoints);
        Assert.Equal(result.OriginalCounter.Character.MinHitPoints, result.Counter.Character.MinHitPoints);
        Assert.Equal(500, result.Counter.Character.MinMana);
        Assert.Equal("100", result.Counter.Character.Attributes["Stamina"]);
        Assert.Null(result.Counter.MagicCooldowns);
        Assert.False(HasLegacyStatusFlag(result.Counter.Character.Attributes, PounceGateCounterSolidFlag));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CounterSent),
            update => update.Uid == result.CounterUid && update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.ArchiveSkill or LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.Single(PounceGateCounterSolidArchiveIndices(result.LaterSent, result.LaterUid));
        AssertPounceGateCounterHitpointPairs(result.LaterSent, result.LaterUid, 100_000U - incomingHit.Damage);
        Assert.DoesNotContain("WorldServer sent legacy player HellVortex counter", result.Log);
    }

    private static async Task<PounceGateCounterResult> RunPounceGateCounterAsync(
        string? fence = null,
        long laterHitpoints = 100_000)
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(), [], CreatePounceRuntimeRow(),
            CreateMagicRow(
                HellVortexSpellId, sort: 98, useMana: 7, damage: 200,
                useStamina: 5, coldTime: 1_000, damage3: 30_030));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new[] { new List<byte[]>(), new List<byte[]>() };
        var observations = new List<PounceGateCounterSolidObservation>();
        var casterConnection = CreatePounceGateCounterConnection(PlayerUid, casterSent, sessions, observations);
        var targetConnections = targetSent.Select((sent, index) => CreatePounceGateCounterConnection(
            checked(TargetPlayerUid + (uint)index), sent, sessions, observations)).ToArray();
        var caster = CreateSession(
            minHitPoints: 100_000,
            strength: 1_000,
            stamina: 100,
            spellIds: [PounceRuntimeSpellId],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["DynamicID"] = "71",
                ["MaxHitpoints"] = "100000",
                ["LegacySpeffectLevel.Solid"] = "4",
                ["LegacySpeffectRoll.Solid"] = "0",
            },
            playerItems: CreatePounceEquipment(mainShield: true, alternateShield: false));
        var targets = Enumerable.Range(0, 2).Select(index => CreateSession(
            uid: TargetPlayerUid + index,
            name: "PounceGateCandidate" + index,
            x: 842 + index,
            y: 584,
            minHitPoints: 100_000,
            strength: 300,
            stamina: 100,
            spellIds: [],
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PkMode"] = "0",
                ["DynamicID"] = "71",
                ["MaxHitpoints"] = "100000",
                ["LegacySpeffectLevel.Solid"] = "4",
                ["LegacySpeffectRoll.Solid"] = "0",
            })).ToArray();
        sessions.Set(casterConnection, caster);
        for (var index = 0; index < targets.Length; index++)
        {
            sessions.Set(targetConnections[index], targets[index]);
        }

        // Assign the counter after observing the store's actual enumeration order.
        var targetOrder = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid);
        Assert.Equal(2, targetOrder.Count);
        var counterIndex = Array.FindIndex(targets, target => target.Character.Uid == targetOrder[0]);
        var laterIndex = Array.FindIndex(targets, target => target.Character.Uid == targetOrder[1]);
        Assert.True(counterIndex >= 0);
        Assert.True(laterIndex >= 0);
        var counterAttributes = new Dictionary<string, string>(targets[counterIndex].Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            [RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1",
            [RuntimeLegacyHellVortexRollAttribute] = "0",
        };
        if (fence == "wrong-instance")
        {
            counterAttributes["DynamicID"] = "72";
        }
        if (fence is "dead-flag" or "ghost-flag")
        {
            counterAttributes["StatusFlags"] = BuildStatusFlags(fence == "dead-flag"
                ? LegacyPlayerDeathState.DeadStatusFlag : LegacyPlayerDeathState.GhostStatusFlag);
        }
        var counter = targets[counterIndex] with
        {
            Character = targets[counterIndex].Character with
            {
                MinHitPoints = fence == "dead-hp" ? 0 : 100_000,
                Attributes = counterAttributes,
            },
            PlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(HellVortexSpellId)]),
        };
        var later = targets[laterIndex] with
        {
            Character = targets[laterIndex].Character with { MinHitPoints = laterHitpoints },
        };
        sessions.Set(targetConnections[counterIndex], counter);
        sessions.Set(targetConnections[laterIndex], later);
        Assert.Equal(targetOrder, GetVisibleTargetOrder(sessions, casterConnection, PlayerUid));
        using var log = new StringWriter();
        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                casterConnection, log, caster, WorldData: worldData, Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgAttackPacket(CreatePounceRuntimeQuery(840, 584)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnections[counterIndex], out var finalCounter));
        Assert.NotNull(finalCounter);
        Assert.True(sessions.TryGet(targetConnections[laterIndex], out var finalLater));
        Assert.NotNull(finalLater);
        return new PounceGateCounterResult(
            finalCaster, finalCounter, finalLater, counter,
            checked((uint)counter.Character.Uid), checked((uint)later.Character.Uid),
            casterSent, targetSent[counterIndex], targetSent[laterIndex], observations, log.ToString());
    }

    private static ConquerTcpConnection CreatePounceGateCounterConnection(
        uint recipientUid,
        List<byte[]> sent,
        WorldSessionStore sessions,
        List<PounceGateCounterSolidObservation> observations)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Add(bytes);
                if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == recipientUid &&
                    update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                        entry.Flag == PounceGateCounterSolidFlag))
                {
                    var snapshot = sessions.Snapshot();
                    observations.Add(new PounceGateCounterSolidObservation(
                        recipientUid,
                        Assert.Single(snapshot, active => active.Session.Character.Uid == PlayerUid).Session,
                        Assert.Single(snapshot, active => active.Session.Character.Uid == recipientUid).Session));
                }
                return ValueTask.CompletedTask;
            });
    }

    private static LegacySpellAnimationPacket AssertPounceGateCounterAnimation(PounceGateCounterResult result)
    {
        var counter = Assert.Single(ParseSpellAnimations(result.CasterSent), animation => animation.SpellId == HellVortexSpellId);
        Assert.Equal(result.CounterUid, counter.AttackerUid);
        Assert.Equal(0U, counter.AttackedUid);
        Assert.Equal((0, 0), (counter.X, counter.Y));
        Assert.Equal(0, counter.BombFlag);
        Assert.Equal(2, counter.Targets.Count);
        Assert.DoesNotContain(counter.Targets, target => target.Uid == result.CounterUid);
        foreach (var frames in new[] { result.CasterSent, result.CounterSent, result.LaterSent })
        {
            var counterIndex = PounceGateCounterAnimationIndex(frames, HellVortexSpellId);
            var incomingIndex = PounceGateCounterAnimationIndex(frames, PounceRuntimeSpellId);
            Assert.Equal((ushort)2239, ReadFrame(frames[counterIndex]).Header.Opcode);
            Assert.True(counterIndex < incomingIndex);
        }
        return counter;
    }

    private static LegacySpellAnimationPacket AssertPounceGateCounterIncoming(PounceGateCounterResult result)
    {
        var incoming = Assert.Single(ParseSpellAnimations(result.CasterSent), animation => animation.SpellId == PounceRuntimeSpellId);
        AssertPounceSourceAnimation(incoming, 840, 584, expectedSoul: 0);
        Assert.DoesNotContain(incoming.Targets, target => target.Uid == result.CounterUid);
        var recipients = result.Counter.Character.Attributes["DynamicID"] == "71"
            ? new[] { result.CasterSent, result.CounterSent, result.LaterSent }
            : new[] { result.CasterSent, result.LaterSent };
        foreach (var frames in recipients)
        {
            var index = PounceGateCounterAnimationIndex(frames, PounceRuntimeSpellId);
            Assert.Equal((ushort)2239, ReadFrame(frames[index]).Header.Opcode);
            var recipientAnimation = Assert.Single(ParseSpellAnimations(frames), animation => animation.SpellId == PounceRuntimeSpellId);
            Assert.Equal(incoming.Targets, recipientAnimation.Targets);
        }
        return incoming;
    }

    private static void AssertPounceGateCounterBlockedTarget(PounceGateCounterResult result)
    {
        Assert.Equal(100_000, result.Counter.Character.MinHitPoints);
        Assert.Equal(493, result.Counter.Character.MinMana);
        Assert.Equal("95", result.Counter.Character.Attributes["Stamina"]);
        Assert.True(result.Counter.MagicCooldowns?.ContainsKey(HellVortexSpellId) == true);
        Assert.False(HasLegacyStatusFlag(result.Counter.Character.Attributes, PounceGateCounterSolidFlag));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CounterSent),
            update => update.Uid == result.CounterUid && update.Updates.Any(entry => entry.Type is
                LegacyMsgUpdateDataType.ArchiveSkill or LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        Assert.Contains("WorldServer sent legacy player HellVortex counter", result.Log);
    }

    private static int PounceGateCounterAnimationIndex(IReadOnlyList<byte[]> frames, ushort spellId)
    {
        return SingleFrameIndex(frames, frame => LegacySpellAnimationPacket.TryParse(frame, out var animation) &&
            animation.SpellId == spellId);
    }

    private static int[] PounceGateCounterSolidArchiveIndices(IReadOnlyList<byte[]> frames, uint uid)
    {
        var indices = Enumerable.Range(0, frames.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) && update.Uid == uid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == PounceGateCounterSolidFlag && entry.Time == 5 && entry.Damage == 2_400)).ToArray();
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
        return indices;
    }

    private static int[] AssertPounceGateCounterHitpointPairs(
        IReadOnlyList<byte[]> frames,
        uint uid,
        params uint[] remainingHitpoints)
    {
        var indices = Enumerable.Range(0, frames.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) && update.Uid == uid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
        Assert.Equal(remainingHitpoints.Length * 2, indices.Length);
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
        for (var pair = 0; pair < remainingHitpoints.Length; pair++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[indices[pair * 2]]), out var maximum));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[indices[pair * 2 + 1]]), out var current));
            Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], maximum.Updates.Select(entry => entry.Type));
            Assert.Equal(
                [LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
                current.Updates.Select(entry => entry.Type));
            Assert.Equal(100_000UL, maximum.Updates[0].Value);
            Assert.Equal(100_000UL, current.Updates[0].Value);
            Assert.Equal(remainingHitpoints[pair], current.Updates[1].Value);
        }
        return indices;
    }

    private sealed record PounceGateCounterSolidObservation(uint Uid, WorldSession Caster, WorldSession Target);

    private sealed record PounceGateCounterResult(
        WorldSession Caster,
        WorldSession Counter,
        WorldSession Later,
        WorldSession OriginalCounter,
        uint CounterUid,
        uint LaterUid,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> CounterSent,
        IReadOnlyList<byte[]> LaterSent,
        IReadOnlyList<PounceGateCounterSolidObservation> SolidObservations,
        string Log);
}
