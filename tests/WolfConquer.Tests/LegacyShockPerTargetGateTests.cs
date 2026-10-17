using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ShockGateSolidFlag = 0x164;

    [Fact]
    public async Task HandlerShockGateCounterFinishesBeforeLaterHooksAndCarriesRefreshedCaster()
    {
        var result = await RunShockPerTargetGateAsync();
        var hell = AssertShockGateCounter(result);
        var casterHit = Assert.Single(hell.Targets, hit => hit.Uid == PlayerUid);
        var laterCounterHit = Assert.Single(hell.Targets, hit => hit.Uid == result.LaterUid);
        var incoming = AssertShockGateIncoming(result);
        var laterIncomingHit = Assert.Single(incoming.Targets);
        Assert.Equal(result.LaterUid, laterIncomingHit.Uid);
        Assert.True(laterIncomingHit.Damage > 0);
        Assert.Equal(100_000L - casterHit.Damage, result.Caster.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Caster.Character.Attributes, ShockGateSolidFlag));
        Assert.Equal("2400", result.Caster.Character.Attributes["Solid"]);
        Assert.Equal(493, result.Caster.Character.MinMana);
        Assert.Equal("95", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal(100_000L - laterCounterHit.Damage - laterIncomingHit.Damage, result.Later.Character.MinHitPoints);

        var observations = result.Observations.Where(observation => observation.Uid == result.LaterUid).ToArray();
        Assert.Equal(2, observations.Length);
        Assert.Equal(100_000L - casterHit.Damage, observations[1].Caster.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(observations[1].Caster.Character.Attributes, ShockGateSolidFlag));
        Assert.Equal(100_000L - laterCounterHit.Damage, observations[1].Target.Character.MinHitPoints);
        var solid = ShockGateSolidIndices(result.LaterSent, result.LaterUid);
        Assert.Equal(2, solid.Length);
        var hp = AssertShockGateHpPairs(result.LaterSent, result.LaterUid,
            100_000U - laterCounterHit.Damage,
            100_000U - laterCounterHit.Damage - laterIncomingHit.Damage);
        var counterIndex = ShockGateAnimationIndex(result.LaterSent, HellVortexSpellId);
        Assert.True(solid[0] < hp[0] && hp[1] < counterIndex);
        Assert.True(counterIndex < solid[1] && solid[1] < hp[2]);
        Assert.True(hp[3] < ShockGateAnimationIndex(result.LaterSent, CrackingShockSpellId));
        AssertShockGateHpPairs(result.CasterSent, PlayerUid, 100_000U - casterHit.Damage);
        AssertShockGateBlockedCounter(result, result.First, result.FirstUid, result.FirstSent);
    }

    [Fact]
    public async Task HandlerShockGateCounterKillingLaterTargetPreventsStaleReceiveAndSecondDeath()
    {
        var result = await RunShockPerTargetGateAsync("kill-later");
        var hell = AssertShockGateCounter(result);
        Assert.True(Assert.Single(hell.Targets, hit => hit.Uid == result.LaterUid).Damage > 1);
        Assert.Empty(AssertShockGateIncoming(result).Targets);
        Assert.Equal(0, result.Later.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Later.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.Single(ShockGateSolidIndices(result.LaterSent, result.LaterUid));
        Assert.Single(result.Observations, observation => observation.Uid == result.LaterUid);
        var deathHp = AssertExistingNormalDeathCombatPhases(result.LaterSent, result.LaterUid,
            (100_000, 1), (100_000, 1), (100_000, 1), (100_000, 0));
        Assert.All(deathHp, index =>
            Assert.True(index < ShockGateAnimationIndex(result.LaterSent, HellVortexSpellId)));
        Assert.Single(result.LaterSent, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Death && attack.Query.OpponentUid == result.LaterUid);
        AssertShockGateBlockedCounter(result, result.First, result.FirstUid, result.FirstSent);
    }

    [Theory]
    [InlineData("move-out", false)]
    [InlineData("move-in", true)]
    public async Task HandlerShockUsesRefreshedQueryRadiusAfterCounterBeforeLaterHooks(string mode, bool receivesShock)
    {
        var result = await RunShockPerTargetGateAsync(mode);
        Assert.True(result.Mutated);
        var hell = AssertShockGateCounter(result);
        var counterHit = Assert.Single(hell.Targets, hit => hit.Uid == result.LaterUid);
        var incoming = AssertShockGateIncoming(result);
        Assert.Equal(receivesShock ? 843L : 844L, result.Later.Character.X);
        Assert.Equal(584, result.Later.Character.Y);
        var solid = ShockGateSolidIndices(result.LaterSent, result.LaterUid);
        Assert.Equal(receivesShock ? 2 : 1, solid.Length);
        if (receivesShock)
        {
            // Query (837,584), not caster (840,584): the refreshed target is exactly six away.
            var hit = Assert.Single(incoming.Targets);
            Assert.Equal(result.LaterUid, hit.Uid);
            Assert.Equal(100_000L - counterHit.Damage - hit.Damage, result.Later.Character.MinHitPoints);
            Assert.True(ShockGateAnimationIndex(result.LaterSent, HellVortexSpellId) < solid[1]);
            AssertShockGateHpPairs(result.LaterSent, result.LaterUid,
                100_000U - counterHit.Damage, 100_000U - counterHit.Damage - hit.Damage);
        }
        else
        {
            Assert.Empty(incoming.Targets);
            Assert.Equal(100_000L - counterHit.Damage, result.Later.Character.MinHitPoints);
            AssertShockGateHpPairs(result.LaterSent, result.LaterUid, 100_000U - counterHit.Damage);
        }
        AssertShockGateBlockedCounter(result, result.First, result.FirstUid, result.FirstSent);
    }

    [Fact]
    public async Task HandlerShockEvaluatesLaterGateAfterEarlierReceiveRatherThanCandidateSelection()
    {
        var result = await RunShockPerTargetGateAsync("late-counter");
        Assert.True(result.Mutated);
        var incomingHit = Assert.Single(AssertShockGateIncoming(result).Targets);
        Assert.Equal(result.FirstUid, incomingHit.Uid);
        var hell = AssertShockGateCounter(result, counterUid: result.LaterUid);
        var firstCounterHit = Assert.Single(hell.Targets, hit => hit.Uid == result.FirstUid);
        var casterHit = Assert.Single(hell.Targets, hit => hit.Uid == PlayerUid);
        Assert.Equal(100_000L - incomingHit.Damage - firstCounterHit.Damage, result.First.Character.MinHitPoints);
        Assert.Equal(100_000L - casterHit.Damage, result.Caster.Character.MinHitPoints);
        var solid = ShockGateSolidIndices(result.FirstSent, result.FirstUid);
        Assert.Equal(2, solid.Length);
        var hp = AssertShockGateHpPairs(result.FirstSent, result.FirstUid,
            100_000U - incomingHit.Damage, 100_000U - incomingHit.Damage - firstCounterHit.Damage);
        var counterIndex = ShockGateAnimationIndex(result.FirstSent, HellVortexSpellId);
        Assert.True(solid[0] < hp[0] && hp[1] < solid[1]);
        Assert.True(solid[1] < hp[2] && hp[3] < counterIndex);
        AssertShockGateBlockedCounter(result, result.Later, result.LaterUid, result.LaterSent);
    }

    [Fact]
    public async Task HandlerShockFreshImmortalForceGatePublishesForbiddenPirateWithoutLaterReceive()
    {
        var result = await RunShockPerTargetGateAsync("late-immortal");
        Assert.True(result.Mutated);
        Assert.DoesNotContain(ParseSpellAnimations(result.CasterSent), animation => animation.SpellId == HellVortexSpellId);
        Assert.Equal(result.FirstUid, Assert.Single(AssertShockGateIncoming(result).Targets).Uid);
        Assert.Equal(100_000, result.Later.Character.MinHitPoints);
        Assert.Empty(ShockGateSolidIndices(result.LaterSent, result.LaterUid));
        Assert.DoesNotContain(ParseMsgUpdates(result.LaterSent), update => update.Uid == result.LaterUid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints));
        var forbiddenIndex = SingleFrameIndex(result.CasterSent, frame =>
            LegacyMsgUpdatePacket.TryParse(frame, out var update) && update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ForbiddenPirate && entry.Value == 1));
        Assert.Equal((ushort)2176, ReadFrame(result.CasterSent[forbiddenIndex]).Header.Opcode);
        Assert.True(forbiddenIndex < ShockGateAnimationIndex(result.CasterSent, CrackingShockSpellId));
    }

    private static async Task<ShockPerTargetGateResult> RunShockPerTargetGateAsync(string mode = "counter")
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateCrackingShockDebtRow(level: 0, experience: 0, damageOnMonster: 30_095),
            CreateMagicRow(HellVortexSpellId, sort: 98, useMana: 7, damage: 200,
                useStamina: 5, coldTime: 1_000, damage3: 30_030));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new[] { new List<byte[]>(), new List<byte[]>() };
        var observations = new List<ShockGateObservation>();
        var casterConnection = CreateSendingConnection(casterSent);
        ConquerTcpConnection? firstConnection = null;
        ConquerTcpConnection? laterConnection = null;
        uint firstUid = 0;
        uint laterUid = 0;
        var mutated = false;

        void Observe(uint recipientUid, byte[] bytes)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == recipientUid &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == ShockGateSolidFlag))
            {
                observations.Add(new ShockGateObservation(recipientUid,
                    Assert.Single(sessions.Snapshot(), active => active.Session.Character.Uid == PlayerUid).Session,
                    Assert.Single(sessions.Snapshot(), active => active.Session.Character.Uid == recipientUid).Session));
                if (!mutated && recipientUid == firstUid && mode is ("late-counter" or "late-immortal"))
                {
                    Assert.NotNull(laterConnection);
                    Assert.True(sessions.TryGet(laterConnection, out var later));
                    Assert.NotNull(later);
                    var attributes = new Dictionary<string, string>(later.Character.Attributes);
                    if (mode == "late-counter")
                    {
                        attributes[RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1";
                        attributes[RuntimeLegacyHellVortexRollAttribute] = "0";
                    }
                    else
                    {
                        attributes["StatusFlags"] = BuildStatusFlags(LegacyImmortalForceFlag);
                    }
                    sessions.Set(laterConnection, later with
                    {
                        Character = later.Character with { Attributes = attributes },
                        PlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(HellVortexSpellId)]),
                    });
                    mutated = true;
                }
            }
            if (!mutated && mode is ("move-out" or "move-in") &&
                LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) && animation.SpellId == HellVortexSpellId)
            {
                Assert.NotNull(laterConnection);
                Assert.True(sessions.TryGet(laterConnection, out var later));
                Assert.NotNull(later);
                var attributes = new Dictionary<string, string>(later.Character.Attributes);
                if (mode == "move-out")
                {
                    // Geometry must short-circuit even a newly armed counter outside the query radius.
                    attributes[RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1";
                    attributes[RuntimeLegacyHellVortexRollAttribute] = "0";
                }
                sessions.Set(laterConnection, later with
                {
                    Character = later.Character with { X = mode == "move-out" ? 844 : 843, Attributes = attributes },
                    PlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(HellVortexSpellId)]),
                });
                mutated = true;
            }
        }

        var connections = targetSent.Select((sent, index) => new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                sent.Add(bytes);
                Observe(checked(TargetPlayerUid + (uint)index), bytes);
                return ValueTask.CompletedTask;
            })).ToArray();
        static Dictionary<string, string> Attributes() => new()
        {
            ["PkMode"] = "0",
            ["DynamicID"] = "71",
            ["MaxHitpoints"] = "100000",
            ["LegacySpeffectLevel.Solid"] = "4",
            ["LegacySpeffectRoll.Solid"] = "0",
        };
        var caster = CreateSession(x: 840, minHitPoints: 100_000, strength: 300, spirit: 0, stamina: 100,
            spellIds: [CrackingShockSpellId], classCode: 9_001, attributes: Attributes());
        sessions.Set(casterConnection, caster);
        for (var index = 0; index < connections.Length; index++)
        {
            sessions.Set(connections[index], CreateSession(uid: TargetPlayerUid + index, name: "ShockGate" + index,
                x: 842 + index, minHitPoints: 100_000, strength: 1_000, spirit: 0, stamina: 100,
                spellIds: [], attributes: Attributes()));
        }
        // Assign roles from the store's actual iteration order, not connection insertion order.
        var order = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid);
        Assert.Equal(2, order.Count);
        firstUid = order[0];
        laterUid = order[1];
        var firstIndex = checked((int)(firstUid - TargetPlayerUid));
        var laterIndex = checked((int)(laterUid - TargetPlayerUid));
        firstConnection = connections[firstIndex];
        laterConnection = connections[laterIndex];
        Assert.True(sessions.TryGet(firstConnection, out var first));
        Assert.NotNull(first);
        Assert.True(sessions.TryGet(laterConnection, out var laterTarget));
        Assert.NotNull(laterTarget);
        var firstAttributes = new Dictionary<string, string>(first.Character.Attributes);
        if (mode is not ("late-counter" or "late-immortal"))
        {
            firstAttributes[RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1";
            firstAttributes[RuntimeLegacyHellVortexRollAttribute] = "0";
        }
        sessions.Set(firstConnection, first with
        {
            Character = first.Character with { X = 842, Attributes = firstAttributes },
            PlayerSpells = new PlayerSpellSnapshot([CreatePlayerSpell(HellVortexSpellId)]),
        });
        sessions.Set(laterConnection, laterTarget with
        {
            Character = laterTarget.Character with
            {
                X = mode == "move-in" ? 844 : 843,
                MinHitPoints = mode == "kill-later" ? 1 : 100_000,
            },
        });
        Assert.Equal(order, GetVisibleTargetOrder(sessions, casterConnection, PlayerUid));
        using var log = new StringWriter();
        await PacketHandlers.HandleAsync(new WorldPacketHandlerContext(
                casterConnection, log, caster, WorldData: worldData, Sessions: sessions,
                MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: firstUid, X: 837, Y: 584, AttackType: MsgAttackType.Magic,
                Damage: 0, KilledMonster: 0, ResponseDamage: 0, Effect: MsgAttackEffect.None,
                Unknown: 0, SpellId: CrackingShockSpellId)).ToFrameBytes()));
        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(firstConnection, out var finalFirst));
        Assert.NotNull(finalFirst);
        Assert.True(sessions.TryGet(laterConnection, out var finalLater));
        Assert.NotNull(finalLater);
        return new(finalCaster, finalFirst, finalLater, firstUid, laterUid,
            casterSent, targetSent[firstIndex], targetSent[laterIndex], observations, mutated);
    }

    private static LegacySpellAnimationPacket AssertShockGateIncoming(ShockPerTargetGateResult result)
    {
        var incoming = Assert.Single(ParseSpellAnimations(result.CasterSent), animation => animation.SpellId == CrackingShockSpellId);
        Assert.Equal(PlayerUid, incoming.AttackerUid);
        Assert.Equal(0U, incoming.AttackedUid);
        Assert.Equal((837, 584), (incoming.X, incoming.Y));
        Assert.Equal(0, incoming.LevelHu);
        Assert.Equal(0, incoming.BombFlag);
        Assert.All(incoming.Targets, hit =>
        {
            Assert.True(hit.Hit);
            Assert.Equal(MsgAttackEffect.None, hit.Effect);
        });
        foreach (var frames in new[] { result.CasterSent, result.FirstSent, result.LaterSent })
        {
            ShockGateAnimationIndex(frames, CrackingShockSpellId);
            var received = Assert.Single(ParseSpellAnimations(frames), animation => animation.SpellId == CrackingShockSpellId);
            Assert.Equal(incoming.Targets, received.Targets);
        }
        return incoming;
    }

    private static LegacySpellAnimationPacket AssertShockGateCounter(ShockPerTargetGateResult result, uint? counterUid = null)
    {
        var hell = Assert.Single(ParseSpellAnimations(result.CasterSent), animation => animation.SpellId == HellVortexSpellId);
        Assert.Equal(counterUid ?? result.FirstUid, hell.AttackerUid);
        Assert.Equal((0, 0), (hell.X, hell.Y));
        Assert.Equal(0U, hell.AttackedUid);
        Assert.Equal(0, hell.BombFlag);
        Assert.Equal(2, hell.Targets.Count);
        Assert.All(hell.Targets, hit => Assert.True(hit.Hit && hit.Damage > 0));
        foreach (var frames in new[] { result.CasterSent, result.FirstSent, result.LaterSent })
        {
            Assert.True(ShockGateAnimationIndex(frames, HellVortexSpellId) <
                ShockGateAnimationIndex(frames, CrackingShockSpellId));
        }
        return hell;
    }

    private static void AssertShockGateBlockedCounter(
        ShockPerTargetGateResult result, WorldSession counter, uint counterUid, IReadOnlyList<byte[]> frames)
    {
        Assert.Equal(100_000, counter.Character.MinHitPoints);
        Assert.Equal(493, counter.Character.MinMana);
        Assert.Equal("95", counter.Character.Attributes["Stamina"]);
        Assert.True(counter.MagicCooldowns?.ContainsKey(HellVortexSpellId) == true);
        Assert.DoesNotContain(AssertShockGateIncoming(result).Targets, hit => hit.Uid == counterUid);
        Assert.Empty(ShockGateSolidIndices(frames, counterUid));
        Assert.DoesNotContain(ParseMsgUpdates(frames), update => update.Uid == counterUid && update.Updates.Any(entry =>
            entry.Type is LegacyMsgUpdateDataType.Hitpoints or LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.ArchiveSkill));
    }

    private static int ShockGateAnimationIndex(IReadOnlyList<byte[]> frames, ushort spellId)
    {
        var index = SingleFrameIndex(frames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var animation) && animation.SpellId == spellId);
        Assert.Equal((ushort)2239, ReadFrame(frames[index]).Header.Opcode);
        return index;
    }

    private static int[] ShockGateSolidIndices(IReadOnlyList<byte[]> frames, uint uid)
    {
        var indices = Enumerable.Range(0, frames.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) && update.Uid == uid &&
            update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == ShockGateSolidFlag && entry.Damage == 2_400 && entry.Time == 5)).ToArray();
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
        return indices;
    }

    private static int[] AssertShockGateHpPairs(IReadOnlyList<byte[]> frames, uint uid, params uint[] remaining)
    {
        var indices = Enumerable.Range(0, frames.Count).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var update) && update.Uid == uid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        Assert.Equal(remaining.Length * 2, indices.Length);
        Assert.All(indices, index => Assert.Equal((ushort)2176, ReadFrame(frames[index]).Header.Opcode));
        for (var pair = 0; pair < remaining.Length; pair++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[indices[pair * 2]]), out var maximum));
            Assert.Equal([new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000)], maximum.Updates);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[indices[pair * 2 + 1]]), out var current));
            Assert.Equal(
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, 100_000),
                 new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, remaining[pair])], current.Updates);
        }
        return indices;
    }

    private sealed record ShockGateObservation(uint Uid, WorldSession Caster, WorldSession Target);

    private sealed record ShockPerTargetGateResult(
        WorldSession Caster, WorldSession First, WorldSession Later, uint FirstUid, uint LaterUid,
        IReadOnlyList<byte[]> CasterSent, IReadOnlyList<byte[]> FirstSent, IReadOnlyList<byte[]> LaterSent,
        IReadOnlyList<ShockGateObservation> Observations, bool Mutated);
}
