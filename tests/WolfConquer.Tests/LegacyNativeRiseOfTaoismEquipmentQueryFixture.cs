using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed class NativeRiseEquipmentFixture : IAsyncDisposable
    {
        private readonly ManualRuntimeTimeProvider clock = new(DateTimeOffset.UtcNow);
        private readonly CancellationTokenSource cancel = new();
        private readonly FineRainExpiryLog log = new();
        private readonly WorldServerFrameHandler handler;
        private readonly WorldSessionStore sessions;
        private readonly ConquerTcpConnection ownerConnection;
        private readonly ConquerTcpConnection peerConnection;
        private readonly WorldPacketHandlerContext context;
        private bool runtimeStarted;
        private DateTimeOffset? producedExpiry;
        private bool expiryProbeArmed;
        private bool expiryProbeCompleted;
        private ExceptionDispatchInfo? expiryProbeFailure;

        public NativeRiseEquipmentFixture()
        {
            ItemType Helmet(uint id, string name, string hp, string magicAttack)
            {
                var fields = Enumerable.Repeat("0", 64).ToArray();
                fields[0] = id.ToString(CultureInfo.InvariantCulture);
                fields[1] = name;
                fields[18] = hp;
                fields[29] = magicAttack;
                return new ItemType(checked((int)id), name, fields);
            }
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(RiseOfTaoismAttachStatusSpellId, sort: 6, useMana: 0,
                    useStamina: 0, damage: 27, duration: 1, coolDown: 0, coldTime: 0),
                 CreateMagicRow(ThunderSpellId, sort: 1, useMana: 0, useStamina: 0,
                    damage: 541, range: 18, coolDown: 0, coldTime: 0)],
                items: [Helmet(111_097, "NativeRiseRawHelmet", "4700", "200"),
                        Helmet(111_096, "NativeRisePeerRawHelmet", "20000", "0")]);
            var owner = CreateSession(mapId: 1_015, classCode: 2_005, strength: 0, spirit: 100,
                minHitPoints: 5_000, minMana: 500, stamina: 100,
                spellIds: [RiseOfTaoismAttachStatusSpellId, ThunderSpellId],
                playerItems: Items(99_297, 111_097),
                attributes: new Dictionary<string, string> { ["PkMode"] = "0" });
            var peer = CreateSession(uid: TargetPlayerUid, name: "NativeRisePeer", mapId: 1_015,
                x: 832, y: 584, classCode: 2_005, strength: 0, spirit: 0,
                minHitPoints: 20_000, minMana: 0, stamina: 100, spellIds: [],
                playerItems: Items(99_296, 111_096),
                attributes: new Dictionary<string, string> { ["PkMode"] = "0" });
            Assert.Empty(owner.PlayerItems.RuneItems);
            Assert.Null(owner.EquipmentHitpointsStatus);
            Assert.Equal(0L, owner.Character.Agility);
            Assert.Equal(0L, owner.Character.Vitality);
            handler = new WorldServerFrameHandler(snapshotLoader: null, handoffService: null, worldData: world,
                runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(true, TimeSpan.FromMilliseconds(20), clock),
                log: log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            sessions = GetHandlerSessions(handler);
            ownerConnection = Connection(OwnerFrames, async bytes =>
            {
                if (!expiryProbeArmed ||
                    !LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) ||
                    !update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                        entry.Values is not null && !IsLegacyStatusFlagSet(entry.Values, LegacyRiseOfTaoismFlag)))
                    return;
                expiryProbeArmed = false;
                try
                {
                    // Actual RemoveFlag vector publication precedes its following
                    // QueryEquipment. No flag/cache/raw input is edited by this callback.
                    Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
                    Assert.Equal("27", Owner.Character.Attributes["RiseofTaoismExtraMDamage"]);
                    Assert.Equal(55_000L, Owner.Character.MinHitPoints);
                    await AssertThunderHitAsync(expectedDamage: 6_921, expectedPeerHitpoints: 13_079);
                    expiryProbeCompleted = true;
                }
                catch (Exception error) { expiryProbeFailure = ExceptionDispatchInfo.Capture(error); }
            });
            peerConnection = Connection(PeerFrames);
            sessions.Set(ownerConnection, owner);
            sessions.Set(peerConnection, peer);
            context = new WorldPacketHandlerContext(ownerConnection, log, owner, WorldData: world,
                Sessions: sessions, TimeProvider: clock,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<byte[]> OwnerFrames { get; } = new();
        public ConcurrentQueue<byte[]> PeerFrames { get; } = new();
        public WorldSession Owner => Current(ownerConnection);
        public WorldSession Peer => Current(peerConnection);
        private WorldSession Current(ConquerTcpConnection connection) =>
            Assert.IsType<WorldSession>(sessions.TryGet(connection, out var current) ? current : null);

        public async Task<int> QueryOwnerAsync()
        {
            var start = OwnerFrames.Count;
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgActionPacket { ObjId = PlayerUid,
                    Type = (MsgActionType)251 }.ToFrameBytes()));
            return start;
        }

        public async Task<int> AcquireRiseAsync()
        {
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
            var start = OwnerFrames.Count;
            var before = DateTimeOffset.UtcNow;
            await CastAsync(RiseOfTaoismAttachStatusSpellId, PlayerUid);
            var after = DateTimeOffset.UtcNow;
            Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, LegacyRiseOfTaoismFlag));
            Assert.Equal("27", Owner.Character.Attributes["RiseofTaoismExtraMDamage"]);
            Assert.Equal("true", Owner.Character.Attributes["LegacyRiseOfTaoismRemoveOnDead"]);
            AssertLegacyXpSkillExpiresAround(Owner.Character.Attributes, "LegacyRiseOfTaoismExpiresAtUtc",
                before, after, expectedDurationSeconds: 1);
            producedExpiry = DateTimeOffset.Parse(Owner.Character.Attributes["LegacyRiseOfTaoismExpiresAtUtc"],
                CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var frames = OwnerFrames.ToArray().Skip(start).ToArray();
            var countdown = Assert.Single(ParseMsgUpdates(frames).SelectMany(packet => packet.Updates),
                entry => entry.Type == LegacyMsgUpdateDataType.FineRain);
            Assert.Equal(227U, countdown.Flag);
            Assert.Equal(1U, countdown.Time);
            Assert.Equal(50_000U, countdown.Damage);
            var animation = Assert.Single(ParseSpellAnimations(frames),
                packet => packet.SpellId == RiseOfTaoismAttachStatusSpellId);
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(PlayerUid, animation.AttackedUid);
            Assert.Equal(0U, Assert.Single(animation.Targets).Damage);
            return start;
        }

        public void AssertQuery(int start, uint expectedMagicAttack, uint expectedMaximum,
            long expectedCurrentHitpoints, (uint Maximum, ulong Hitpoints)[] expectedPairs)
        {
            var frames = OwnerFrames.ToArray().Skip(start).ToArray();
            var bytes = Assert.Single(frames,
                frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out var status));
            Assert.Equal(PlayerUid, status.Uid);
            Assert.Equal(expectedMagicAttack, status.MagicAttack);
            Assert.Equal(expectedMaximum, status.MaxHP);
            Assert.Equal(500U, status.MaxMana);
            Assert.Equal(expectedCurrentHitpoints, Owner.Character.MinHitPoints);
            Assert.Equal(500L, Owner.Character.MinMana);
            Assert.Equal(expectedMaximum, Owner.EquipmentHitpointsStatus!.MaximumHitpoints);
            var pairs = ParseMsgUpdates(frames).Where(packet => packet.Uid == PlayerUid &&
                packet.Updates.Count == 2 &&
                packet.Updates[0].Type == LegacyMsgUpdateDataType.MaxHitpoints &&
                packet.Updates[1].Type == LegacyMsgUpdateDataType.Hitpoints)
                .Select(packet => (checked((uint)packet.Updates[0].Value), packet.Updates[1].Value)).ToArray();
            Assert.Equal(expectedPairs, pairs);
        }

        public async Task<int> ExpireProducedRiseAsync()
        {
            var expiry = producedExpiry ?? throw new InvalidOperationException("Rise must be genuinely acquired first.");
            Assert.False(runtimeStarted);
            var start = OwnerFrames.Count;
            expiryProbeArmed = true;
            // Established test bridge into the ACTUAL global runtime owner. It
            // does not invoke the private expiry method or manufacture its result.
            FineRainExpiryStartGlobal(handler, cancel.Token);
            runtimeStarted = true;
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1, TimeSpan.FromSeconds(2));
            var due = expiry.AddMilliseconds(20);
            Assert.True(due > clock.GetUtcNow());
            clock.Advance(due - clock.GetUtcNow());
            await WaitUntilAsync(() => clock.ScheduledTimerCount >= 1 &&
                !HasLegacyStatusFlag(Owner.Character.Attributes, LegacyRiseOfTaoismFlag),
                TimeSpan.FromSeconds(2));
            var frames = OwnerFrames.ToArray().Skip(start).ToArray();
            Assert.Contains(ParseMsgUpdates(frames), packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null && !IsLegacyStatusFlagSet(entry.Values, LegacyRiseOfTaoismFlag)));
            expiryProbeFailure?.Throw();
            Assert.True(expiryProbeCompleted);
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyRiseOfTaoismExpiresAtUtc"));
            Assert.False(Owner.Character.Attributes.ContainsKey("LegacyRiseOfTaoismRemoveOnDead"));
            Assert.Contains("WorldServer expired legacy RiseofTaoism flags", log.ToString());
            return start;
        }

        public async Task StopRuntimeAndAdvanceManualCastCadenceAsync()
        {
            await cancel.CancelAsync();
            await WaitUntilAsync(() => clock.ScheduledTimerCount == 0 && FineRainExpiryGlobalStopped(handler),
                TimeSpan.FromSeconds(3));
            clock.Advance(TimeSpan.FromMilliseconds(500));
        }

        public async Task AssertThunderHitAsync(uint expectedDamage, long expectedPeerHitpoints)
        {
            var ownerStart = OwnerFrames.Count;
            var peerStart = PeerFrames.Count;
            await CastAsync(ThunderSpellId, TargetPlayerUid);
            Assert.Equal(expectedPeerHitpoints, Peer.Character.MinHitPoints);
            foreach (var frames in new[] { OwnerFrames.ToArray().Skip(ownerStart),
                                          PeerFrames.ToArray().Skip(peerStart) })
            {
                var animation = Assert.Single(ParseSpellAnimations(frames),
                    packet => packet.SpellId == ThunderSpellId);
                Assert.Equal(PlayerUid, animation.AttackerUid);
                var hit = Assert.Single(animation.Targets);
                Assert.Equal(TargetPlayerUid, hit.Uid);
                Assert.Equal(expectedDamage, hit.Damage);
                Assert.True(hit.Hit);
                Assert.Equal(MsgAttackEffect.None, hit.Effect);
            }
        }

        private Task CastAsync(ushort spellId, uint target) =>
            PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid, OpponentUid: target, X: 832, Y: 584,
                    AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                    SpellId: spellId)).ToFrameBytes())).AsTask();

        private static PlayerItemSnapshot Items(uint uid, uint id) => PlayerItemSnapshot.Empty with
        {
            EquipmentItems = [new PlayerItemSnapshotItem
            {
                Uid = uid, ItemId = id, Position = 1, Durability = 100, MaximumDurability = 100,
            }],
        };
        private static ConquerTcpConnection Connection(ConcurrentQueue<byte[]> frames,
            Func<byte[], ValueTask>? onSend = null) =>
            new(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: async (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    frames.Enqueue(bytes);
                    if (onSend is not null) await onSend(bytes);
                });

        public async ValueTask DisposeAsync()
        {
            await cancel.CancelAsync();
            if (runtimeStarted)
                await WaitUntilAsync(() => clock.ScheduledTimerCount == 0 && FineRainExpiryGlobalStopped(handler),
                    TimeSpan.FromSeconds(3));
            GC.KeepAlive(ownerConnection);
            GC.KeepAlive(peerConnection);
            cancel.Dispose();
            log.Dispose();
        }
    }
}
