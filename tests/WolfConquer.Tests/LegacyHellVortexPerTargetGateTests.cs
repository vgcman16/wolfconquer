using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int HellVortexPerTargetGateSolidFlag = 0x164;

    [Fact]
    public async Task HandlerHellVortexRechecksLaterPlayerKilledByEarlierHeavensWonderBeforeReceiveHooks()
    {
        var result = await RunHellVortexOrderedRetaliationAsync(killLaterTarget: true);
        var wonder = Assert.Single(ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == HeavensWonderRuntimeSpellId);
        Assert.Equal(result.FirstUid, wonder.AttackerUid);
        var lethalHit = Assert.Single(wonder.Targets, target => target.Uid == result.LaterUid);
        Assert.True(lethalHit.Damage >= 1);
        Assert.Equal(0, result.Later.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.Later.Character.Attributes, LegacyPlayerDeathState.DeadStatusFlag));
        Assert.False(HasLegacyStatusFlag(result.Later.Character.Attributes, HellVortexPerTargetGateSolidFlag));
        Assert.False(result.Later.Character.Attributes.ContainsKey("Solid"));
        Assert.DoesNotContain(result.LaterSent, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
            update.Uid == result.LaterUid && update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.BloodthirstElan ||
                (entry.Type == LegacyMsgUpdateDataType.ArchiveSkill && entry.Flag == HellVortexPerTargetGateSolidFlag)));

        var incoming = Assert.Single(ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == HellVortexSpellId);
        AssertHellVortexRetaliationAnimation(incoming, expectedSoul: 0);
        var hit = Assert.Single(incoming.Targets);
        Assert.Equal(result.FirstUid, hit.Uid);
        Assert.Equal(result.FirstDamage, hit.Damage);
        Assert.DoesNotContain(incoming.Targets, target => target.Uid == result.LaterUid);
        var hpIndices = HellVortexBackDamageHitpointIndices(result.LaterSent, result.LaterUid);
        Assert.Equal(4, hpIndices.Length); // Three source ClearFlags-query setters/pairs, then final HP0.
        var hpIndex = hpIndices[^1];
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.LaterSent[hpIndex]), out var hp));
        Assert.Equal(0UL, Assert.Single(hp.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        var hpFrames = result.LaterSent.Where(bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == result.LaterUid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
        Assert.Equal(8, hpFrames.Length);
        Assert.All(hpFrames, bytes => Assert.Equal((ushort)2176, ReadFrame(bytes).Header.Opcode));
        for (var phase = 0; phase < 4; phase++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(hpFrames[phase * 2]), out var maximum));
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(hpFrames[phase * 2 + 1]), out var pair));
            Assert.Equal([LegacyMsgUpdateDataType.MaxHitpoints], maximum.Updates.Select(entry => entry.Type));
            Assert.Equal(
                [LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints],
                pair.Updates.Select(entry => entry.Type));
            Assert.Equal(100_000UL, maximum.Updates[0].Value);
            Assert.Equal(100_000UL, pair.Updates[0].Value);
            Assert.Equal(phase < 3 ? 1UL : 0UL, pair.Updates[1].Value);
        }
        var queryIndex = SingleFrameIndex(result.LaterSent, frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        var statusIndex = SingleFrameIndex(result.LaterSent, frame =>
            LegacyPlayerStatusPacket.TryParse(frame, out var status) && status.Uid == result.LaterUid);
        Assert.True(queryIndex < hpIndices[0]);
        Assert.True(hpIndices[0] < statusIndex && statusIndex < hpIndices[1]);
        Assert.All(hpIndices, index =>
            Assert.True(index < HellVortexBackDamageAnimationIndex(result.LaterSent, HeavensWonderRuntimeSpellId)));
        Assert.True(hpIndex < HellVortexBackDamageAnimationIndex(result.LaterSent, HeavensWonderRuntimeSpellId));
        Assert.Single(result.LaterSent, bytes =>
            MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Death &&
            attack.Query.Uid == result.FirstUid && attack.Query.OpponentUid == result.LaterUid);
        Assert.True(result.First.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
        Assert.Equal(493, result.First.Character.MinMana);
        Assert.Equal("95", result.First.Character.Attributes["Stamina"]);
        var casterHit = Assert.Single(wonder.Targets, target => target.Uid == PlayerUid);
        Assert.Equal(100_000L - casterHit.Damage, result.Caster.Character.MinHitPoints);
    }

    [Fact]
    public async Task HandlerHellVortexCarriesRetaliatedAttackerIntoLaterTargetAndPublishesEachIncomingHpImmediately()
    {
        var result = await RunHellVortexOrderedRetaliationAsync();
        var revenge = ParseSpellAnimations(result.CasterSent)
            .Where(animation => animation.SpellId == RevengeTailAttachStatusSpellId).ToArray();
        Assert.Equal([result.FirstUid, result.LaterUid], revenge.Select(animation => animation.AttackerUid));
        Assert.All(revenge, animation =>
        {
            Assert.Equal(1, animation.BombFlag);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(PlayerUid, hit.Uid);
            Assert.Equal(HellVortexBackDamageRevengeDamage, hit.Damage);
        });
        Assert.Equal(100_000L - 2 * HellVortexBackDamageRevengeDamage, result.Caster.Character.MinHitPoints);
        Assert.Equal(493, result.Caster.Character.MinMana);
        Assert.Equal("89", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal((840L, 584L), (result.Caster.Character.X, result.Caster.Character.Y));
        Assert.Equal("1", result.First.Character.Attributes[HellVortexBackDamageRevengeChanges]);
        Assert.Equal("1", result.Later.Character.Attributes[HellVortexBackDamageRevengeChanges]);
        Assert.True(HasLegacyStatusFlag(result.First.Character.Attributes, RevengeTailStatusFlag));
        Assert.True(HasLegacyStatusFlag(result.Later.Character.Attributes, RevengeTailStatusFlag));
        Assert.Equal(100_000L - result.FirstDamage, result.First.Character.MinHitPoints);
        Assert.Equal(100_000L - result.LaterDamage, result.Later.Character.MinHitPoints);

        var incoming = Assert.Single(ParseSpellAnimations(result.CasterSent),
            animation => animation.SpellId == HellVortexSpellId);
        AssertHellVortexRetaliationAnimation(incoming, expectedSoul: 0);
        Assert.Equal([result.FirstUid, result.LaterUid], incoming.Targets.Select(target => target.Uid));
        Assert.Equal([result.FirstDamage, result.LaterDamage], incoming.Targets.Select(target => target.Damage));
        var casterHp = HellVortexBackDamageHitpointIndices(result.CasterSent, PlayerUid);
        Assert.Equal(2, casterHp.Length);
        for (var index = 0; index < casterHp.Length; index++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.CasterSent[casterHp[index]]), out var update));
            Assert.Equal(100_000UL - (ulong)(index + 1) * HellVortexBackDamageRevengeDamage,
                Assert.Single(update.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        }
        var revengeIndices = Enumerable.Range(0, result.CasterSent.Count).Where(index =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(result.CasterSent[index]), out var animation) &&
            animation.SpellId == RevengeTailAttachStatusSpellId).ToArray();
        Assert.All(revengeIndices, index => Assert.Equal((ushort)2239, ReadFrame(result.CasterSent[index]).Header.Opcode));
        var firstHp = Assert.Single(HellVortexBackDamageHitpointIndices(result.FirstSent, result.FirstUid));
        var laterHp = Assert.Single(HellVortexBackDamageHitpointIndices(result.LaterSent, result.LaterUid));
        int Published(byte[] frame) => HellVortexRetaliationPublicationIndex(result.Timeline, frame);
        Assert.True(Published(result.CasterSent[casterHp[0]]) < Published(result.CasterSent[revengeIndices[0]]));
        Assert.True(Published(result.CasterSent[revengeIndices[0]]) < Published(result.FirstSent[firstHp]));
        Assert.True(Published(result.FirstSent[firstHp]) < Published(result.CasterSent[casterHp[1]]),
            "HellVortex.cs:42-47 completes the first Receive before entering the next candidate's Physical.BackDmg.");
        Assert.True(Published(result.CasterSent[casterHp[1]]) < Published(result.CasterSent[revengeIndices[1]]));
        Assert.True(Published(result.CasterSent[revengeIndices[1]]) < Published(result.LaterSent[laterHp]));
        Assert.True(Published(result.LaterSent[laterHp]) <
            Published(result.CasterSent[HellVortexBackDamageAnimationIndex(result.CasterSent, HellVortexSpellId)]));
    }

    [Fact]
    public async Task HandlerHellVortexPublishesEarlierIncomingHpBeforeLaterCanAttackCountercast()
    {
        var result = await RunHellVortexOrderedRetaliationAsync(laterGateCounter: true);
        var animations = ParseSpellAnimations(result.CasterSent);
        Assert.Equal(2, animations.Count);
        var counter = Assert.Single(animations, animation => animation.AttackerUid == result.LaterUid);
        Assert.Equal(HellVortexSpellId, counter.SpellId);
        Assert.Equal((0, 0), (counter.X, counter.Y));
        var outer = Assert.Single(animations, animation => animation.AttackerUid == PlayerUid);
        AssertHellVortexRetaliationAnimation(outer, expectedSoul: 0);
        Assert.Equal(result.FirstUid, Assert.Single(outer.Targets).Uid);
        Assert.Equal(result.FirstDamage, outer.Targets[0].Damage);
        var firstCounterHit = Assert.Single(counter.Targets, target => target.Uid == result.FirstUid);
        var casterCounterHit = Assert.Single(counter.Targets, target => target.Uid == PlayerUid);
        Assert.Equal(100_000L - result.FirstDamage - firstCounterHit.Damage, result.First.Character.MinHitPoints);
        Assert.Equal(100_000L - casterCounterHit.Damage, result.Caster.Character.MinHitPoints);
        Assert.Equal(100_000, result.Later.Character.MinHitPoints);
        Assert.Equal(493, result.Caster.Character.MinMana);
        Assert.Equal("89", result.Caster.Character.Attributes["Stamina"]);
        Assert.Equal(493, result.Later.Character.MinMana);
        Assert.Equal("89", result.Later.Character.Attributes["Stamina"]);
        var firstHp = HellVortexBackDamageHitpointIndices(result.FirstSent, result.FirstUid);
        Assert.Equal(2, firstHp.Length);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(result.FirstSent[firstHp[0]]), out var incomingHp));
        Assert.Equal(100_000UL - result.FirstDamage,
            Assert.Single(incomingHp.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        var casterHp = Assert.Single(HellVortexBackDamageHitpointIndices(result.CasterSent, PlayerUid));
        int Published(byte[] frame) => HellVortexRetaliationPublicationIndex(result.Timeline, frame);
        Assert.True(Published(result.FirstSent[firstHp[0]]) < Published(result.CasterSent[casterHp]),
            "HellVortex.cs:42 gates one current candidate at a time; CanAttackPlayer.cs:42-59 countercasts only on reaching the later candidate.");
        var counterIndex = SingleFrameIndex(result.CasterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var animation) && animation.AttackerUid == result.LaterUid);
        var outerIndex = SingleFrameIndex(result.CasterSent, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var animation) && animation.AttackerUid == PlayerUid);
        Assert.Equal((ushort)2239, ReadFrame(result.CasterSent[counterIndex]).Header.Opcode);
        Assert.Equal((ushort)2239, ReadFrame(result.CasterSent[outerIndex]).Header.Opcode);
        Assert.True(Published(result.FirstSent[firstHp[1]]) < Published(result.CasterSent[counterIndex]));
        Assert.True(counterIndex < outerIndex);
    }

    private static int HellVortexRetaliationPublicationIndex(IReadOnlyList<byte[]> timeline, byte[] frame)
    {
        return Assert.Single(Enumerable.Range(0, timeline.Count), index => ReferenceEquals(timeline[index], frame));
    }

    private static async Task<HellVortexOrderedRetaliationResult> RunHellVortexOrderedRetaliationAsync(
        bool killLaterTarget = false,
        bool laterGateCounter = false)
    {
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateHellVortexRetaliationRow(),
            CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 1_000),
            CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, useMana: 0,
                damage: HellVortexBackDamageRevengeDamage, coldTime: 0));
        var caster = CreateSession(x: 840, minHitPoints: 100_000, strength: 1_000, stamina: 100,
            spellIds: [HellVortexSpellId]);
        Assert.Empty(caster.PlayerItems.EquipmentItems);
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new[] { new List<byte[]>(), new List<byte[]>() };
        var timeline = new List<byte[]>();
        ConquerTcpConnection Capture(List<byte[]> frames) => new(
            localEndPoint: null,
            remoteEndPoint: null,
            sendAsync: (frame, _) =>
            {
                var bytes = frame.ToArray();
                frames.Add(bytes);
                timeline.Add(bytes);
                return ValueTask.CompletedTask;
            });
        var casterConnection = Capture(casterSent);
        var targetConnections = targetSent.Select(Capture).ToArray();
        var targets = new[]
        {
            CreateSession(uid: TargetPlayerUid, name: "HellVortexGateCandidateA", x: 842,
                minHitPoints: 100_000, strength: 1_000, stamina: 100, spellIds: []),
            CreateSession(uid: TargetPlayerUid + 1, name: "HellVortexGateCandidateB", x: 843,
                minHitPoints: 100_000, strength: 1_000, stamina: 100, spellIds: []),
        };
        sessions.Set(casterConnection, caster);
        for (var index = 0; index < targets.Length; index++)
        {
            sessions.Set(targetConnections[index], targets[index]);
        }
        // Assign roles after observing SessionStore order, not by UID or insertion assumptions.
        var order = GetVisibleTargetOrder(sessions, casterConnection, PlayerUid);
        Assert.Equal(2, order.Count);
        var firstIndex = Array.FindIndex(targets, target => target.Character.Uid == order[0]);
        var laterIndex = Array.FindIndex(targets, target => target.Character.Uid == order[1]);
        Assert.True(firstIndex >= 0);
        Assert.True(laterIndex >= 0);
        for (var role = 0; role < 2; role++)
        {
            var index = role == 0 ? firstIndex : laterIndex;
            var attributes = new Dictionary<string, string>();
            ushort[] spells;
            if (killLaterTarget && role == 0)
            {
                spells = [HeavensWonderRuntimeSpellId];
                attributes[CounterKill16350BackDamageRollAttribute] = "0";
            }
            else if (killLaterTarget)
            {
                spells = [];
                attributes["MaxHitpoints"] = "100000";
                attributes["LegacySpeffectLevel.Solid"] = "4";
                attributes["LegacySpeffectRoll.Solid"] = "0";
                attributes["LegacySpeffectLevel.Bloodthirst"] = "4";
                attributes["LegacySpeffectRoll.Bloodthirst"] = "0";
            }
            else if (laterGateCounter)
            {
                spells = role == 1 ? [HellVortexSpellId] : [];
                if (role == 1)
                {
                    attributes[RuntimeLegacyNinjaHellVortexEquippedAttribute] = "1";
                    attributes[RuntimeLegacyHellVortexRollAttribute] = "0";
                }
            }
            else
            {
                spells = [RevengeTailAttachStatusSpellId];
                attributes["StatusFlags"] = BuildStatusFlags(RevengeTailStatusFlag);
                attributes[HellVortexBackDamageRevengeChanges] = "2";
            }
            targets[index] = targets[index] with
            {
                Character = targets[index].Character with
                {
                    MinHitPoints = killLaterTarget && role == 1 ? 1 : 100_000,
                    Attributes = new Dictionary<string, string>(attributes) { ["Stamina"] = "100" },
                },
                PlayerSpells = new PlayerSpellSnapshot(spells.Select(spellId => CreatePlayerSpell(spellId)).ToArray()),
            };
            sessions.Set(targetConnections[index], targets[index]);
        }
        Assert.Equal(order, GetVisibleTargetOrder(sessions, casterConnection, PlayerUid));
        var firstDamage = CalculateHellVortexRetaliationPhysicalTail(caster, targets[firstIndex], worldData) * 12 / 28;
        var laterDamage = CalculateHellVortexRetaliationPhysicalTail(caster, targets[laterIndex], worldData) * 12 / 28;
        Assert.True(firstDamage > 0);
        using var log = new StringWriter();
        var context = new WorldPacketHandlerContext(casterConnection, log, caster,
            WorldData: worldData, Sessions: sessions,
            MonsterStates: new MonsterRuntimeStateStore(), SobNpcStates: new SobNpcRuntimeStateStore(),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);

        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(CreateHellVortexRetaliationQuery(order[0])).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var finalCaster));
        Assert.NotNull(finalCaster);
        Assert.True(sessions.TryGet(targetConnections[firstIndex], out var finalFirst));
        Assert.NotNull(finalFirst);
        Assert.True(sessions.TryGet(targetConnections[laterIndex], out var finalLater));
        Assert.NotNull(finalLater);
        return new(order[0], order[1], firstDamage, laterDamage,
            casterSent, targetSent[firstIndex], targetSent[laterIndex], timeline, finalCaster, finalFirst, finalLater);
    }

    private sealed record HellVortexOrderedRetaliationResult(
        uint FirstUid,
        uint LaterUid,
        uint FirstDamage,
        uint LaterDamage,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> FirstSent,
        IReadOnlyList<byte[]> LaterSent,
        IReadOnlyList<byte[]> Timeline,
        WorldSession Caster,
        WorldSession First,
        WorldSession Later);
}
