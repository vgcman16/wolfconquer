using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedStartupItemUidOwnershipTests
{
    [Theory]
    [InlineData("unrelated-items")] [InlineData("redeem-physical")]
    [InlineData("claim-physical")] [InlineData("detained-alias")]
    public Task AllAfterV2RetirementCannotRelinquishItsCapturedBeforeUidToAnUnrelatedPhysicalOwner(string owner) => Run(async f =>
    {
        await f.StageRetirementAsync(2); await f.CloseAsync(); f.RequireAllAfter();
        if (owner == "unrelated-items") File.WriteAllBytes(Path.Combine(f.Data.Items,
            (RetirementFixture.Uid + 1).ToString(CultureInfo.InvariantCulture) + ".bin"), f.BeforeItems);
        else
        {
            var bytes = DetainedImage(rewardOnly: false);
            if (owner is "redeem-physical" or "detained-alias") File.WriteAllBytes(f.Data.Redeem, bytes);
            if (owner is "claim-physical" or "detained-alias") File.WriteAllBytes(f.Data.Claim, bytes);
        }
        // The current physical namespace is unique: the original owner has
        // already retired this UID. Only its exact captured BEFORE proves the
        // conflict, including the native exact Redeem/Claim alias counted once.
        var observed = f.Scan(); Assert.Equal(1, observed.PhysicalCount); Assert.Equal(0, observed.ReferenceCount);
        Assert.True(observed.Maximum <= LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor);
        f.AssertRefused(); f.RequireAllAfter();
    });

    [Theory]
    [InlineData(0, "no-reference")] [InlineData(1, "no-reference")] [InlineData(2, "no-reference")]
    [InlineData(0, "mail-reference")] [InlineData(1, "mail-reference")] [InlineData(2, "mail-reference")]
    [InlineData(0, "reward-reference")] [InlineData(1, "reward-reference")] [InlineData(2, "reward-reference")]
    public Task GenuineV2RetirementAndExternalUidReferencesRecoverWithoutInventingAnotherPhysicalOwner(int boundary, string reference) => Run(async f =>
    {
        await f.StageRetirementAsync(boundary); await f.CloseAsync();
        if (reference == "mail-reference") f.WriteMailReference(RetirementFixture.ItemUid);
        else if (reference == "reward-reference") File.WriteAllBytes(f.Data.Redeem, DetainedImage(rewardOnly: true));
        var observed = f.Scan(); Assert.Equal(boundary == 2 ? 0 : 1, observed.PhysicalCount);
        Assert.Equal(reference == "no-reference" ? 0 : 1, observed.ReferenceCount);
        var expected = f.Data.Files();
        expected[Path.GetRelativePath(f.Data.Root, f.ItemsPath)] = f.AfterItems;
        expected.Remove(Path.GetRelativePath(f.Data.Root, f.PendingPath));
        f.Open(); f.AssertRecovered(); f.Data.AssertFiles(expected);
        await f.CloseAsync(); f.AssertLeaseAvailable(); var recovered = f.Data.Files();
        f.Open(); f.AssertRecovered(); f.Data.AssertFiles(recovered);
    });

    [Fact]
    public Task AReferenceExemptionStillCannotExceedTheActualPinnedUidHighWater() => Run(async f =>
    {
        await f.StageRetirementAsync(2); await f.CloseAsync(); f.RequireAllAfter();
        f.WriteMailReference(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1);
        var observed = f.Scan(); Assert.Equal(0, observed.PhysicalCount); Assert.Equal(1, observed.ReferenceCount);
        Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor + 1, observed.Maximum);
        f.AssertRefused(); f.RequireAllAfter();
    });

    private static async Task Run(Func<RetirementFixture, Task> body)
    {
        var fixture = new RetirementFixture(); var errors = new List<Exception>();
        try { await fixture.InitializeAsync(); await body(fixture); } catch (Exception error) { errors.Add(error); }
        try { await fixture.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { fixture.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
        // The guarded owned fixture remains intact on assertion/cleanup failure.
    }

    private static byte[] DetainedImage(bool rewardOnly)
    {
        Assert.Equal(322, Unsafe.SizeOf<LegacyNativeMsgDetainedItem>());
        var row = new LegacyNativeMsgDetainedItem
        {
            UID = 1, ItemUID = RetirementFixture.ItemUid, ItemID = 1_000_000,
            OwnerUID = RetirementFixture.Uid, GainerUID = RetirementFixture.Uid + 1,
            Action = rewardOnly ? LegacyNativeMsgDetainedItem.ContainerType.RewardCps : LegacyNativeMsgDetainedItem.ContainerType.ClaimPage,
        };
        var bytes = new byte[sizeof(int) + Unsafe.SizeOf<LegacyNativeMsgDetainedItem>()];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1); MemoryMarshal.Write(bytes.AsSpan(sizeof(int)), in row); return bytes;
    }

    private sealed class RetirementFixture
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid, ItemUid = 1_000_091;
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new();
        private readonly List<LegacyOwnedAdmissionRuntime> runtimes = [];
        private readonly StringWriter log = new();
        private TextWriter? synchronized;
        private NativeLoginBeastMetadataScope? beast;
        private CancellationTokenSource? lifetime;
        private ConquerTcpConnection? connection;
        private WorldServerFrameHandler handler = null!;
        private LegacyOwnedAdmissionRuntime runtime = null!;
        private readonly List<byte[]> sent = [];
        internal byte[] BeforeItems { get; private set; } = [];
        internal byte[] AfterItems { get; private set; } = [];
        private byte[] character = [], registry = [], pending = [];
        internal string ItemsPath => Path.Combine(Data.Items, Uid.ToString(CultureInfo.InvariantCulture) + ".bin");
        internal string PendingPath => Path.Combine(Data.Users, ".managed-trade-journal", "pending.json");
        private WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
        private WorldSession Current => Assert.IsType<WorldSession>(Store.TryGet(connection!, out var current) ? current : null);

        internal async Task InitializeAsync()
        {
            synchronized = TextWriter.Synchronized(log); beast = new(1000); Open();
            var catalog = NativeCallerWorldDataFixture.Create([OwnedEquipmentBehaviorFixture.Item(1_000_000, "OwnedRetirement")], []);
            handler = new WorldServerFrameHandler(Data.Loader, null, catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
                synchronized, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            var cancellation = new CancellationTokenSource(); lifetime = cancellation;
            var native = new LegacyNativeGameClientRuntime();
            connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (bytes, token) => { token.ThrowIfCancellationRequested(); sent.Add(bytes.ToArray()); return ValueTask.CompletedTask; },
                _ => { cancellation.Cancel(); return ValueTask.CompletedTask; }, native);
            var bytes = new LegacyMsgLoginGamePacket(Uid).ToFrameBytes();
            Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await handler.HandleFrameAsync(connection, frame, cancellation.Token);
            Assert.Equal(Uid, Current.Character.Uid); Assert.Same(native, Current.NativeGameClient);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, Current));
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
            Assert.True(Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(ItemUid, Assert.Single(Current.PlayerItems.InventoryItems).Uid);
            Assert.Equal(ItemUid, Assert.Single(native.Inventory!.ClientItems).Value.Uid);
            character = File.ReadAllBytes(Data.UserPath); BeforeItems = File.ReadAllBytes(ItemsPath);
            registry = File.ReadAllBytes(Data.State);
        }

        internal Task StageRetirementAsync(int boundary)
        {
            var original = Current; var native = original.NativeGameClient!;
            var updated = original with { PlayerItems = original.PlayerItems with { InventoryItems = [] } };
            AfterItems = PlayerItemSnapshotLoader.ToLegacyBytes(updated.PlayerItems);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
            var image = binding.Items.Image; var originalPhysical = Assert.Single(native.Inventory!.ClientItems).Value;
            var reason = string.Empty; int calls = 0, first = sent.Count;
            using var stop = LegacyJournalInterruption.StopAfter(boundary);
            bool ActualWriter()
            {
                calls++;
                return LegacyNpcSalePersistence.TryPersistItemRemoval(original, updated, Data.Items, stop, out reason);
            }
            // Actual owner gate and real one-whole-UID writer. This component
            // boundary does not claim Drop eligibility or ground publication.
            Assert.False(Store.TryCommitSession(connection!, original, updated, ActualWriter, out var final));
            Assert.Equal(1, calls); Assert.Null(final); Assert.True(stop.PendingObserved, log.ToString());
            Assert.Equal(Enumerable.Range(0, boundary + 1), stop.Visited);
            Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
            runtime.Recovery.Block(reason); // Same recovery-required latch as the actual removal caller.
            Assert.True(runtime.Recovery.IsBlocked); Assert.Equal(first, sent.Count); Assert.Same(original, Current);
            Assert.Same(image, binding.Items.Image); Assert.Same(originalPhysical, Assert.Single(native.Inventory.ClientItems).Value);
            Assert.Equal(character, File.ReadAllBytes(Data.UserPath));
            Assert.Equal(boundary == 2 ? AfterItems : BeforeItems, File.ReadAllBytes(ItemsPath));
            pending = File.ReadAllBytes(PendingPath); var record = JsonNode.Parse(pending)!;
            Assert.Equal(2, record["Version"]!.GetValue<int>()); var changes = record["Changes"]!.AsArray();
            Assert.Equal(2, changes.Count);
            for (var index = 0; index < changes.Count; index++)
            {
                var change = changes[index]!;
                Assert.Equal((int)(index == 0 ? LegacyTradeJournalFileKind.Character : LegacyTradeJournalFileKind.Items), change["Kind"]!.GetValue<int>());
                Assert.Equal((long)Uid, change["FileId"]!.GetValue<long>());
                Assert.Equal(index == 0 ? character : BeforeItems, Convert.FromBase64String(change["Before"]!.GetValue<string>()));
                Assert.Equal(index == 0 ? character : AfterItems, Convert.FromBase64String(change["After"]!.GetValue<string>()));
            }
            Assert.Equal(registry, File.ReadAllBytes(Data.State)); return Task.CompletedTask;
        }

        internal void Open()
        {
            var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason);
            if (opened is not null) runtimes.Add(opened);
            Assert.True(success, reason); runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            Assert.True(runtime.Recovery.IsReadyForOrdinaryAdmission(Data.Options().Roots));
        }

        internal LegacyItemUidRawCatalog.Observation Scan()
        {
            Assert.True(LegacyItemUidRawCatalog.TryScan(Data.Items, Data.Mailbox, Data.Redeem, Data.Claim,
                out var observed, out var reason), reason); return observed;
        }

        internal void RequireAllAfter()
        {
            Assert.True(LegacyTradeJournal.HasPending(Data.Users)); Assert.Equal(pending, File.ReadAllBytes(PendingPath));
            Assert.Equal(character, File.ReadAllBytes(Data.UserPath)); Assert.Equal(AfterItems, File.ReadAllBytes(ItemsPath));
            Assert.Contains(ItemUid, LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(BeforeItems));
            Assert.Empty(LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(AfterItems));
        }

        internal void AssertRefused()
        {
            var files = Data.Files(); var success = LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason);
            if (opened is not null) runtimes.Add(opened);
            Assert.False(success, reason); Assert.Null(opened); Assert.NotEmpty(reason);
            Data.AssertFiles(files); Assert.Equal(registry, File.ReadAllBytes(Data.State)); AssertLeaseAvailable();
        }

        internal void AssertRecovered()
        {
            Assert.False(LegacyTradeJournal.HasPending(Data.Users)); Assert.Equal(character, File.ReadAllBytes(Data.UserPath));
            Assert.Equal(AfterItems, File.ReadAllBytes(ItemsPath)); Assert.Equal(registry, File.ReadAllBytes(Data.State));
            var observed = Scan(); Assert.Equal(0, observed.PhysicalCount);
            Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.HistoricalFloor, JsonNode.Parse(registry)!["HighWater"]!.GetValue<uint>());
            Assert.Throws<IOException>(() => new FileStream(Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose());
        }

        internal void WriteMailReference(uint uid)
        {
            var mailbox = new LegacyMailboxStore(Data.Root); const uint owner = Uid + 1;
            mailbox.Add(owner, new LegacyMailboxEntry { Id = 1, Title = "owned retired UID reference", ItemId = uid, Timer = DateTime.Now.AddDays(1).Ticks });
            mailbox.Save(owner);
        }

        internal void AssertLeaseAvailable()
        { using var lease = new FileStream(Data.OwnerLease, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Assert.Equal(0L, lease.Length); }

        internal async Task CloseAsync()
        {
            var errors = new List<Exception>();
            if (connection is not null)
            {
                try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            }
            connection = null;
            for (var index = runtimes.Count - 1; index >= 0; index--)
                try { await runtimes[index].DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            runtimes.Clear();
            foreach (var resource in new IDisposable?[] { lifetime, beast, synchronized, log })
                try { resource?.Dispose(); } catch (Exception error) { errors.Add(error); }
            lifetime = null; beast = null; synchronized = null;
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
    }
}
