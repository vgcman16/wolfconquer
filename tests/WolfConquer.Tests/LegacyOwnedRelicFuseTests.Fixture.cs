using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedRelicFuseTests
{
    private static async Task Run(Func<FuseFixture, Task> body, string input = "valid")
    {
        var fixture = new FuseFixture(); var errors = new List<Exception>();
        try { await fixture.InitializeAsync(input); await body(fixture); } catch (Exception error) { errors.Add(error); }
        try { await fixture.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { fixture.RestoreMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { fixture.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    // Actual owned admission/frame/native/file fixture. Inputs and namespace
    // bootstrap precede admission; the server alone chooses entropy and UIDs.
    private sealed class FuseFixture
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
        internal const uint First = 1_045_001, Second = First + 1, Kept = First + 2;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly List<byte[]> Sent = [];
        private readonly StringWriter logBuffer = new();
        private readonly TextWriter log;
        internal WorldDataCatalog Catalog = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal Action<byte[]>? BeforeSend;
        private ConquerTcpConnection? connection;
        internal RootLoopbackFramePeer? Peer;
        internal ConquerTcpConnection ActualConnection => connection!;
        internal CancellationToken ActualToken => lifetime!.Token;
        internal string ReadDiagnostics() { lock (log) return logBuffer.ToString(); }
        private CancellationTokenSource? lifetime;
        private Exception? observerFailure;
        private NativeLoginBeastMetadataScope? beasts;
        internal FuseFixture() { log = TextWriter.Synchronized(logBuffer); }
        internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal uint HighWater => JsonNode.Parse(File.ReadAllBytes(Data.State))!["HighWater"]!.GetValue<uint>();
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Handler));
        internal WorldSession Current
        {
            get
            {
                string output; lock (log) output = logBuffer.ToString();
                Assert.True(Store.TryGet(connection!, out var current), "Owned relic-fuse admission unavailable:\n" + output);
                return Assert.IsType<WorldSession>(current);
            }
        }
        internal LegacyNativeGameClientRuntime Native => Current.NativeGameClient!;

        internal async Task InitializeAsync(string input)
        {
            var first = Item(First, 4_100_001);
            if (input == "locked") first = first.WithLocked(1);
            if (input == "stacked") first = first.WithStackSize(2);
            if (input == "bound") first = first.WithBound(1);
            var items = PlayerItemSnapshot.Empty with
            { InventoryItems = new[] { first, Item(Second, 4_100_002), Item(Kept, 1_000_000) } };
            if (input.StartsWith("return-", StringComparison.Ordinal))
            {
                var heldFirst = Item(First, 4_100_001).WithPosition(213);
                var heldSecond = Item(Second, 4_100_002).WithPosition(214);
                var heldResult = Item(First + 9, 4_100_001).WithPosition(215);
                if (input == "return-locked") heldFirst = heldFirst.WithLocked(1);
                if (input == "return-locked-sibling") heldSecond = heldSecond.WithLocked(1);
                if (input == "return-stacked") heldResult = heldResult.WithStackSize(2);
                if (input == "return-suspicious") heldSecond = Item(Second, 4_100_002, suspicious: 1).WithPosition(214);
                if (input == "return-child") heldSecond = heldSecond.WithDepositedItems([Item(First + 10, 1_000_000)]);
                if (input == "return-invalid-item") heldResult = heldResult.WithItemId(1_000_000);
                if (input == "return-result-type") heldResult = heldResult.WithItemId(4_100_003);
                if (input == "return-unbound-result") heldFirst = heldFirst.WithBound(1);
                int count = input == "return-capacity" ? 40 : 1;
                items = PlayerItemSnapshot.Empty with
                {
                    InventoryItems = Enumerable.Range(0, count).Select(index => Item(Kept + (uint)index + 100, 1_000_000)).ToArray(),
                    RelicFuseItems = [heldFirst, heldSecond, heldResult],
                };
            }
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, items);
            var typed = new uint[] { 4_100_001, 4_100_002, 4_100_003, 4_100_004, 4_100_005, 1_000_000 }
                .Select(id => LegacyFullAccountItemMutationFixture.ItemType(id, "OwnedFuse" + id, durability: 10_000)).ToArray();
            var caps = Enumerable.Range(1, 13).Select(type => XuanbaoParser.ParseComposeAttributeLimitRow(
                "fixture://owned-fuse-caps", type, FormattableString.Invariant($"{type}@@0@@1@@400@@"))).ToArray();
            Catalog = LegacyItemWorldDataFixture.Create(typed, [], xuanbaoComposeAttributeLimits: caps);
            beasts = new(1000);
            Data.Bootstrap();
            await ReopenFrameAsync();
        }

        internal void OpenRuntime()
        {
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var runtime, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(runtime);
        }

        internal async Task ReopenFrameAsync()
        {
            OpenRuntime(); observerFailure = null;
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
            AssertConverged(); Sent.Clear();
        }

        internal static PlayerItemSnapshotItem Item(uint uid, uint type, byte suspicious = 0) => new()
        {
            Uid = uid, ItemId = type, StackSize = 1, Suspicious = suspicious, Durability = 10_000, MaximumDurability = 10_000,
            Attribute1 = 100_003, Attribute2 = 110_004, Attribute3 = 120_005, Attribute4 = 130_009, Attribute5 = 100_114,
        };

        internal async Task SendAsync(byte[] bytes)
        {
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await Handler.HandleFrameAsync(connection!, frame, lifetime!.Token);
            if (Peer is { } peer) await peer.DrainObservations();
            if (observerFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }

        internal void AssertConverged()
        {
            Assert.Equal(File.ReadAllBytes(ItemsPath), PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
            var image = Native.OrdinarySpellBinding!.Items.Image;
            Assert.True(image.IsCurrentPlain());
            Assert.Equal(Current.PlayerItems.InventoryItems.Select(row => row.Uid).Order(), Native.Inventory!.ClientItems.Keys.Order());
            Assert.Equal(Current.PlayerItems.RelicFuseItems.Select(row => row.Uid).Order(), Native.RelicFuse!.Values.Select(row => row.Uid).Order());
        }

        internal async Task CloseAsync()
        {
            BeforeSend = null; var errors = new List<Exception>();
            if (connection is not null)
            {
                try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            }
            connection = null;
            if (Peer is { } peer)
            {
                try { await peer.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
                Peer = null;
            }
            if (Runtime is not null) try { await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            Runtime = null!;
            lifetime?.Dispose(); lifetime = null;
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }

        internal void RestoreMetadata()
        {
            try { beasts?.Dispose(); }
            finally { beasts = null; log.Dispose(); }
        }
    }
}
