using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public async Task DirectTransformHpGuardRejectedRealExpiryRetainsTransformedStatusUntilAnotherAdmittedQuery()
    {
        await using var fixture = new DirectTransformHpGuardFixture(withFineRain: true);
        await fixture.TransformAsync();
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(2));
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.Owner.Character, out _));
        var start = fixture.Frames[PlayerUid].Count;
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.Owner.Character, out _));
        Assert.Equal("0", fixture.Owner.Character.Attributes[LegacyTransformState.TransformationIdAttribute]);
        await fixture.AssertOwnerAsync(3_000, 10_000);
        Assert.Equal(1, fixture.WaitCount);
        var expiredFrames = fixture.Frames[PlayerUid].ToArray().Skip(start).ToArray();
        Assert.DoesNotContain(expiredFrames, bytes => LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.DoesNotContain(ParseMsgUpdates(expiredFrames), update => update.Uid == PlayerUid &&
            update.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        Assert.Single(expiredFrames, DirectTransformHpGuardIsFinish);
        // Real212 power1/duration60 has source integer decrement0. Advancing
        // BOTH live timers does not skip a running nonzero HP-decay consumer.
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(3));
        await fixture.AssertOwnerAsync(3_000, 10_000);
        Assert.Equal(1, fixture.WaitCount); // Consumed transform expiry does not retry at6000ms.
        await fixture.QueryEquipmentAsync();
        await fixture.AssertOwnerAsync(501, 16_000);
        Assert.Equal(1, fixture.WaitCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectTransformHpGuardRealLethalReceiveFinishesBeforeTargetDeathAndHonorsAdmission(bool withFineRain)
    {
        var finishSeen = 0;
        var waitSeen = 0;
        var expected = withFineRain ? 3_000U : 500U;
        await using var fixture = new DirectTransformHpGuardFixture(withFineRain, onSend: async (current, recipient, bytes) =>
        {
            if (recipient != PlayerUid) return;
            if (DirectOverlayLifecycleIsWait(bytes))
            {
                Interlocked.Increment(ref waitSeen);
                Assert.Equal(1, current.Target.Character.MinHitPoints);
                Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, FineRainDeadStatusFlag));
                Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(current.Owner.Character, out _));
                await current.AssertOwnerAsync(3_000, 10_000);
            }
            if (!DirectTransformHpGuardIsFinish(bytes)) return;
            Interlocked.Increment(ref finishSeen);
            Assert.Equal(1, current.Target.Character.MinHitPoints); // SourceFinish BEFORE attacked.Dead.
            Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, FineRainDeadStatusFlag));
            Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(current.Owner.Character, out _));
            await current.AssertOwnerAsync(expected, withFineRain ? 10_000 : 0);
        });
        await fixture.TransformAsync();
        await fixture.KillTargetAsync();
        Assert.Equal(1, finishSeen);
        Assert.Equal(withFineRain ? 1 : 0, waitSeen);
        Assert.Equal(withFineRain ? 1 : 0, fixture.WaitCount);
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, FineRainDeadStatusFlag));
        Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(fixture.Owner.Character, out _));
        await fixture.AssertOwnerAsync(expected, withFineRain ? 10_000 : 0);
        // Handler-only same-wall-tick kill: no runtime timer or periodic invoke was started.
    }

    [Theory]
    [InlineData("wait-attacker-remove")]
    [InlineData("finish-target-rebind")]
    public async Task DirectTransformHpGuardIdentityLossAbortsTheOuterLethalReceive(string mutation)
    {
        var triggered = 0;
        var afterMutation = -1;
        WorldSession? replacement = null;
        await using var fixture = new DirectTransformHpGuardFixture(withFineRain: true,
            onSend: async (current, recipient, bytes) =>
            {
                if (recipient != PlayerUid ||
                    !(mutation == "wait-attacker-remove" ? DirectOverlayLifecycleIsWait(bytes) : DirectTransformHpGuardIsFinish(bytes)) ||
                    Interlocked.CompareExchange(ref triggered, 1, 0) != 0) return;
                Assert.Equal(1, current.Target.Character.MinHitPoints);
                Assert.False(HasLegacyStatusFlag(current.Target.Character.Attributes, FineRainDeadStatusFlag));
                await current.AssertOwnerAsync(3_000, 10_000);
                afterMutation = current.Order.Count;
                if (mutation == "wait-attacker-remove")
                {
                    current.RemoveOwner();
                }
                else
                {
                    replacement = CreateSession(uid: TargetPlayerUid + 1_000, name: "TransformFinishReplacement",
                        mapId: 1_000, x: 832, y: 584, classCode: 2_005, strength: 0, spirit: 0,
                        minHitPoints: 77_777, minMana: 77, stamina: 100, spellIds: [],
                        attributes: new Dictionary<string, string>
                        {
                            ["MaxHitpoints"] = "77777", ["TransformFinishReplacementMarker"] = "exact-new-target",
                        });
                    current.BindTarget(replacement);
                }
            });
        await fixture.TransformAsync();
        await fixture.KillTargetAsync();
        Assert.Equal(1, triggered);
        Assert.True(afterMutation >= 0);
        Assert.Empty(fixture.Order.ToArray().Skip(afterMutation)); // No remaining fanout/death/outer animation.
        if (mutation == "wait-attacker-remove")
        {
            Assert.False(fixture.TryGetOwner(out _));
            Assert.Equal(1, fixture.Target.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, FineRainDeadStatusFlag));
        }
        else
        {
            Assert.Same(replacement, fixture.Target);
            Assert.Equal(77_777, fixture.Target.Character.MinHitPoints);
            Assert.Equal(77, fixture.Target.Character.MinMana);
            Assert.Equal("100", fixture.Target.Character.Attributes["Stamina"]);
            Assert.Equal("exact-new-target", fixture.Target.Character.Attributes["TransformFinishReplacementMarker"]);
            Assert.Null(fixture.Target.EquipmentHitpointsStatus);
            Assert.Null(fixture.Target.PhysicalStatusOverlay);
            Assert.Null(fixture.Target.FineRainEquipmentAdmission);
        }
    }

    private static bool DirectTransformHpGuardIsFinish(byte[] bytes) =>
        MsgActionPacket.TryParse(ReadFrame(bytes), out var action) &&
        action.ObjId == PlayerUid && action.Type == MsgActionType.FinishTransform;

    private sealed class DirectTransformHpGuardFixture : IAsyncDisposable
    {
        private readonly bool _withFineRain;
        private readonly WorldServerFrameHandler _handler;
        private readonly WorldSessionStore _sessions;
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly WorldPacketHandlerContext _context;
        private readonly ManualRuntimeTimeProvider _clock;
        private readonly CancellationTokenSource _cancel = new();
        private readonly StringWriter _log = new();
        private bool _started;

        public DirectTransformHpGuardFixture(bool withFineRain,
            Func<DirectTransformHpGuardFixture, uint, byte[], ValueTask>? onSend = null)
        {
            _withFineRain = withFineRain;
            _clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero));
            var signed = LegacySignedMillisecondClock.FromTimeProvider(_clock, 10_000);
            const uint helmetId = 111_090;
            var helmet = DirectOverlayItem(helmetId, "TransformGuardHp500");
            var fields = helmet.Fields.ToArray();
            fields[18] = "500";
            helmet = helmet with { Fields = fields };
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(TransformSpellId, sort: 19, level: 0, useMana: 0, useStamina: 0,
                    damage: 100, duration: 2, experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(TransformSpellId, sort: 19, level: 1, useMana: 0, useStamina: 0,
                    damage: 100, duration: 2, experience: 1_000, coolDown: 0, coldTime: 0),
                CreateMagicRow(FineRainAttachStatusSpellId, sort: 6, useMana: 0, damage: 1,
                    duration: 60, coolDown: 0, coldTime: 0),
                CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, damage: 120,
                    coolDown: 0, coldTime: 0),
            ], items: [helmet], transformInfo:
            [
                new TransformInfo(TransformSpellId, 0, "DivineHare", 207, 3_000,
                    "fixture://TransformInfo.txt", LineNumber: 1, RawLine: "1350 0 DivineHare 207 3000",
                    Fields: ["1350", "0", "DivineHare", "207", "3000"]),
            ]);
            PlayerItemSnapshot Items(uint uid) => PlayerItemSnapshot.Empty with
            {
                EquipmentItems = [new PlayerItemSnapshotItem
                {
                    Uid = uid, ItemId = helmetId, Position = 1, Durability = 100, MaximumDurability = 100,
                }],
            };
            var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2, strength: 0, spirit: 0,
                minHitPoints: 300, stamina: 100, spellIds: [TransformSpellId, FineRainAttachStatusSpellId, ThunderSpellId],
                playerItems: Items(98_001), attributes: new Dictionary<string, string>
                {
                    ["MaxHitpoints"] = "500", ["PkMode"] = "0",
                });
            var target = CreateSession(uid: TargetPlayerUid, name: "TransformGuardWoundedTarget", mapId: 1_000,
                x: 832, y: 584, classCode: 2_005, strength: 0, spirit: 0, minHitPoints: 1, spellIds: [],
                playerItems: Items(98_002), attributes: new Dictionary<string, string> { ["MaxHitpoints"] = "500" });
            Assert.Equal(500U, LegacyMaxHitpointsCalculator.Calculate(owner.Character, owner.PlayerItems,
                CombatGearSnapshot.Empty, world).Total);
            Assert.Equal(500U, LegacyMaxHitpointsCalculator.Calculate(target.Character, target.PlayerItems,
                CombatGearSnapshot.Empty, world).Total);
            Assert.Null(owner.EquipmentHitpointsStatus);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                Frames[uid] = new ConcurrentQueue<byte[]>();
                _connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Frames[uid].Enqueue(bytes);
                        Order.Enqueue((uid, bytes));
                        if (onSend is not null) await onSend(this, uid, bytes);
                    });
            }
            _handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData: world,
                runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromSeconds(1), _clock, signed),
                log: TextWriter.Synchronized(_log), protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            _sessions = GetHandlerSessions(_handler);
            _sessions.Set(_connections[PlayerUid], owner);
            _sessions.Set(_connections[TargetPlayerUid], target);
            _context = new WorldPacketHandlerContext(_connections[PlayerUid], TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, TimeProvider: _clock, LegacyClock: signed,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public Dictionary<uint, ConcurrentQueue<byte[]>> Frames { get; } = [];
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Owner => AssertHandlerSession(_handler, _connections[PlayerUid]);
        public WorldSession Target => AssertHandlerSession(_handler, _connections[TargetPlayerUid]);
        public int WaitCount => Frames[PlayerUid].Count(DirectOverlayLifecycleIsWait);
        public bool TryGetOwner(out WorldSession? owner) => _sessions.TryGet(_connections[PlayerUid], out owner);
        public void BindTarget(WorldSession target) => _sessions.Set(_connections[TargetPlayerUid], target);
        public void RemoveOwner()
        {
            Assert.True(_sessions.TryRemove(_connections[PlayerUid], out var removed));
            Assert.NotNull(removed);
            Assert.Equal((long)PlayerUid, removed.Character.Uid);
        }

        public async Task TransformAsync()
        {
            if (_withFineRain)
            {
                await PacketHandlers.HandleAsync(_context with { Session = Owner },
                    ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                        X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                        ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                        SpellId: FineRainAttachStatusSpellId)).ToFrameBytes()));
                Assert.Equal("1", Owner.Character.Attributes["FineRainPower"]);
                await AssertOwnerAsync(501, 10_000);
            }
            await PacketHandlers.HandleAsync(_context with { Session = Owner }, ReadFrame(CreateTransformAttack().ToFrameBytes()));
            Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(Owner.Character, out var maximum));
            Assert.Equal(3_000U, maximum);
            // SourceClientTransform.UpdateStatus writes3000 even when a prior
            // FineRain query cached501; rejected finish must retain that3000.
            await AssertOwnerAsync(3_000, _withFineRain ? 10_000 : 0);
        }

        public async Task KillTargetAsync()
        {
            await PacketHandlers.HandleAsync(_context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                    X: 832, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                    SpellId: ThunderSpellId)).ToFrameBytes()));
        }

        public async Task<LegacyPlayerStatusPacket> QueryAsync()
        {
            var start = Frames[PlayerUid].Count;
            await PacketHandlers.HandleAsync(_context with { Session = Owner },
                ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
            var bytes = Assert.Single(Frames[PlayerUid].ToArray().Skip(start), frame =>
                LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            return status;
        }

        public async Task AssertOwnerAsync(uint maximum, int stamp)
        {
            Assert.Equal((long)maximum, Owner.Character.MinHitPoints);
            Assert.Equal(maximum, (await QueryAsync()).MaxHP);
            DirectFineRainHpAssertOwnedMaximum(Owner, maximum);
            Assert.Equal(stamp, LegacyFineRainEquipmentAdmission.ForOwner(Owner).StampMilliseconds);
        }

        public async Task QueryEquipmentAsync() =>
            await PacketHandlers.HandleAsync(_context with { Session = Owner },
                ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));

        public async Task StartAsync()
        {
            Assert.False(_started);
            StartHandlerRuntimeUpdateLoop(_handler, _connections[PlayerUid], Owner, _cancel.Token);
            _started = true;
            await WaitForScheduledManualTimerAsync(_clock);
        }

        public async Task AdvanceAsync(TimeSpan amount)
        {
            Assert.True(_started);
            await WaitForScheduledManualTimerAsync(_clock);
            _clock.Advance(amount);
            await WaitForScheduledManualTimerAsync(_clock);
        }

        public async ValueTask DisposeAsync()
        {
            await _cancel.CancelAsync();
            if (_started)
            {
                var global = typeof(WorldServerFrameHandler).GetField("_legacyRuntimeGlobalLoopStarted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(global);
                await WaitUntilAsync(() => _clock.ScheduledTimerCount == 0 && Equals(0, global.GetValue(_handler)),
                    TimeSpan.FromSeconds(3));
            }
            GC.KeepAlive(_connections);
            _cancel.Dispose();
            _log.Dispose();
        }
    }
}
