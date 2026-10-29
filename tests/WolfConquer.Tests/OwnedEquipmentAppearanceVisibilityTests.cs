using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedEquipmentAppearanceVisibilityTests
{
    [Fact]
    public async Task HandlerItemUsageEquipVisibleGearRefreshesNearbyPeerSpawnOnly()
    {
        OwnedFixture? fixture = null;
        Exception? primary = null;
        try
        {
            fixture = new OwnedFixture();
            fixture.Initialize();
            await fixture.LoginAll();
            var actorCount = fixture.ActorFrames.Count;
            var nearCount = fixture.NearFrames.Count;
            var farCount = fixture.FarFrames.Count;
            var before = File.ReadAllBytes(fixture.ItemPath);
            var expected = before.ToArray();
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(4 + 12, 2), 1);
            fixture.BeforeActorFrame = () => Assert.Equal(expected, File.ReadAllBytes(fixture.ItemPath));
            await fixture.SendEquip();
            fixture.BeforeActorFrame = null;
            Assert.Equal(actorCount + 13, fixture.ActorFrames.Count);
            Assert.Equal(nearCount + 1, fixture.NearFrames.Count);
            Assert.Equal(farCount, fixture.FarFrames.Count);
            var sourceQuery = LegacySelectedEquipmentQueryAssertions.NeutralQuery(fixture.ActorFrames, actorCount + 3, OwnedFixture.Actor);
            Assert.Equal(LegacyShowEquipmentPacket.ShowAction, sourceQuery.Equipment.Action);
            Assert.Equal(0U, sourceQuery.Equipment.Alternante);
            Assert.Equal(2001U, sourceQuery.Equipment.EquipmentSlots.Head);
            Assert.True(LegacyPlayerSpawnPacket.TryParse(Frame(fixture.NearFrames[^1]), out var spawn));
            Assert.Equal((ulong)OwnedFixture.Actor, spawn.GetNumericField(2));
            Assert.Equal(111_003UL, spawn.GetNumericField(8));
            Assert.Equal(800_001UL, spawn.GetNumericField(64));
            Assert.Equal(expected, File.ReadAllBytes(fixture.ItemPath));
            var saved = new PlayerItemSnapshotLoader(fixture.Data.Items).LoadByCharacterUid(OwnedFixture.Actor);
            Assert.Equal(2001U, Assert.Single(saved.EquipmentItems).Uid);
            Assert.Empty(saved.InventoryItems);
            Assert.False(LegacyTradeJournal.HasPending(fixture.Data.Users));
        }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (fixture is not null)
            try { fixture.RetainEvidence = primary is not null; await fixture.DisposeAsync(); }
            catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed class OwnedFixture : IAsyncDisposable
    {
        internal const uint Actor = LegacyOwnedAdmissionRuntimeFixture.Uid, Near = Actor + 1, Far = Actor + 2;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        private readonly NativeLoginBeastMetadataScope beasts = new(1000);
        private readonly StringWriter log = new();
        internal readonly List<byte[]> ActorFrames = [], NearFrames = [], FarFrames = [];
        private readonly List<ConquerTcpConnection> connections = [];
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal WorldServerFrameHandler Handler = null!;
        private ConquerTcpConnection actor = null!, near = null!, far = null!;
        internal bool RetainEvidence;
        internal Action? BeforeActorFrame;
        internal string ItemPath => Path.Combine(Data.Items, Actor.ToString(CultureInfo.InvariantCulture) + ".bin");

        internal void Initialize()
        {
            var original = File.ReadAllText(Data.UserPath);
            WritePeer(original, Near, "Nearby", 440, 386);
            WritePeer(original, Far, "Faraway", 530, 380);
            var item = new PlayerItemSnapshotItem { Uid = 2001, ItemId = 111003, Position = 0,
                Durability = 100, MaximumDurability = 100, StackSize = 1, PurificationItemId = 800001 };
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Actor, new([item], [], [], [], []));
            Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime);
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = "111003"; fields[1] = "OwnedAppearanceHelmet";
            fields[ItemType.DurabilityFieldIndex] = "100"; fields[ItemType.StackSizeFieldIndex] = "1";
            var catalog = LegacyItemWorldDataFixture.Create(new[] { new ItemType(111003, fields[1], fields) }, []);
            Handler = new WorldServerFrameHandler(Data.Loader, null, catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
                log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            actor = Connection(ActorFrames); near = Connection(NearFrames); far = Connection(FarFrames);
        }

        private void WritePeer(string original, uint uid, string name, int x, int y)
        {
            var text = original.Replace("UID=" + Actor, "UID=" + uid, StringComparison.Ordinal)
                .Replace("Name=OwnedStartup", "Name=" + name, StringComparison.Ordinal)
                .Replace("X=430", "X=" + x, StringComparison.Ordinal)
                .Replace("Y=380", "Y=" + y, StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(Data.Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini"), text);
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid, PlayerItemSnapshot.Empty);
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(uid, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(uid, PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(uid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(uid, CombatGearSnapshot.Empty);
        }
        private ConquerTcpConnection Connection(List<byte[]> frames)
        {
            var native = new LegacyNativeGameClientRuntime();
            var closed = new CancellationTokenSource();
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (bytes, token) => { token.ThrowIfCancellationRequested(); if (ReferenceEquals(frames, ActorFrames)) BeforeActorFrame?.Invoke(); frames.Add(bytes.ToArray()); return ValueTask.CompletedTask; },
                _ => { closed.Cancel(); return ValueTask.CompletedTask; }, native);
            connections.Add(connection);
            cancellation.Add(closed);
            return connection;
        }
        private readonly List<CancellationTokenSource> cancellation = [];
        internal async Task LoginAll()
        {
            var store = Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
            foreach (var pair in new[] { (Connection: actor, Uid: Actor), (Connection: near, Uid: Near), (Connection: far, Uid: Far) })
            {
                await Handler.HandleFrameAsync(pair.Connection, Frame(new LegacyMsgLoginGamePacket(pair.Uid).ToFrameBytes()));
                Assert.True(store.TryGet(pair.Connection, out var current));
                Assert.NotNull(current);
                Assert.Equal(pair.Uid, current.Character.Uid);
                Assert.Same(pair.Connection.NativeGameClient, current.NativeGameClient);
                Assert.NotNull(pair.Connection.NativeGameClient!.OrdinarySpellBinding);
                Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(pair.Connection, current));
            }
        }
        internal async Task SendEquip() => await Handler.HandleFrameAsync(actor, Frame(new MsgItemUsagePacket(
            Id: 2001, DwParam: 1, Action: MsgItemUsageAction.Equip, Timestamp: 1234,
            DwParam2: 0, DwParam3: 0, DwParam4: 0).ToFrameBytes()));
        public async ValueTask DisposeAsync()
        {
            BeforeActorFrame = null;
            var errors = new List<Exception>();
            foreach (var c in connections)
            {
                try { await c.DisconnectAsync(); await c.CompleteTransportCloseAsync(); }
                catch (Exception error) { errors.Add(error); }
            }
            try { if (Runtime is not null) await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var c in cancellation) try { c.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { beasts.Dispose(); log.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 0 && !RetainEvidence) Data.Dispose();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }
}
