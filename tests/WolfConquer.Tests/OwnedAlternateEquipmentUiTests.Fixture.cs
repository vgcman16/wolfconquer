using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedAlternateEquipmentUiTests
{
    private static async Task WithFixtureAsync(SourceAlternateUiFixture fixture, Func<Task> test)
    {
        Exception? primary = null;
        try { await test(); fixture.RetainEvidence = false; } catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        try { await fixture.DisposeAsync(); } catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }
    private sealed class SourceAlternateUiClock : ILegacySignedMillisecondClock
    {
        internal int Reads { get; private set; }
        public int GetMilliseconds() { Reads++; return 10_001; }
        internal void Reset() => Reads = 0;
    }
    private sealed class SourceAlternateUiFixture : IAsyncDisposable
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        private readonly NativeLoginBeastMetadataScope beasts = new(1000);
        private readonly StringWriter log = new();
        internal readonly ConcurrentQueue<byte[]> OwnerFrames = new(), PeerFrames = new(), ReplacementFrames = new();
        internal readonly SourceAlternateUiClock Clock = new();
        private readonly List<ConquerTcpConnection> connections = [];
        private readonly List<CancellationTokenSource> closes = [];
        internal WorldServerFrameHandler Handler = null!;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal ConquerTcpConnection Connection = null!, Peer = null!;
        internal bool RetainEvidence = true;
        private byte[]? expectedEquipmentImage;
        private Exception? ownerSendFailure;
        internal Func<SourceAlternateUiFixture, byte[], ValueTask>? OnOwnerSend;
        internal WorldSessionStore Sessions => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        internal WorldSession Owner => Assert.IsType<WorldSession>(Sessions.TryGet(Connection, out var value) ? value : null);
        internal uint OwnedMaximum { get { Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(Owner, out var value)); return value; } }
        internal static async Task<SourceAlternateUiFixture> CreateAsync(bool active, bool guarded = false, Func<SourceAlternateUiFixture, byte[], ValueTask>? onOwnerSend = null)
        {
            var fixture = new SourceAlternateUiFixture();
            try { fixture.Initialize(); await fixture.Login(active, guarded); fixture.OnOwnerSend = onOwnerSend; return fixture; }
            catch (Exception primary)
            {
                fixture.RetainEvidence = true;
                try { await fixture.DisposeAsync(); } catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
                ExceptionDispatchInfo.Capture(primary).Throw(); throw;
            }
        }
        private void Initialize()
        {
            var text = File.ReadAllText(Data.UserPath).Replace("Class=7051", "Class=2005", StringComparison.Ordinal)
                .Replace("MinHitPoints=330", "MinHitPoints=300", StringComparison.Ordinal)
                .Replace("Strength=10", "Strength=0", StringComparison.Ordinal)
                .Replace("Agility=10", "Agility=0", StringComparison.Ordinal)
                .Replace("Vitaliti=10", "Vitaliti=0", StringComparison.Ordinal)
                .Replace("Spirit=10", "Spirit=0", StringComparison.Ordinal);
            File.WriteAllText(Data.UserPath, text);
            var peerUid = PlayerUid + 1;
            var peerText = text.Replace("UID=" + PlayerUid, "UID=" + peerUid, StringComparison.Ordinal)
                .Replace("Name=OwnedStartup", "Name=AlternateUiPeer", StringComparison.Ordinal)
                .Replace("X=430", "X=432", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(Data.Users, peerUid + ".ini"), peerText);
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(peerUid, PlayerItemSnapshot.Empty);
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(peerUid, PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(peerUid, PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(peerUid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(peerUid, CombatGearSnapshot.Empty);
            var oldItem = ItemRow(92_601, 111_096, 1);
            var newItem = ItemRow(92_602, 111_097, 0);
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(PlayerUid, new([newItem], [], [], [oldItem], []));
            Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            var catalog = LegacyItemWorldDataFixture.Create(new[] { Helmet(111_096, 500), Helmet(111_097, 700) }, []);
            var options = LegacyRuntimeUpdateLoopOptions.Disabled with { LegacyClock = Clock };
            Handler = new WorldServerFrameHandler(Data.Loader, null, catalog, options, log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            Connection = CreateConnection(OwnerFrames); Peer = CreateConnection(PeerFrames);
        }
        private static PlayerItemSnapshotItem ItemRow(uint uid, uint id, ushort position) => new()
        { Uid = uid, ItemId = id, Position = position, Durability = 100, MaximumDurability = 100 };
        private static ItemType Helmet(uint id, uint maximum)
        {
            var fields = Enumerable.Repeat("0", 66).ToArray();
            fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "AlternateHelmet" + maximum;
            fields[18] = maximum.ToString(CultureInfo.InvariantCulture);
            fields[ItemType.DurabilityFieldIndex] = "100"; fields[ItemType.StackSizeFieldIndex] = "1";
            return new ItemType(checked((int)id), fields[1], fields);
        }
        private ConquerTcpConnection CreateConnection(ConcurrentQueue<byte[]> frames)
        {
            var closed = new CancellationTokenSource(); closes.Add(closed);
            var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                async (bytes, token) =>
                {
                    token.ThrowIfCancellationRequested();
                    var copy = bytes.ToArray();
                    try
                    {
                        if (ReferenceEquals(frames, OwnerFrames) && expectedEquipmentImage is { } expected)
                            Assert.Equal(expected, File.ReadAllBytes(Path.Combine(Data.Items, PlayerUid + ".bin")));
                        frames.Enqueue(copy);
                        if (ReferenceEquals(frames, OwnerFrames) && OnOwnerSend is { } observe)
                            await observe(this, copy);
                    }
                    catch (Exception failure) { ownerSendFailure ??= failure; throw; }
                },
                _ => { closed.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime());
            connections.Add(connection); return connection;
        }
        private async Task Login(bool active, bool guarded)
        {
            foreach (var pair in new[] { (Connection, Uid: PlayerUid), (Peer, Uid: PlayerUid + 1) })
            {
                await Handler.HandleFrameAsync(pair.Item1, ReadFrame(new LegacyMsgLoginGamePacket(pair.Uid).ToFrameBytes()));
                Assert.True(Sessions.TryGet(pair.Item1, out var current)); Assert.NotNull(current);
                Assert.Same(pair.Item1.NativeGameClient, current.NativeGameClient);
                Assert.NotNull(pair.Item1.NativeGameClient!.OrdinarySpellBinding);
                Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(pair.Item1, current));
            }
            if (active) await Handler.HandleFrameAsync(Connection, ReadFrame(new MsgItemUsagePacket(0, 0,
                MsgItemUsageAction.Alternante, 0, 0, 0, 0).ToFrameBytes()));
            Assert.Equal(active, Owner.AlternateEquipmentActive);
            if (guarded)
            {
                // Controlled transient state, as in the original query-order
                // test. The actor, native binding, PIN and persistence owner are
                // genuinely admitted; this does not claim a FineRain/Imp cast.
                var before = Owner;
                var native = Connection.NativeGameClient!;
                var binding = native.OrdinarySpellBinding;
                var words = new ulong[16]; words[212 / 32] |= 1UL << (212 % 32);
                var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["StatusFlags"] = string.Join('/', words), ["FineRainPower"] = "100" };
                Sessions.Set(Connection, before with
                {
                    Character = before.Character with { Attributes = attributes },
                    PhysicalStatusOverlay = new LegacyPhysicalStatusOverlay(PlayerUid, 60, 75),
                    EquipmentHitpointsStatus = new LegacyEquipmentHitpointsStatus(PlayerUid, 600),
                    FineRainEquipmentAdmission = new LegacyFineRainEquipmentAdmission(PlayerUid, 10_000),
                });
                Assert.Same(native, Owner.NativeGameClient);
                Assert.Same(binding, native.OrdinarySpellBinding);
                Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(Connection, Owner));
                Assert.True(Assert.IsType<LegacyTradeRecoveryState>(typeof(WorldServerFrameHandler)
                    .GetField("_tradeRecovery", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler))
                    .IsReadyFor(Data.Users, Data.Items));
            }
            OwnerFrames.Clear(); PeerFrames.Clear(); Clock.Reset();
        }
        internal async Task EquipAsync()
        {
            var path = Path.Combine(Data.Items, PlayerUid + ".bin");
            var expected = File.ReadAllBytes(path);
            var count = BinaryPrimitives.ReadInt32LittleEndian(expected);
            var found = 0;
            for (var index = 0; index < count; index++)
            {
                var offset = 4 + index * PlayerItemSnapshotLoader.LegacyDbItemSize;
                if (BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset, 4)) != 92_602) continue;
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset + 12, 2), 51); found++;
            }
            Assert.Equal(1, found);
            expectedEquipmentImage = expected;
            try
            {
                await Handler.HandleFrameAsync(Connection, ReadFrame(new MsgItemUsagePacket(92_602, 51,
                    MsgItemUsageAction.Equip, 0, 0, 0, 0).ToFrameBytes()));
                if (ownerSendFailure is { } failure) ExceptionDispatchInfo.Capture(failure).Throw();
                Assert.Equal(Assert.IsType<byte[]>(expectedEquipmentImage), File.ReadAllBytes(path));
            }
            finally { expectedEquipmentImage = null; }
        }
        internal WorldSession SetAcceptedEquipmentDurability(ushort durability)
        {
            var before = Owner;
            var item = Assert.Single(before.PlayerItems.EquipmentItems, row => row.Uid == 92_602);
            Assert.Equal((ushort)51, item.Position);
            Assert.NotEqual(item.Durability, durability);
            var proposedItems = before.PlayerItems with
            {
                EquipmentItems = before.PlayerItems.EquipmentItems.Select(row =>
                    row.Uid == item.Uid ? row.WithDurability(durability) : row).ToArray(),
            };
            // The equipment transaction preserves the original physical root
            // order; generic Set writes the canonical equipment root order.
            // Retain both whole raw records, then change only target durability.
            var expected = CanonicalEquipmentImage(before);
            var count = BinaryPrimitives.ReadInt32LittleEndian(expected);
            var found = 0;
            for (var index = 0; index < count; index++)
            {
                var offset = 4 + index * PlayerItemSnapshotLoader.LegacyDbItemSize;
                if (BinaryPrimitives.ReadUInt32LittleEndian(expected.AsSpan(offset, 4)) != item.Uid) continue;
                Assert.Equal(item.Durability,
                    BinaryPrimitives.ReadUInt16LittleEndian(expected.AsSpan(offset + 8, 2)));
                BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(offset + 8, 2), durability);
                found++;
            }
            Assert.Equal(1, found);
            Assert.Equal(expected, PlayerItemSnapshotLoader.ToLegacyBytes(proposedItems));
            Sessions.Set(Connection, before with { PlayerItems = proposedItems }, out var final);
            Assert.Same(final, Owner);
            Assert.Equal(expected, PlayerItemSnapshotLoader.ToLegacyBytes(final.PlayerItems));
            Assert.Equal(expected, File.ReadAllBytes(Path.Combine(Data.Items, PlayerUid + ".bin")));
            expectedEquipmentImage = expected;
            return final;
        }
        private byte[] CanonicalEquipmentImage(WorldSession before)
        {
            var raw = Assert.IsType<byte[]>(expectedEquipmentImage);
            Assert.Equal(raw, File.ReadAllBytes(Path.Combine(Data.Items, PlayerUid + ".bin")));
            Assert.Equal(new uint[] { 92_601, 92_602 }, before.PlayerItems.EquipmentItems.Select(row => row.Uid));
            Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(raw));
            var recordSize = PlayerItemSnapshotLoader.LegacyDbItemSize;
            Assert.Equal(8 + 2 * recordSize, raw.Length);
            Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(raw.AsSpan(raw.Length - 4)));
            Assert.Equal(92_602U, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4)));
            Assert.Equal(92_601U, BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(4 + recordSize)));
            var canonical = raw.ToArray();
            raw.AsSpan(4 + recordSize, recordSize).CopyTo(canonical.AsSpan(4, recordSize));
            raw.AsSpan(4, recordSize).CopyTo(canonical.AsSpan(4 + recordSize, recordSize));
            Assert.Equal(canonical, PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems));
            return canonical;
        }
        internal async Task QueryStatusAsync() => await Handler.HandleFrameAsync(Connection,
            ReadFrame(new LegacyPlayerStatusQueryPacket(PlayerUid).ToFrameBytes()));
        internal async Task<WorldSession> ReissueAsync()
        {
            Connection = CreateConnection(ReplacementFrames);
            await Handler.HandleFrameAsync(Connection, ReadFrame(new LegacyMsgLoginGamePacket(PlayerUid).ToFrameBytes()));
            Assert.True(Sessions.TryGet(Connection, out var current));
            Assert.NotNull(current);
            Assert.NotNull(Connection.NativeGameClient!.OrdinarySpellBinding);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(Connection, current));
            return current;
        }
        public async ValueTask DisposeAsync()
        {
            OnOwnerSend = null;
            var errors = new List<Exception>();
            foreach (var connection in connections)
                try { await connection.DisconnectAsync(); await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            try { if (Runtime is not null) await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var close in closes) try { close.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { beasts.Dispose(); log.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 0 && !RetainEvidence) Data.Dispose();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }
}
