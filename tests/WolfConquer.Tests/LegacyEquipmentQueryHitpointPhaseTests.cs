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
    [Fact]
    public async Task DirectQueryHpNeutralStartupPublishesThreeSourcePairsAtTheirActualPhases()
    {
        var observations = new ConcurrentQueue<(uint Maximum, long Hitpoints)>();
        var capture = false;
        using var fixture = new DirectQueryHpFixture(onSend: (current, recipient, bytes) =>
        {
            if (capture && recipient == PlayerUid && DirectQueryHpIsFirstMax(bytes))
                observations.Enqueue(DirectQueryHpObserve(current.Owner));
            return ValueTask.CompletedTask;
        });
        Assert.Null(fixture.Owner.EquipmentHitpointsStatus);
        var start = fixture.Order.Count;
        capture = true;
        await fixture.QueryEquipmentAsync();
        capture = false;
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        var owner = frames.Where(frame => frame.Recipient == PlayerUid).Select(frame => frame.Bytes).ToArray();
        DirectQueryHpAssertPairs(owner, [(500U, 300U), (500U, 300U), (500U, 300U)]);
        Assert.Equal(new[] { (500U, 300L), (500U, 300L), (500U, 300L) }, observations.ToArray());
        var hpIndices = owner.Select((bytes, index) => (bytes, index))
            .Where(entry => DirectQueryHpIsHpPacket(entry.bytes)).Select(entry => entry.index).ToArray();
        var equipmentIndex = Array.FindIndex(owner, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        var statusIndex = Array.FindIndex(owner, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(equipmentIndex >= 0 && equipmentIndex < hpIndices[0]); // AppendItems before UpdateStats.
        Assert.True(statusIndex > hpIndices[1] && statusIndex < hpIndices[2]); // StatusCreate after AddRune, before clamp.
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(owner[statusIndex]), out var status));
        Assert.Equal(500U, status.MaxHP);
        Assert.Equal(300, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 500);
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
    }

    [Fact]
    public async Task DirectQueryHpWoundedActiveTransformExposesBaseClampSetterAndTransformedExplicitPair()
    {
        var observations = new ConcurrentQueue<(uint Maximum, long Hitpoints)>();
        var capture = false;
        using var fixture = new DirectQueryHpFixture(nativeWoundCasterQueryControls: true,
            onSend: (current, recipient, bytes) =>
        {
            if (capture && recipient == PlayerUid && DirectQueryHpIsFirstMax(bytes))
            {
                Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(current.Owner.Character, out _));
                observations.Enqueue(DirectQueryHpObserve(current.Owner));
            }
            return ValueTask.CompletedTask;
        });
        await fixture.PrepareQueryHpNativeWoundCasterAsync();
        await fixture.TransformAsync();
        await fixture.WoundOwnerAsync(); // Real peer251 earns MA1; raw239 then native caller /2 =120.
        Assert.Equal(2_880, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 3_000);
        var start = fixture.Order.Count;
        capture = true;
        await fixture.QueryEquipmentAsync();
        capture = false;
        var frames = fixture.Order.ToArray().Skip(start).Where(frame => frame.Recipient == PlayerUid)
            .Select(frame => frame.Bytes).ToArray();
        DirectQueryHpAssertPairs(frames,
            [(500U, 2_880U), (500U, 500U), (500U, 3_000U), (3_000U, 3_000U)]);
        Assert.Equal(new[] { (500U, 2_880L), (500U, 500L), (500U, 3_000L), (3_000U, 3_000L) },
            observations.ToArray());
        var packets = ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
        Assert.Equal(8, packets.Length); // Four source pairs, not one final-total replay.
        Assert.Equal(3_000, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 3_000);
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.Owner.Character, out _));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRainDeadStatusFlag));
    }

    [Fact]
    public async Task DirectQueryHpReal1350SendsPriorStatePeerSpawnThenSetterAndExplicitHpPhasesBeforeAnimation()
    {
        var peerObserved = 0;
        var observations = new ConcurrentQueue<(uint Maximum, long Hitpoints)>();
        using var fixture = new DirectQueryHpFixture(onSend: (current, recipient, bytes) =>
        {
            if (recipient == TargetPlayerUid &&
                LegacyPlayerSpawnPacket.TryParse(ReadFrame(bytes), out var spawn) &&
                spawn.GetNumericField(2) == PlayerUid)
            {
                Interlocked.Increment(ref peerObserved);
                Assert.Equal(300UL, spawn.GetNumericField(21)); // Actual source current HP.
                Assert.Equal(500UL, spawn.GetNumericField(23)); // Prior Status.Max, NOT transform3000 yet.
                Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(current.Owner.Character, out var maximum));
                Assert.Equal(3_000U, maximum); // Metadata is active before UpdateStatus.
                Assert.Equal((500U, 300L), DirectQueryHpObserve(current.Owner));
            }
            if (recipient == PlayerUid && DirectQueryHpIsFirstMax(bytes))
                observations.Enqueue(DirectQueryHpObserve(current.Owner));
            return ValueTask.CompletedTask;
        });
        Assert.Null(fixture.Owner.EquipmentHitpointsStatus);
        await fixture.TransformAsync();
        Assert.Equal(1, peerObserved);
        Assert.Equal(new[] { (500U, 3_000L), (3_000U, 3_000L) }, observations.ToArray());
        var frames = fixture.Order.ToArray();
        DirectQueryHpAssertPairs(frames.Where(frame => frame.Recipient == PlayerUid).Select(frame => frame.Bytes),
            [(500U, 3_000U), (3_000U, 3_000U)]);
        var spawnIndex = Array.FindIndex(frames, frame => frame.Recipient == TargetPlayerUid &&
            LegacyPlayerSpawnPacket.TryParse(ReadFrame(frame.Bytes), out var spawn) && spawn.GetNumericField(2) == PlayerUid);
        var hpIndices = frames.Select((frame, index) => (frame, index))
            .Where(entry => entry.frame.Recipient == PlayerUid && DirectQueryHpIsHpPacket(entry.frame.Bytes))
            .Select(entry => entry.index).ToArray();
        var animationIndex = Array.FindIndex(frames, frame => frame.Recipient == PlayerUid &&
            LegacySpellAnimationPacket.TryParse(ReadFrame(frame.Bytes), out var animation) &&
            animation.SpellId == TransformSpellId);
        Assert.True(spawnIndex >= 0 && spawnIndex < hpIndices[0]);
        Assert.True(animationIndex > hpIndices[^1]);
        Assert.Equal(3_000, fixture.Owner.Character.MinHitPoints);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 3_000);
    }

    [Fact]
    public async Task DirectQueryHpPairRetainsCapturedMaxButReadsFreshHpAfterFirstMaxRunsARealTransform()
    {
        var phase = 0;
        LegacyMsgUpdatePacket? resumed = null;
        using var fixture = new DirectQueryHpFixture(onSend: async (current, recipient, bytes) =>
        {
            if (recipient != PlayerUid) return;
            if (DirectQueryHpIsFirstMax(bytes) && Interlocked.CompareExchange(ref phase, 1, 0) == 0)
            {
                Assert.Equal((500U, 300L), DirectQueryHpObserve(current.Owner));
                await current.TransformAsync(); // Real public1350; never install a desired HP/cache answer.
                Assert.Equal((3_000U, 3_000L), DirectQueryHpObserve(current.Owner));
                Volatile.Write(ref phase, 2);
                return;
            }
            if (Volatile.Read(ref phase) == 2 && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                update.Uid == PlayerUid && update.Updates.Count == 2 &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints) &&
                Interlocked.CompareExchange(ref phase, 3, 2) == 2)
            {
                resumed = update; // The immediate resumed OUTER pair, not a later final-state packet.
                Assert.Equal((3_000U, 3_000L), DirectQueryHpObserve(current.Owner));
            }
        });
        await fixture.QueryEquipmentAsync();
        Assert.Equal(3, Volatile.Read(ref phase));
        Assert.NotNull(resumed);
        Assert.Collection(resumed.Updates,
            max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal(500UL, max.Value); },
            hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal(3_000UL, hp.Value); });
        Assert.Equal((3_000U, 3_000L), DirectQueryHpObserve(fixture.Owner));
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.Owner.Character, out _));
    }

    private static (uint Maximum, long Hitpoints) DirectQueryHpObserve(WorldSession session)
    {
        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(session, out var maximum));
        return (maximum, session.Character.MinHitPoints);
    }

    private static bool DirectQueryHpIsFirstMax(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Count == 1 && packet.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints;

    private static bool DirectQueryHpIsHpPacket(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints);

    private static void DirectQueryHpAssertPairs(IEnumerable<byte[]> frames, (uint Maximum, uint Hitpoints)[] phases)
    {
        var packets = ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints))
            .ToArray();
        Assert.Equal(phases.Length * 2, packets.Length);
        for (var index = 0; index < phases.Length; index++)
        {
            var maximum = Assert.Single(packets[index * 2].Updates);
            Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, maximum.Type);
            Assert.Equal((ulong)phases[index].Maximum, maximum.Value);
            var phase = phases[index];
            Assert.Collection(packets[index * 2 + 1].Updates,
                max => { Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, max.Type); Assert.Equal((ulong)phase.Maximum, max.Value); },
                hp => { Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, hp.Type); Assert.Equal((ulong)phase.Hitpoints, hp.Value); });
        }
    }

    private sealed partial class DirectQueryHpFixture : IDisposable
    {
        private readonly WorldSessionStore _sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly WorldPacketHandlerContext _context;
        private readonly StringWriter _log = new();

        public DirectQueryHpFixture(Func<DirectQueryHpFixture, uint, byte[], ValueTask>? onSend = null,
            bool nativeWoundCasterQueryControls = false)
        {
            var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
            const uint helmetId = 111_096;
            var helmet = DirectOverlayItem(helmetId, "QueryHpRealItem500");
            var fields = helmet.Fields.ToArray();
            fields[18] = "500";
            helmet = helmet with { Fields = fields };
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(TransformSpellId, sort: 19, level: 0, useMana: 0, useStamina: 0,
                    damage: 100, duration: 60, experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(TransformSpellId, sort: 19, level: 1, useMana: 0, useStamina: 0,
                    damage: 100, duration: 60, experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0,
                    damage: nativeWoundCasterQueryControls ? 239U : 119U, coolDown: 0, coldTime: 0),
            ], items: [helmet], transformInfo:
            [
                new TransformInfo(TransformSpellId, 0, "DivineHare", 207, 3_000,
                    "fixture://TransformInfo.txt", LineNumber: 1, RawLine: "1350 0 DivineHare 207 3000",
                    Fields: ["1350", "0", "DivineHare", "207", "3000"]),
            ]);
            var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2, strength: 0, spirit: 0,
                minHitPoints: 300, stamina: 100, spellIds: [TransformSpellId],
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = [new PlayerItemSnapshotItem
                    {
                        Uid = 98_201, ItemId = helmetId, Position = 1, Durability = 100, MaximumDurability = 100,
                    }],
                }, attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["BattlePower"] = "120" });
            var peer = CreateSession(uid: TargetPlayerUid, name: "QueryHpPeer", mapId: 1_000, x: 832, y: 584,
                classCode: 2_005, strength: 0, spirit: 0, minHitPoints: 500, stamina: 100,
                spellIds: [ThunderSpellId],
                playerItems: nativeWoundCasterQueryControls ? PlayerItemSnapshot.Empty with { EquipmentItems =
                    [new PlayerItemSnapshotItem { Uid = 98_202, ItemId = helmetId, Position = 1,
                        Durability = 100, MaximumDurability = 100 }] } : PlayerItemSnapshot.Empty,
                attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0", ["BattlePower"] = "120",
                });
            Assert.Equal(500U, LegacyMaxHitpointsCalculator.Calculate(owner.Character, owner.PlayerItems,
                CombatGearSnapshot.Empty, world).Total);
            Assert.False(owner.Character.Attributes.ContainsKey("MaxHitpoints")); // No stored fake raw base.
            Assert.Equal(2, owner.Character.Reborn);
            Assert.Null(owner.EquipmentHitpointsStatus);
            var attackerFields = LegacyPlayerStatusCalculator.CalculateFields(peer.Character, peer.PlayerItems, world,
                checked((uint)peer.Character.MinHitPoints), prestigeLevel: 0);
            var targetFields = LegacyPlayerStatusCalculator.CalculateFields(owner.Character, owner.PlayerItems, world,
                checked((uint)owner.Character.MinHitPoints), prestigeLevel: 0);
            Assert.Equal(1U, attackerFields[LegacyPlayerStatusPacket.MagicAttackIndex]);
            Assert.Equal(0U, attackerFields[LegacyPlayerStatusPacket.MagicPercentIndex]);
            Assert.Equal(0U, attackerFields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex]);
            Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
            Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.ItemBlessIndex]);
            Assert.Equal(0U, targetFields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                _connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            }
            _sessions.Set(_connections[PlayerUid], owner);
            _sessions.Set(_connections[TargetPlayerUid], peer);
            _context = new WorldPacketHandlerContext(_connections[PlayerUid], TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, TimeProvider: clock,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Owner
        {
            get
            {
                Assert.True(_sessions.TryGet(_connections[PlayerUid], out var current));
                return Assert.IsType<WorldSession>(current);
            }
        }

        public async Task QueryEquipmentAsync() =>
            await PacketHandlers.HandleAsync(_context with { Session = Owner },
                ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));

        public async Task TransformAsync() =>
            await PacketHandlers.HandleAsync(_context with { Session = Owner }, ReadFrame(CreateTransformAttack().ToFrameBytes()));

        public async Task WoundOwnerAsync()
        {
            Assert.True(_sessions.TryGet(_connections[TargetPlayerUid], out var peer));
            Assert.NotNull(peer);
            var start = Order.Count;
            await PacketHandlers.HandleAsync(_context with { Connection = _connections[TargetPlayerUid], Session = peer },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: TargetPlayerUid, OpponentUid: PlayerUid,
                    X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: ThunderSpellId)).ToFrameBytes()));
            var animation = Assert.Single(ParseSpellAnimations(Order.ToArray().Skip(start)
                .Where(frame => frame.Recipient == PlayerUid).Select(frame => frame.Bytes)),
                packet => packet.SpellId == ThunderSpellId);
            Assert.Equal(120U, Assert.Single(animation.Targets).Damage);
        }

        public void Dispose()
        {
            GC.KeepAlive(_connections);
            _log.Dispose();
        }
    }
}
