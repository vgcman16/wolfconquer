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
    private const ushort DirectArcherEnvelopeFlow = 20_210;
    private const int DirectArcherEnvelopeFlowFlag = 0x29 | 0x40 | 0x100;

    [Theory]
    [InlineData("wonder", false)]
    [InlineData("tail", false)]
    [InlineData("tail", true)]
    public async Task DirectArcherRealCounterPrecedesOriginalInteractionThenGearOpeningBeforeHp(
        string counter, bool stance)
    {
        using var fixture = new DirectArcherEnvelopeFixture(counter, stance);
        await fixture.AssertFieldsAsync();
        await fixture.AttackAsync();
        var order = fixture.Order.ToArray();
        var incoming = order.Select((frame, index) => (frame, index))
            .Where(item => DirectArcherEnvelopeIncoming(item.frame.Bytes)).ToArray();
        Assert.Equal(2, incoming.Length);
        Assert.Equal(new[] { PlayerUid, TargetPlayerUid }.Order(), incoming.Select(item => item.frame.Recipient).Order());
        foreach (var item in incoming) DirectArcherEnvelopeAssertPayload(item.frame.Bytes, stance);
        Assert.Equal(2, fixture.IncomingObservations.Count);
        Assert.All(fixture.IncomingObservations, state =>
        {
            Assert.Equal(100_000, state.Target.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(state.Target.Character.Attributes, DirectArcherEnvelopeFlowFlag));
        });
        var openings = fixture.OpeningObservations.ToArray();
        Assert.Equal(2, openings.Length); // Source UpdateTarget attempt1, attacker Update, attempt2.
        Assert.All(openings, session =>
        {
            Assert.Equal(100_000, session.Character.MinHitPoints);
            Assert.True(HasLegacyStatusFlag(session.Character.Attributes, DirectArcherEnvelopeFlowFlag));
        });
        var flowIndices = Enumerable.Range(0, order.Length).Where(index =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(order[index].Bytes), out var animation) &&
            animation.SpellId == DirectArcherEnvelopeFlow).ToArray();
        Assert.NotEmpty(flowIndices);
        Assert.True(incoming.Max(item => item.index) < flowIndices.Min());
        var hp = order.Select((frame, index) => (frame, index)).Where(item =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(item.frame.Bytes), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints)).ToArray();
        var targetHp = Assert.Single(hp);
        Assert.True(flowIndices.Max() < targetHp.index);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(targetHp.frame.Bytes), out var hpPacket));
        Assert.Equal(stance ? 99_883UL : 99_805UL,
            Assert.Single(hpPacket.Updates, entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints).Value);
        Assert.Equal(stance ? 99_883 : 99_805, fixture.Target.Character.MinHitPoints);
        var counterId = counter == "wonder" ? HeavensWonderRuntimeSpellId : RevengeTailAttachStatusSpellId;
        var counterFrames = order.Select((frame, index) => (frame, index)).Where(item =>
            LegacySpellAnimationPacket.TryParse(ReadFrame(item.frame.Bytes), out var animation) &&
            animation.SpellId == counterId).ToArray();
        if (stance)
        {
            Assert.Empty(counterFrames);
            Assert.Equal(100_000, fixture.Owner.Character.MinHitPoints);
            Assert.Equal("2", fixture.Target.Character.Attributes["LegacyRevengeTailChange"]);
        }
        else
        {
            Assert.Equal(2, counterFrames.Length);
            Assert.True(counterFrames.Max(item => item.index) < incoming.Min(item => item.index));
            var counterAnimation = Assert.Single(ParseSpellAnimations(order.Where(frame => frame.Recipient == PlayerUid)
                .Select(frame => frame.Bytes)), animation => animation.SpellId == counterId);
            Assert.Equal(TargetPlayerUid, counterAnimation.AttackerUid);
            var reflected = Assert.Single(counterAnimation.Targets, hit => hit.Uid == PlayerUid);
            Assert.True(reflected.Damage > 0);
            Assert.Equal(100_000L - reflected.Damage, fixture.Owner.Character.MinHitPoints);
            Assert.All(fixture.IncomingObservations, state =>
                Assert.Equal(100_000L - reflected.Damage, state.Owner.Character.MinHitPoints));
            if (counter == "tail")
            {
                Assert.Equal(5_000U, reflected.Damage);
                Assert.Equal(1, counterAnimation.BombFlag);
                Assert.Equal("1", fixture.Target.Character.Attributes["LegacyRevengeTailChange"]);
                Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, RevengeTailStatusFlag));
            }
            else
            {
                Assert.Equal(493, fixture.Target.Character.MinMana);
                Assert.Equal("95", fixture.Target.Character.Attributes["Stamina"]);
                Assert.True(fixture.Target.MagicCooldowns?.ContainsKey(HeavensWonderRuntimeSpellId) == true);
            }
        }
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 5));
    }

    [Theory]
    [InlineData("counter", "attacker", "remove")]
    [InlineData("counter", "attacker", "rebind")]
    [InlineData("counter", "target", "remove")]
    [InlineData("counter", "target", "rebind")]
    [InlineData("incoming", "attacker", "remove")]
    [InlineData("incoming", "attacker", "rebind")]
    [InlineData("incoming", "target", "remove")]
    [InlineData("incoming", "target", "rebind")]
    public async Task DirectArcherOriginalUidsFenceOuterReceiveAfterCounterAndIncomingCallbacks(
        string phase, string participant, string mutation)
    {
        var triggers = 0;
        var boundary = -1;
        WorldSession? ownerAtBoundary = null;
        WorldSession? targetAtBoundary = null;
        WorldSession? replacement = null;
        using var fixture = new DirectArcherEnvelopeFixture("tail", stance: false,
            onSend: (current, recipient, bytes) =>
            {
                var isCounter = recipient == TargetPlayerUid &&
                    LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                    animation.SpellId == RevengeTailAttachStatusSpellId;
                var isIncoming = recipient == PlayerUid && DirectArcherEnvelopeIncoming(bytes);
                if (!(phase == "counter" ? isCounter : isIncoming) ||
                    Interlocked.CompareExchange(ref triggers, 1, 0) != 0) return ValueTask.CompletedTask;
                ownerAtBoundary = current.Owner;
                targetAtBoundary = current.Target;
                Assert.Equal(95_000, ownerAtBoundary.Character.MinHitPoints);
                Assert.Equal(100_000, targetAtBoundary.Character.MinHitPoints);
                Assert.False(HasLegacyStatusFlag(targetAtBoundary.Character.Attributes, DirectArcherEnvelopeFlowFlag));
                boundary = current.Order.Count - 1;
                var mutationSlot = participant == "attacker" ? PlayerUid : TargetPlayerUid;
                if (mutation == "remove") current.Remove(mutationSlot);
                else replacement = current.Rebind(mutationSlot);
                return ValueTask.CompletedTask;
            });
        await fixture.AssertFieldsAsync();
        var start = fixture.Order.Count;
        await fixture.AttackAsync();
        Assert.Equal(1, triggers);
        Assert.True(boundary >= start);
        Assert.NotNull(ownerAtBoundary);
        Assert.NotNull(targetAtBoundary);
        var slot = participant == "attacker" ? PlayerUid : TargetPlayerUid;
        var present = fixture.TrySlot(slot, out var affected);
        if (mutation == "remove")
        {
            Assert.False(present);
            Assert.Null(affected);
        }
        else
        {
            Assert.True(present);
            Assert.Same(replacement, affected);
            Assert.NotNull(affected);
            Assert.Equal((long)slot + 8_000, affected.Character.Uid);
            Assert.Equal(77_777, affected.Character.MinHitPoints);
            Assert.Equal(77, affected.Character.MinMana);
            Assert.Equal("33", affected.Character.Attributes["Stamina"]);
            Assert.Equal("replacement", affected.Character.Attributes["DirectArcherEnvelopeIdentity"]);
            Assert.Null(affected.EquipmentHitpointsStatus);
            Assert.Null(affected.PhysicalStatusOverlay);
            Assert.Null(affected.FineRainEquipmentAdmission);
        }
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        Assert.Empty(fixture.OpeningObservations);
        Assert.DoesNotContain(frames, frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame.Bytes), out var update) &&
            update.Uid == TargetPlayerUid && update.Updates.Any(entry =>
                entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        Assert.DoesNotContain(frames, frame => LegacySpellAnimationPacket.TryParse(ReadFrame(frame.Bytes), out var animation) &&
            animation.SpellId == DirectArcherEnvelopeFlow);
        Assert.DoesNotContain(frames, frame => MsgAttackPacket.TryParse(ReadFrame(frame.Bytes), out var attack) &&
            attack.Query.AttackType == MsgAttackType.Death);
        var ownerIncoming = frames.Where(frame => frame.Recipient == PlayerUid && DirectArcherEnvelopeIncoming(frame.Bytes)).ToArray();
        if (phase == "counter" && participant == "attacker")
            Assert.Empty(ownerIncoming); // No original owner remains to publish the incoming action.
        else
            DirectArcherEnvelopeAssertPayload(Assert.Single(ownerIncoming).Bytes, stance: false);
        Assert.All(frames.Where(frame => DirectArcherEnvelopeIncoming(frame.Bytes)),
            frame => DirectArcherEnvelopeAssertPayload(frame.Bytes, stance: false));
        if (participant == "attacker")
        {
            Assert.True(fixture.TrySlot(TargetPlayerUid, out var target));
            Assert.Same(targetAtBoundary, target);
            Assert.Equal(100_000, target!.Character.MinHitPoints);
        }
        else
        {
            Assert.Equal(95_000, fixture.Owner.Character.MinHitPoints);
        }
        if (replacement is not null)
        {
            var replacementUid = replacement.Character.Uid;
            Assert.DoesNotContain(ParseMsgUpdates(frames.Select(frame => frame.Bytes)), update => update.Uid == replacementUid);
            Assert.All(ParseSpellAnimations(frames.Select(frame => frame.Bytes)), animation =>
                Assert.DoesNotContain(animation.Targets, hit => hit.Uid == replacementUid));
        }
    }

    private static bool DirectArcherEnvelopeIncoming(byte[] bytes) =>
        MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) && packet.Query.AttackType == MsgAttackType.Archer;

    private static void DirectArcherEnvelopeAssertPayload(byte[] bytes, bool stance)
    {
        Assert.Equal((ushort)2135, ReadFrame(bytes).Header.Opcode);
        Assert.True(MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(PlayerUid, packet.Query.Uid);
        Assert.Equal(TargetPlayerUid, packet.Query.OpponentUid);
        Assert.Equal((120, 121), ((int)packet.Query.X, (int)packet.Query.Y));
        Assert.Equal(stance ? 117 : 195, packet.Query.Damage);
        Assert.Equal(stance ? MsgAttackEffect.Block : MsgAttackEffect.None, packet.Query.Effect);
        Assert.Equal((ushort)0, packet.Query.SpellId);
    }

    private sealed class DirectArcherEnvelopeFixture : IDisposable
    {
        private readonly WorldSessionStore _sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly StringWriter _log = new();
        private readonly WorldPacketHandlerContext _context;

        public DirectArcherEnvelopeFixture(string counter, bool stance,
            Func<DirectArcherEnvelopeFixture, uint, byte[], ValueTask>? onSend = null)
        {
            var world = CreateMagicCatalog(CreateMonsterFamily(), [],
                CreateCounterKillRuntimeRow(HeavensWonderRuntimeSpellId, coolDown: 0, coldTime: 1_000),
                CreateMagicRow(RevengeTailAttachStatusSpellId, sort: 6, useMana: 0, useStamina: 0,
                    damage: 5_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(DirectArcherEnvelopeFlow, sort: 6, useMana: 0, useStamina: 0,
                    damage: 1, gDamage: 1, duration: 10, coolDown: 0, coldTime: 0));
            var owner = CreateSession(mapId: 1_000, classCode: 4_005, strength: 300, spirit: 0,
                minHitPoints: 100_000, stamina: 100, spellIds: [],
                attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "120" });
            var target = CreateSession(uid: TargetPlayerUid, name: "DirectArcherEnvelopeTarget", mapId: 1_000,
                x: 832, y: 584, classCode: 13_005, strength: 1_000, spirit: 0,
                minHitPoints: 100_000, stamina: 100,
                spellIds: [counter == "wonder" ? HeavensWonderRuntimeSpellId : RevengeTailAttachStatusSpellId, DirectArcherEnvelopeFlow],
                attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0", ["MaxHitpoints"] = "100000", ["BattlePower"] = "120",
                    ["StatusFlags"] = BuildStatusFlags((counter == "tail" ? new[] { RevengeTailStatusFlag } : Array.Empty<int>())
                        .Concat(stance ? new[] { DefensiveStanceStatusFlag } : Array.Empty<int>()).ToArray()),
                    ["LegacyRevengeTailChange"] = "2", [CounterKill16350BackDamageRollAttribute] = "0",
                    ["LegacyCombatGearPassiveRoll.20210"] = "0",
                }) with
            {
                CombatGear = CombatGearSnapshot.Empty with
                {
                    Count = 1, Items = [new CombatGearSnapshotItem(1_302, 1, 0, 1, 0, 0, [[], []], [])],
                },
            };
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                _connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, token) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (DirectArcherEnvelopeIncoming(bytes) && TrySlot(PlayerUid, out var liveOwner) &&
                            TrySlot(TargetPlayerUid, out var liveTarget) && liveOwner is not null && liveTarget is not null &&
                            liveOwner.Character.Uid == PlayerUid && liveTarget.Character.Uid == TargetPlayerUid)
                            IncomingObservations.Enqueue((liveOwner, liveTarget));
                        if (uid == TargetPlayerUid && LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var animation) &&
                            animation.SpellId == DirectArcherEnvelopeFlow && TrySlot(TargetPlayerUid, out var opening) &&
                            opening is not null && opening.Character.Uid == TargetPlayerUid)
                            OpeningObservations.Enqueue(opening);
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            _sessions.Set(_connections[PlayerUid], owner);
            _sessions.Set(_connections[TargetPlayerUid], target);
            _context = new WorldPacketHandlerContext(_connections[PlayerUid], TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, MonsterStates: new MonsterRuntimeStateStore(),
                SobNpcStates: new SobNpcRuntimeStateStore(), TimeProvider: new DirectOverlayFrozenClock(),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public ConcurrentQueue<(WorldSession Owner, WorldSession Target)> IncomingObservations { get; } = new();
        public ConcurrentQueue<WorldSession> OpeningObservations { get; } = new();
        public bool TrySlot(uint slot, out WorldSession? session) => _sessions.TryGet(_connections[slot], out session);
        public WorldSession Owner => Slot(PlayerUid);
        public WorldSession Target => Slot(TargetPlayerUid);
        private WorldSession Slot(uint slot)
        {
            Assert.True(TrySlot(slot, out var session));
            return Assert.IsType<WorldSession>(session);
        }
        public void Remove(uint slot)
        {
            Assert.True(_sessions.TryRemove(_connections[slot], out var removed));
            Assert.NotNull(removed);
            Assert.Equal((long)slot, removed.Character.Uid);
        }
        public WorldSession Rebind(uint slot)
        {
            var replacement = CreateSession(uid: slot + 8_000, name: "DirectArcherReplacement",
                mapId: 1_000, x: 832, y: 584, minHitPoints: 77_777, minMana: 77, stamina: 33, spellIds: [],
                attributes: new Dictionary<string, string>
                {
                    ["MaxHitpoints"] = "77777", ["DirectArcherEnvelopeIdentity"] = "replacement",
                });
            _sessions.Set(_connections[slot], replacement);
            return replacement;
        }
        public async Task AssertFieldsAsync()
        {
            var attacker = await StatusAsync(PlayerUid);
            var target = await StatusAsync(TargetPlayerUid);
            Assert.Equal(300U, attacker.MinAttack);
            Assert.Equal(301U, attacker.MaxAttack);
            Assert.Equal(0U, attacker.CriticalStrike);
            Assert.Equal(0U, attacker.Breakthrough);
            Assert.Equal(0U, attacker.PhysicalDamageIncrease);
            Assert.Equal(0U, target.PhysicalDamageDecrease);
            Assert.Equal(0U, target.Dodge);
            Assert.Equal(0U, target.ItemBless);
        }
        private async Task<LegacyPlayerStatusPacket> StatusAsync(uint uid)
        {
            var start = Order.Count;
            await PacketHandlers.HandleAsync(_context with { Connection = _connections[uid], Session = Slot(uid) },
                ReadFrame(new LegacyPlayerStatusQueryPacket(uid).ToFrameBytes()));
            var bytes = Assert.Single(Order.ToArray().Skip(start), frame => frame.Recipient == uid &&
                LegacyPlayerStatusPacket.TryParse(ReadFrame(frame.Bytes), out _)).Bytes;
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var packet));
            return packet;
        }
        public async Task AttackAsync() => await PacketHandlers.HandleAsync(_context with { Session = Owner },
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                X: 120, Y: 121, AttackType: MsgAttackType.Archer, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        public void Dispose() { GC.KeepAlive(_connections); _log.Dispose(); }
    }
}
