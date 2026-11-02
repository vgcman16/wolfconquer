using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedRelicDecomposeTests
{
    internal static async Task Run(Func<DecomposeFixture, Task> body, string input = "valid", int count = 1,
        bool bound = false, bool ranking = false, bool distantPeer = false)
    {
        var fixture = new DecomposeFixture(); var errors = new List<Exception>();
        try { await fixture.InitializeAsync(input, count, bound, ranking, distantPeer); await body(fixture); }
        catch (Exception error) { errors.Add(error); }
        try { await fixture.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { fixture.RestoreMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { fixture.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    internal sealed class DecomposeFixture
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid, PeerUid = Uid + 1;
        internal const uint First = 1_045_001, Kept = 1_045_100, Crystal = 3_306_885;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly List<byte[]> Sent = [], PeerSent = [];
        private readonly StringWriter buffer = new();
        private readonly TextWriter log;
        internal WorldDataCatalog Catalog = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal RootLoopbackFramePeer? Peer, OtherPeer;
        internal Action<byte[]>? BeforeSend;
        internal Action<byte[]>? BeforePeerSend;
        private ConquerTcpConnection? connection, peerConnection;
        private CancellationTokenSource? lifetime;
        private NativeLoginBeastMetadataScope? beasts;
        private Exception? observerFailure;
        private bool withRanking;
        internal uint[] Selected = [];
        internal DecomposeFixture() { log = TextWriter.Synchronized(buffer); }
        internal string ReadDiagnostics() { lock (log) return buffer.ToString(); }
        internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string PeerItemsPath => Path.Combine(Data.Items, PeerUid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal uint HighWater => JsonNode.Parse(File.ReadAllBytes(Data.State))!["HighWater"]!.GetValue<uint>();
        internal ConquerTcpConnection ActualConnection => connection!;
        internal CancellationToken ActualToken => lifetime!.Token;
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Handler));
        internal WorldSession Current => CurrentFor(connection!);
        internal WorldSession PeerCurrent => CurrentFor(peerConnection!);
        internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;
        private WorldSession CurrentFor(ConquerTcpConnection actual)
        {
            Assert.True(Store.TryGet(actual, out var current), "Owned decompose admission unavailable:\n" + ReadDiagnostics());
            return Assert.IsType<WorldSession>(current);
        }

        internal async Task InitializeAsync(string input, int count, bool bound, bool ranking, bool distantPeer)
        {
            Assert.InRange(count, 1, 4); withRanking = ranking;
            Selected = Enumerable.Range(0, count).Select(index => First + (uint)index).ToArray();
            var selected = Selected.Select((uid, index) => Item(uid, 4_100_001U + (uint)index).WithBound(bound ? (byte)1 : (byte)0)).ToArray();
            if (input == "mixedBound") selected[1] = selected[1].WithBound(1);
            if (input == "locked") selected[0] = selected[0].WithLocked(1);
            if (input == "stacked") selected[0] = selected[0].WithStackSize(2);
            if (input == "legacy-zero") selected[0] = selected[0].WithStackSize(0);
            if (input == "suspicious") selected[0] = new PlayerItemSnapshotItem
            { Uid = selected[0].Uid, ItemId = selected[0].ItemId, StackSize = 1, Durability = 10_000, MaximumDurability = 10_000, Suspicious = 1 };
            if (input == "not-relic") selected[0] = selected[0].WithItemId(1_000_000);
            if (input == "child") selected[0] = selected[0].WithDepositedItems([Item(Kept + 100, 1_000_000)]);
            var inventory = selected.Append(Item(Kept, 1_000_000)).ToList();
            if (input == "full") inventory.AddRange(Enumerable.Range(0, 40 - inventory.Count)
                .Select(index => Item(Kept + 200U + (uint)index, 1_000_000)));
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, PlayerItemSnapshot.Empty with { InventoryItems = inventory.ToArray() });
            var types = new uint[] { 4_100_001, 4_100_002, 4_100_003, 4_100_004, 1_000_000, Crystal }
                .Where(id => input != "noMetadata" || id != Crystal)
                .Select(id => LegacyFullAccountItemMutationFixture.ItemType(id, "OwnedDecompose" + id,
                    durability: (ushort)(id == Crystal ? 1 : 10_000))).ToArray();
            RuneRankingRecord[] seed = ranking
                ? [Rank(Uid, "OwnedStartup", 20_000), Rank(PeerUid, "OwnedPeer", 10_000)] : [];
            Catalog = NativeCallerWorldDataFixture.Create(types, [], runeRankings: seed);
            if (ranking)
            {
                var text = File.ReadAllText(Data.UserPath).Replace("UID=" + Uid + "\r\n", "UID=" + PeerUid + "\r\n", StringComparison.Ordinal)
                    .Replace("Name=OwnedStartup", "Name=OwnedPeer", StringComparison.Ordinal);
                if (distantPeer)
                {
                    // The real independent actor starts outside the blocked player's
                    // visibility; RuneStorage's real equipment query can then finish
                    // without targeting that stalled recipient for a spawn frame.
                    Assert.Contains("X=430\r\nY=380\r\n", text, StringComparison.Ordinal);
                    text = text.Replace("X=430\r\nY=380\r\n", "X=100\r\nY=100\r\n", StringComparison.Ordinal);
                }
                File.WriteAllText(Path.Combine(Data.Users, PeerUid + ".ini"), text);
                new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(PeerUid, PlayerItemSnapshot.Empty);
                new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(PeerUid, PlayerSpellSnapshot.Empty);
                new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(PeerUid, PlayerProficiencySnapshot.Empty);
                new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(PeerUid, PlayerQuestSnapshot.Empty);
                new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(PeerUid, CombatGearSnapshot.Empty);
            }
            beasts = new(1000); Data.Bootstrap(); await ReopenFrameAsync();
        }
        private static RuneRankingRecord Rank(uint uid, string name, int score) =>
            new(name, score, checked((int)uid), "fixture://owned-decompose-rank", 1, name + " " + score + " " + uid,
                [name, score.ToString(CultureInfo.InvariantCulture), uid.ToString(CultureInfo.InvariantCulture)]);
        internal static PlayerItemSnapshotItem Item(uint uid, uint type) => new()
        { Uid = uid, ItemId = type, StackSize = 1, Durability = 10_000, MaximumDurability = 10_000,
            Attribute1 = 100_003, Attribute2 = 110_004, Attribute3 = 120_005, Attribute4 = 130_009, Attribute5 = 100_114 };
        internal async Task ReopenFrameAsync()
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime); observerFailure = null;
            Handler = new(Data.Loader, null, Catalog, LegacyRuntimeUpdateLoopOptions.Disabled, log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            lifetime = new(); var native = new LegacyNativeGameClientRuntime();
            Peer = new RootLoopbackFramePeer(bytes =>
            {
                try { BeforeSend?.Invoke(bytes); } catch (Exception error) { observerFailure ??= error; throw; }
                Sent.Add(bytes); return ValueTask.CompletedTask;
            }, native);
            connection = Peer.Connection;
            await SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes());
            Assert.Same(native, Native); Assert.NotNull(native.OrdinarySpellBinding);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, Current));
            if (withRanking)
            {
                OtherPeer = new RootLoopbackFramePeer(bytes => { BeforePeerSend?.Invoke(bytes); PeerSent.Add(bytes); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime());
                peerConnection = OtherPeer.Connection;
                await SendPeerAsync(new LegacyMsgLoginGamePacket(PeerUid).ToFrameBytes());
                Assert.NotNull(PeerCurrent.NativeGameClient!.OrdinarySpellBinding);
            }
            AssertConverged(); Sent.Clear(); PeerSent.Clear();
        }
        internal static byte[] Request(params uint[] ids) => RequestWithCount(checked((uint)ids.Length), ids);
        internal static byte[] RequestWithCount(uint count, params uint[] physical)
        {
            var packet = new MsgItemUsagePacket(0, 0, MsgItemUsageAction.DecomposeRelics, 511_043_495, count, 0, 0);
            var bytes = packet.ToFrameBytes(); Array.Resize(ref bytes, checked(bytes.Length + physical.Length * sizeof(uint)));
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
            for (int index = 0; index < physical.Length; index++)
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4 + MsgItemUsagePacket.PayloadLength + index * 4), physical[index]);
            return bytes;
        }
        internal Task Decompose() => SendAsync(Request(Selected));
        internal async Task SendAsync(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await Handler.HandleFrameAsync(connection!, frame, lifetime!.Token);
            if (Peer is { } peer) await peer.DrainObservations();
            if (observerFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }
        internal async Task SendPeerAsync(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await Handler.HandleFrameAsync(peerConnection!, frame, lifetime!.Token);
            if (OtherPeer is { } peer) await peer.DrainObservations();
        }
        internal void AssertConverged()
        {
            Assert.Equal(File.ReadAllBytes(ItemsPath), PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            Assert.True(Native.OrdinarySpellBinding!.Items.Image.IsCurrentPlain());
            Assert.Equal(Current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), Native.Inventory!.ClientItems.Keys.Order());
            Assert.Equal(Current.PlayerItems.RelicFuseItems.Select(row => row.Uid).Order(), Native.RelicFuse!.Values.Select(row => row.Uid).Order());
        }
        internal async Task CloseAsync()
        {
            BeforeSend = null; BeforePeerSend = null; var errors = new List<Exception>();
            foreach (var actual in new[] { connection, peerConnection }) if (actual is not null)
            {
                try { await actual.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await actual.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            }
            connection = peerConnection = null;
            foreach (var peer in new[] { Peer, OtherPeer }) if (peer is not null)
                try { await peer.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            Peer = OtherPeer = null;
            if (Runtime is not null) try { await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            Runtime = null!; lifetime?.Dispose(); lifetime = null;
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
        internal void RestoreMetadata() { try { beasts?.Dispose(); } finally { beasts = null; log.Dispose(); } }
    }
}
