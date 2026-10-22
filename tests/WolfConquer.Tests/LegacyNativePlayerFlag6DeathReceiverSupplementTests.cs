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
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeFlag6SupplementNormalPlayerDeathRetainsReceiverAcrossDeadTalk(bool originalVisible)
    {
        var model = new Flag6PublicReceiverSupplementModel(originalVisible);
        using var fixture = new Flag6ReceiverCombatFixture(model);
        await fixture.PrimeAsync();
        var start = fixture.Order.Count;
        fixture.Armed = true;

        await fixture.KillByPublicThunderAsync();
        await model.Swapped.WaitAsync(TimeSpan.FromSeconds(2));

        var frames = fixture.TargetFrames(start);
        var hp = AssertExistingNormalDeathCombatPhases(frames, TargetPlayerUid,
            (500U, 300U), (500U, 300U), (500U, 300U), (500U, 0U));
        var talk = SingleFrameIndex(frames, frame => LegacyMsgMessagePacket.TryParse(frame, out var message) &&
            message.Content.Contains(LegacyPlayerDeathState.DeadTalkText));
        Assert.Equal(fixture.SwapAtTargetFrame, talk);
        Assert.Equal(1, fixture.DeadTalkCallbacks);
        var vectors = Enumerable.Range(0, frames.Length).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(frames[index]), out var packet) &&
            packet.Uid == TargetPlayerUid && packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag)).ToArray();
        Assert.Equal(2, vectors.Length);
        Assert.True(talk < vectors[0] && hp[^1] < vectors[1]);
        model.AssertVector(frames[vectors[0]], TargetPlayerUid, dead: false);
        model.AssertVector(frames[vectors[1]], TargetPlayerUid, dead: true);
        var death = Assert.Single(frames.Select(ReadFrame).Select(frame =>
                MsgAttackPacket.TryParse(frame, out var packet) ? (MsgAttackPacket?)packet : null)
            .OfType<MsgAttackPacket>().Where(packet => packet.Query.AttackType == MsgAttackType.Death));
        Assert.Equal(PlayerUid.ToString(System.Globalization.CultureInfo.InvariantCulture),
            fixture.Target.Character.Attributes[LegacyPlayerDeathState.DeadKillerUidAttribute]);
        Assert.Equal(TargetPlayerUid, death.Query.OpponentUid);
        Assert.True(Array.FindIndex(frames, bytes => MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Query.AttackType == MsgAttackType.Death) > vectors[1]);
        Assert.Equal(0, fixture.Target.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(fixture.Target.Character.Attributes, 10));
        model.AssertRetained(fixture.Target);
    }

    // Focused MODEL fixture, derived from the existing public normal-death fixture.
    // No Team, transform or Thundercloud is active: DeadTalk is the first awaited send AFTER
    // Player.Dead receiver capture; earlier upstream magic publications are NOT swap points.
    private sealed class Flag6ReceiverCombatFixture : IDisposable
    {
        private const ushort Thunder = 1_000;
        private readonly WorldSessionStore sessions = new();
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly ManualRuntimeTimeProvider clock = new(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        private readonly StringWriter log = new();
        private readonly WorldPacketHandlerContext context;
        private readonly Flag6PublicReceiverSupplementModel model;
        private int operationStart;
        public bool Armed { get; set; }
        public int DeadTalkCallbacks { get; private set; }
        public int SwapAtTargetFrame { get; private set; } = -1;

        public Flag6ReceiverCombatFixture(Flag6PublicReceiverSupplementModel model)
        {
            this.model = model;
            const uint helmet = 111_097;
            var item = DirectOverlayItem(helmet, "Flag6ReceiverHelmet");
            var fields = item.Fields.ToArray();
            fields[18] = "500";
            fields[19] = "500";
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
                [CreateMagicRow(Thunder, sort: 1, useMana: 0, useStamina: 0, damage: 799,
                    range: 18, coolDown: 0, coldTime: 0)], items: [item with { Fields = fields }]);
            WorldSession Session(uint uid, string name, ushort x) => CreateSession(uid: uid,
                name: name, mapId: 1_000, x: x, y: 584, classCode: 2_005, reborn: 2,
                strength: 0, spirit: 0, minHitPoints: 300, minMana: 500, stamina: 100,
                spellIds: uid == PlayerUid ? [Thunder] : [],
                attributes: new Dictionary<string, string>
                {
                    ["PkMode"] = "0", ["BattlePower"] = "100",
                }, playerItems: PlayerItemSnapshot.Empty with
                {
                    EquipmentItems = [new PlayerItemSnapshotItem
                    {
                        Uid = uid + 100, ItemId = helmet, Position = 1,
                        Durability = 100, MaximumDurability = 100,
                    }],
                });
            var actorClient = new LegacyNativeGameClientRuntime();
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var client = uid == TargetPlayerUid ? model.Client : actorClient;
                connections[uid] = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                    sendAsync: async (memory, _) =>
                    {
                        var bytes = memory.ToArray();
                        Order.Enqueue((uid, bytes));
                        if (Armed && uid == TargetPlayerUid && DirectNormalDeathTalk(bytes))
                        {
                            try
                            {
                                DeadTalkCallbacks++;
                                Assert.Equal(1, DeadTalkCallbacks);
                                Assert.Equal(300, Target.Character.MinHitPoints);
                                Assert.False(Target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
                                Assert.Same(model.Original, Target.NativePlayer);
                                SwapAtTargetFrame = TargetFrames(operationStart).Length - 1;
                                await model.SwapAsync(connections[uid], () => Target);
                            }
                            catch (Exception error) { model.CaptureCallbackFailure(error); throw; }
                        }
                    }, nativeGameClient: client);
                sessions.Set(connections[uid], Session(uid,
                    uid == PlayerUid ? "Flag6ReceiverCaster" : "Flag6ReceiverTarget",
                    uid == PlayerUid ? (ushort)832 : (ushort)830) with { NativeGameClient = client });
            }
            context = new WorldPacketHandlerContext(connections[PlayerUid], TextWriter.Synchronized(log),
                Slot(PlayerUid), WorldData: world, Sessions: sessions, TimeProvider: clock,
                LegacyClock: LegacySignedMillisecondClock.FromTimeProvider(clock, 10_000),
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public ConcurrentQueue<(uint Recipient, byte[] Bytes)> Order { get; } = new();
        public WorldSession Target => Slot(TargetPlayerUid);
        private WorldSession Slot(uint uid)
        {
            Assert.True(sessions.TryGet(connections[uid], out var current));
            return Assert.IsType<WorldSession>(current);
        }
        private WorldPacketHandlerContext Context(uint uid) =>
            context with { Connection = connections[uid], Session = Slot(uid) };
        public byte[][] TargetFrames(int start) => Order.ToArray().Skip(start)
            .Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray();
        public async Task PrimeAsync()
        {
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                await PrepareNativeOrdinaryMagicInputQueryAsync(Context(uid),
                    () => Order.ToArray().Where(frame => frame.Recipient == uid).Select(frame => frame.Bytes).ToArray(),
                    expectedMagicAttack: 1, expectedMaximumHitpoints: 500, expectedMaximumMana: 500);
            Assert.Equal(300, Target.Character.MinHitPoints);
            Assert.Equal(new ulong[18], LegacyPlayerDeathState.CreateStatusFlagVector(Target.Character.Attributes, 18));
            Assert.Same(model.Client, connections[TargetPlayerUid].NativeGameClient);
            Assert.Same(model.Original, Target.NativePlayer);
            operationStart = Order.Count;
        }
        public async Task KillByPublicThunderAsync() => await PacketHandlers.HandleAsync(Context(PlayerUid),
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid, OpponentUid: TargetPlayerUid,
                X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: Thunder)).ToFrameBytes()));
        public void Dispose()
        {
            Assert.Equal(0, clock.ScheduledTimerCount);
            GC.KeepAlive(connections);
            log.Dispose();
        }
    }
}
