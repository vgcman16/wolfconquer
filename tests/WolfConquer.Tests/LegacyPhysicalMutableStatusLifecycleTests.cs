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
    public async Task DirectOverlayRuntimeEarned313ExpiresInclusivelyAndAdmittedQueryClearsCurrentValues()
    {
        await using var fixture = new DirectOverlayLifecycleFixture(lethal: false);
        await fixture.EarnFrontAsync();
        Assert.Equal(66U, (await fixture.QueryAsync()).Breakthrough);
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(9));
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(999));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
        Assert.Equal(66U, (await fixture.QueryAsync()).Breakthrough);
        var ownerStart = fixture.Frames[PlayerUid].Count;
        var viewerStart = fixture.Frames[PlayerUid + 50].Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(1));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).FrontBreakExpiresAtUtc);
        Assert.Equal(60U, (await fixture.QueryAsync()).Breakthrough);
        Assert.Equal(0, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Single(fixture.Frames[PlayerUid].ToArray().Skip(ownerStart), DirectOverlayLifecycleClears313);
        Assert.Single(fixture.Frames[PlayerUid + 50].ToArray().Skip(viewerStart), DirectOverlayLifecycleClears313);
        Assert.Equal(0, fixture.WaitCount);
    }

    [Fact]
    public async Task DirectOverlayRuntimeRejected313ExpiryKeepsLaterEarnedCounteractionAndDoesNotRetry()
    {
        await using var fixture = new DirectOverlayLifecycleFixture(lethal: false);
        await fixture.EarnFrontAsync();
        await fixture.StartAsync();
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(7));
        await fixture.CastFineRainAsync();
        var afterFineRain = await fixture.QueryAsync();
        Assert.Equal(60U, afterFineRain.Breakthrough); // The admitted query already discarded old66.
        Assert.Equal(50U, afterFineRain.Counteraction);
        Assert.Equal(17_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(291U, await fixture.StrikeOwnerAsync());
        Assert.Equal(75U, (await fixture.QueryAsync()).Counteraction); // Real receive AFTER the reset.
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(1));
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(999));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
        var expiryStart = fixture.Order.Count;
        await fixture.AdvanceAsync(TimeSpan.FromMilliseconds(1));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).FrontBreakExpiresAtUtc);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain1StatusFlag));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain2StatusFlag));
        var rejected = await fixture.QueryAsync();
        Assert.Equal(60U, rejected.Breakthrough);
        Assert.Equal(75U, rejected.Counteraction);
        Assert.Equal(17_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        var expiryFrames = fixture.Order.ToArray().Skip(expiryStart).ToArray();
        var clear = Array.FindIndex(expiryFrames, entry => entry.Recipient == PlayerUid && DirectOverlayLifecycleClears313(entry.Bytes));
        var wait = Array.FindIndex(expiryFrames, entry => entry.Recipient == PlayerUid && DirectOverlayLifecycleIsWait(entry.Bytes));
        Assert.True(clear >= 0 && wait > clear); // RemoveFlag publication precedes the rejected query.
        Assert.Equal(1, fixture.WaitCount);
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(75U, (await fixture.QueryAsync()).Counteraction);
        Assert.Equal(1, fixture.WaitCount); // Passing6000ms does not retry a consumed313 event.
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).FrontBreakExpiresAtUtc);
        await fixture.QueryEquipmentThroughRuneStorageAsync();
        var admitted = await fixture.QueryAsync();
        Assert.Equal(50U, admitted.Counteraction);
        Assert.Equal(60U, admitted.Breakthrough);
        Assert.Equal(23_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(1, fixture.WaitCount);
    }

    [Fact]
    public async Task DirectOverlayRealDeathRemovesFineRainBeforeResetButPreservesItsAdmissionStamp()
    {
        await using var fixture = new DirectOverlayLifecycleFixture(lethal: true);
        await fixture.StartAsync();
        await fixture.CastFineRainAsync();
        Assert.Equal(600, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(600U, (await fixture.QueryAsync()).MaxHP);
        await fixture.EarnFrontAsync();
        Assert.Equal(66U, (await fixture.QueryAsync()).Breakthrough);
        var firstReceiveStart = fixture.Frames[PlayerUid].Count;
        Assert.Equal(291U, await fixture.StrikeOwnerAsync());
        Assert.Equal(309, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(75U, (await fixture.QueryAsync()).Counteraction);
        DirectFineRainHpAssertMaximumUpdates(fixture.Frames[PlayerUid].ToArray().Skip(firstReceiveStart), 600);
        // Same source-ms tick, still INSIDE the FineRain guard. Raw row0
        // normalizes to power100, so real base500 was filled to600.
        // Second Imp success75->112 suppresses breakthrough:300/2 +floor75 =225.
        var secondReceiveStart = fixture.Frames[PlayerUid].Count;
        Assert.Equal(225U, await fixture.StrikeOwnerAsync());
        Assert.Equal(84, fixture.Owner.Character.MinHitPoints);
        Assert.Equal(112U, (await fixture.QueryAsync()).Counteraction);
        DirectFineRainHpAssertMaximumUpdates(fixture.Frames[PlayerUid].ToArray().Skip(secondReceiveStart), 600);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRainDeadStatusFlag));
        // Third real Imp success112->168 still suppresses breakthrough;
        // its225 receive is now lethal, then source death rebuild resets CA50.
        Assert.Equal(225U, await fixture.StrikeOwnerAsync());
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain1StatusFlag));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, FineRain2StatusFlag));
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).FrontBreakExpiresAtUtc);
        var dead = await fixture.QueryAsync();
        Assert.Equal(60U, dead.Breakthrough);
        Assert.Equal(50U, dead.Counteraction);
        Assert.Equal(500U, dead.MaxHP);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 500);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        foreach (var key in new[]
        {
            "LegacyFineRain1ExpiresAtUtc", "LegacyFineRain2ExpiresAtUtc",
            "LegacyFineRain1RemoveOnDead", "LegacyFineRain2RemoveOnDead",
        })
            Assert.False(fixture.Owner.Character.Attributes.ContainsKey(key));
        Assert.Equal("100", fixture.Owner.Character.Attributes["FineRainPower"]);
        Assert.Equal("100", fixture.Owner.Character.Attributes["defFineRainPower"]);
        Assert.Equal(0, fixture.WaitCount); // Normally registered FineRain was cleared BEFORE admission.
        await fixture.AdvanceAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, fixture.Owner.Character.MinHitPoints);
        Assert.Null(LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).FrontBreakExpiresAtUtc);
        Assert.Equal(50U, (await fixture.QueryAsync()).Counteraction);
        Assert.Equal(500U, (await fixture.QueryAsync()).MaxHP);
        DirectFineRainHpAssertOwnedMaximum(fixture.Owner, 500);
        Assert.Equal(10_000, LegacyFineRainEquipmentAdmission.ForOwner(fixture.Owner).StampMilliseconds);
        Assert.Equal(0, fixture.WaitCount);
    }

    private static bool DirectOverlayLifecycleClears313(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == PlayerUid &&
        update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && !IsLegacyStatusFlagSet(values, DirectOverlayFrontBreakFlag));

    private static bool DirectOverlayLifecycleIsWait(byte[] bytes) =>
        LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var message) &&
        message.Content.Contains("You Shoud wait 6 seconds.");

    private sealed class DirectOverlayLifecycleFixture : IAsyncDisposable
    {
        private const uint StrikerUid = PlayerUid + 2;
        private readonly CancellationTokenSource _cancel = new();
        private readonly StringWriter _log = new();
        private readonly WorldServerFrameHandler _handler;
        private readonly WorldSessionStore _sessions;
        private readonly Dictionary<uint, ConquerTcpConnection> _connections = [];
        private readonly WorldPacketHandlerContext _context;
        private readonly ManualRuntimeTimeProvider _clock;
        private bool _started;

        public DirectOverlayLifecycleFixture(bool lethal)
        {
            var start = new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
            _clock = new ManualRuntimeTimeProvider(start);
            var signedClock = LegacySignedMillisecondClock.FromTimeProvider(_clock, 10_000);
            const uint ownerHelmet = 111_060, dummyHelmet = 111_061, strikerHelmet = 111_062;
            const uint ownerArmor = 131_060;
            var ownerHelmetRow = DirectOverlayItem(ownerHelmet, "OverlayLifecycleOwner",
                critical: 1_000, breakthrough: 60, counteraction: 50);
            var ownerHelmetFields = ownerHelmetRow.Fields.ToArray();
            // Source ItemHP is ushort. Normal base100000 is900 attributeHP
            // plus two legal-width50000/49100 itemHP rows, not one99100 row.
            ownerHelmetFields[18] = lethal ? "500" : "50000";
            ownerHelmetRow = ownerHelmetRow with { Fields = ownerHelmetFields };
            var ownerArmorRow = DirectOverlayItem(ownerArmor, "OverlayLifecycleHpArmor");
            var ownerArmorFields = ownerArmorRow.Fields.ToArray();
            ownerArmorFields[18] = lethal ? "0" : "49100";
            ownerArmorRow = ownerArmorRow with { Fields = ownerArmorFields };
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [
                CreateMagicRow(DirectOverlaySpellId, sort: 99, useMana: 0, damage: 30_250, range: 4,
                    damageOnHuman: 0, damage2: 33, damage3: 2, damageOnMonster: 30_085, gDamage: 0,
                    coolDown: 0, coldTime: 0),
                CreateMagicRow(FineRainAttachStatusSpellId, sort: 6, useMana: 0, damage: 0,
                    duration: 60, coolDown: 0, coldTime: 0),
            ], items:
            [
                ownerHelmetRow,
                ownerArmorRow,
                DirectOverlayItem(dummyHelmet, "OverlayLifecycleDummy", counteraction: 65),
                DirectOverlayItem(strikerHelmet, "OverlayLifecycleStriker", critical: 1_000, breakthrough: 100),
                DirectOverlayItem(DirectOverlayFrontBreakRuneId, "Front Break (+9)"),
                DirectOverlayItem(DirectOverlaySyntheticImpregnabilityRuneId, "Impregnability(+9)"),
            ]);
            PlayerItemSnapshotItem Item(uint uid, uint id, ushort slot) => new()
            {
                Uid = uid, ItemId = id, Position = slot, Durability = 100, MaximumDurability = 100,
            };
            var owner = CreateSession(mapId: 1_000, classCode: 2_005, reborn: 2,
                strength: lethal ? 0 : 300, spirit: 0, minHitPoints: lethal ? 500 : 100_000,
                stamina: 100, spellIds: [DirectOverlaySpellId, FineRainAttachStatusSpellId],
                playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = [Item(93_001, ownerHelmet, 1), Item(93_004, ownerArmor, 3)],
                    RuneItems =
                    [
                        Item(93_002, DirectOverlayFrontBreakRuneId, 103),
                        Item(93_003, DirectOverlaySyntheticImpregnabilityRuneId, 104),
                    ],
                },
                attributes: Attributes("200", lethal ? "500" : "100000")) with { CombatGear = Gear() };
            var dummy = CreateSession(uid: TargetPlayerUid, name: "OverlayLifecycleDummy", mapId: 1_000,
                x: 839, y: 584, classCode: 2_005, strength: 0, spirit: 0, minHitPoints: 100_000,
                spellIds: [], playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = [Item(94_001, dummyHelmet, 1)] },
                attributes: Attributes("300", "100000"));
            var striker = CreateSession(uid: StrikerUid, name: "OverlayLifecycleStriker", mapId: 1_000,
                x: 830, y: 593, classCode: 2_005, strength: 300, spirit: 0, minHitPoints: 100_000,
                stamina: 100, spellIds: [DirectOverlaySpellId],
                playerItems: PlayerItemSnapshot.Empty with { EquipmentItems = [Item(95_001, strikerHelmet, 1)] },
                attributes: Attributes("100", "100000")) with { CombatGear = Gear() };
            var observer = CreateSession(uid: PlayerUid + 50, name: "OverlayLifecycleObserver", mapId: 1_000,
                x: 835, y: 589, strength: 0, spirit: 0, minHitPoints: 100_000, spellIds: []);
            var initial = new Dictionary<uint, WorldSession>
            {
                [PlayerUid] = owner, [TargetPlayerUid] = dummy, [StrikerUid] = striker, [PlayerUid + 50] = observer,
            };
            foreach (var uid in initial.Keys)
            {
                Frames[uid] = new ConcurrentQueue<byte[]>();
                _connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Frames[uid].Enqueue(bytes);
                        Order.Enqueue((uid, bytes));
                        return ValueTask.CompletedTask;
                    });
            }
            _handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData: world,
                runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromSeconds(1), _clock, signedClock),
                log: TextWriter.Synchronized(_log), protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            _sessions = GetHandlerSessions(_handler);
            foreach (var (uid, session) in initial) _sessions.Set(_connections[uid], session);
            _context = new WorldPacketHandlerContext(_connections[PlayerUid], TextWriter.Synchronized(_log), owner,
                WorldData: world, Sessions: _sessions, MonsterStates: new MonsterRuntimeStateStore(),
                SobNpcStates: new SobNpcRuntimeStateStore(), TimeProvider: _clock, LegacyClock: signedClock,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            var rawOwner = LegacyPlayerStatusCalculator.CalculateFields(owner.Character, owner.PlayerItems, world,
                lethal ? 500U : 100_000U, 0);
            var rawHitpoints = LegacyMaxHitpointsCalculator.Calculate(owner.Character, owner.PlayerItems,
                owner.CombatGear ?? CombatGearSnapshot.Empty, world);
            Assert.Equal(lethal ? 500U : 100_000U, rawHitpoints.Total);
            Assert.Equal(lethal ? 0U : 300U, rawOwner[LegacyPlayerStatusPacket.MinAttackIndex]);
            Assert.Equal(1_000U, rawOwner[LegacyPlayerStatusPacket.CriticalStrikeIndex]);
            Assert.Equal(60U, rawOwner[LegacyPlayerStatusPacket.BreakthroughIndex]);
            Assert.Equal(50U, rawOwner[LegacyPlayerStatusPacket.CounteractionIndex]);
            Assert.Equal(0U, rawOwner[LegacyPlayerStatusPacket.DefenceIndex]);
        }

        public Dictionary<uint, ConcurrentQueue<byte[]>> Frames { get; } = [];
        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Owner => AssertHandlerSession(_handler, _connections[PlayerUid]);
        public int WaitCount => Frames[PlayerUid].Count(DirectOverlayLifecycleIsWait);

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

        public async Task<LegacyPlayerStatusPacket> QueryAsync()
        {
            var start = Frames[PlayerUid].Count;
            await PacketHandlers.HandleAsync(_context with { Session = Owner },
                ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
            var bytes = Assert.Single(Frames[PlayerUid].ToArray().Skip(start),
                frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            return status;
        }

        public async Task EarnFrontAsync()
        {
            await AttackAsync(PlayerUid, TargetPlayerUid);
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, DirectOverlayFrontBreakFlag));
            Assert.Equal(_clock.GetUtcNow().AddSeconds(10),
                LegacyPhysicalStatusOverlay.ForOwner(Owner).FrontBreakExpiresAtUtc);
            Assert.Equal(66U, (await QueryAsync()).Breakthrough);
        }

        public async Task<uint> StrikeOwnerAsync()
        {
            var start = Frames[StrikerUid].Count;
            await AttackAsync(StrikerUid, PlayerUid);
            var animation = Assert.Single(ParseSpellAnimations(Frames[StrikerUid].ToArray().Skip(start).ToArray()),
                packet => packet.SpellId == DirectOverlaySpellId);
            var hit = Assert.Single(animation.Targets);
            Assert.Equal(PlayerUid, hit.Uid);
            return hit.Damage;
        }

        public async Task CastFineRainAsync()
        {
            await PacketHandlers.HandleAsync(_context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: PlayerUid,
                    X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                    SpellId: FineRainAttachStatusSpellId)).ToFrameBytes()));
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, FineRain1StatusFlag));
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, FineRain2StatusFlag));
            // Synthetic60s row: raw0 normalizes to source power100, not zero.
            // Both registrations remain later than earned313's10s deadline.
            var expected = _clock.GetUtcNow().AddSeconds(60).ToString("O");
            Assert.Equal(expected, Owner.Character.Attributes["LegacyFineRain1ExpiresAtUtc"]);
            Assert.Equal(expected, Owner.Character.Attributes["LegacyFineRain2ExpiresAtUtc"]);
        }

        public async Task QueryEquipmentThroughRuneStorageAsync()
        {
            await PacketHandlers.HandleAsync(_context with { Session = Owner },
                ReadFrame(LegacyRuneStoragePacket.Startup().ToFrameBytes()));
        }

        private async Task AttackAsync(uint attackerUid, uint targetUid)
        {
            var session = AssertHandlerSession(_handler, _connections[attackerUid]);
            await PacketHandlers.HandleAsync(_context with { Connection = _connections[attackerUid], Session = session },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: attackerUid, OpponentUid: targetUid,
                    X: 120, Y: 121, AttackType: MsgAttackType.Physical, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0)).ToFrameBytes()));
        }

        public async ValueTask DisposeAsync()
        {
            await _cancel.CancelAsync();
            if (_started)
            {
                var globalStarted = typeof(WorldServerFrameHandler).GetField("_legacyRuntimeGlobalLoopStarted",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(globalStarted);
                await WaitUntilAsync(() => _clock.ScheduledTimerCount == 0 &&
                    Equals(0, globalStarted.GetValue(_handler)), TimeSpan.FromSeconds(3));
            }
            GC.KeepAlive(_connections);
            _cancel.Dispose();
            _log.Dispose();
        }

        private static Dictionary<string, string> Attributes(string battlePower, string hp) => new()
        {
            ["PkMode"] = "0", ["MaxHitpoints"] = hp, ["BattlePower"] = battlePower,
            ["LegacyCombatGearAttackSpellIndex"] = "3", ["LegacyCombatGearAttackRateRoll"] = "99",
            ["LegacyPhysicalRefineryRoll"] = "0", ["LegacyPhysicalPlayerLuckyBranchRoll"] = "99",
            ["LegacyPhysicalPlayerCriticalRoll"] = "99", ["LegacyPhysicalDashRoll"] = "0", ["DashRate"] = "0",
            ["LegacyPhysicalBreakthroughRefineryRoll"] = "0", ["LegacyPhysicalImpregnabilityRoll"] = "0",
        };

        private static CombatGearSnapshot Gear() => CombatGearSnapshot.Empty with
        {
            Count = 1, Items = [new CombatGearSnapshotItem(201, Level: 1, Exp: 0, Equip: 1,
                Transform: 0, MasteryScore: 0, Animas: [], BlackJades: [])],
        };
    }
}
