using System.Buffers;
using System.Collections.Concurrent;
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
public sealed partial class LegacyOperatorGearMailOwnerTests
{
    private const uint Uid = OperatorGearMailFixture.Uid;
    private const uint GearId = OperatorGearMailFixture.GearId;

    internal static async Task WithOwnerAsync(Func<OperatorGearMailFixture, Task> test)
    {
        OperatorGearMailFixture? owner = null;
        Exception? primary = null;
        try { owner = new OperatorGearMailFixture(); owner.Initialize(); await owner.LoginAsync(); await test(owner); }
        catch (Exception error) { primary = error; }
        Exception? cleanup = null;
        if (owner is not null) try { owner.RetainEvidence = primary is not null; await owner.DisposeAsync(); } catch (Exception error) { cleanup = error; }
        if (primary is not null && cleanup is not null) throw new AggregateException(primary, cleanup);
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

}

internal sealed class OperatorGearMailFixture : IAsyncDisposable
    {
        internal const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
        internal const uint AccountId = Uid;
        internal const uint GearId = 410339;
        internal Guid Domain => Fixture.ExpectedDomain;
        internal byte[][] SentSnapshot() => Sent.ToArray();
        internal readonly LegacyOwnedAdmissionRuntimeFixture Fixture = new(bootstrap: false);
        internal readonly NativeLoginBeastMetadataScope Beasts = new(1000);
        internal readonly ConcurrentQueue<byte[]> Sent = new();
        internal readonly StringWriter Log = new();
        internal readonly CancellationTokenSource Closed = new();
        internal readonly LegacyNativeGameClientRuntime Native = new();
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal ConquerTcpConnection Connection = null!;
        internal bool RetainEvidence;
        internal bool TransportClosed;
        internal WorldSessionStore Store => Field<WorldSessionStore>(Handler, "_sessions");
        internal WorldSession Current => Assert.IsType<WorldSession>(Store.TryGet(Connection, out var current) ? current : null);

        internal void Initialize(IReadOnlyList<ItemType>? additionalCatalogItems = null)
        {
            // v4 requires an existing complete mailbox image; this is a separate no-reward fixture initialization.
            new LegacyMailboxStore(Fixture.Root).Save(Uid);
            Fixture.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Fixture.Loader, Fixture.Options(), out var opened, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
            var catalog = LegacyItemWorldDataFixture.Create(new[] { Item(1_000_000), Item(GearId) }
                .Concat(additionalCatalogItems ?? []).ToArray(), []);
            Handler = new WorldServerFrameHandler(Fixture.Loader, null, catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
                Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (frame, _) => { Sent.Enqueue(frame.ToArray()); return ValueTask.CompletedTask; },
                _ => { Closed.Cancel(); return ValueTask.CompletedTask; }, Native);
        }

        internal async Task LoginAsync()
        {
            await Handler.HandleFrameAsync(Connection, Frame(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes()));
            Assert.Same(Native, Current.NativeGameClient);
            Assert.NotNull(Native.OrdinarySpellBinding);
        }
        internal LegacyOperatorGearMailRequest Request(Guid operation) => new(operation, Fixture.ExpectedDomain,
            Uid, Uid, "Gear", "Fixture", "Synthetic equipment", new[] { new LegacyOperatorGearMailItem(GearId) });
        internal uint HighWater => JsonNode.Parse(File.ReadAllBytes(Fixture.State))!["HighWater"]!.GetValue<uint>();
        internal PlayerItemSnapshot Items() => new PlayerItemSnapshotLoader(Fixture.Items).LoadByCharacterUid(Uid);
        internal LegacyMailboxEntry[] Mail() => new LegacyMailboxStore(Fixture.Root).Load(Uid).ToArray();
        internal async Task ClaimAsync(uint id) => await Handler.HandleFrameAsync(Connection,
            Frame(new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimItem, id, []).ToFrameBytes()));
        internal async ValueTask<LegacyOperatorGearMailResult> Deliver(LegacyOperatorGearMailRequest request) =>
            await Handler.DeliverOperatorTestGearMailAsync(Runtime, request, CancellationToken.None);
        internal async Task CloseAsync()
        {
            if (TransportClosed) return;
            await Connection.DisconnectAsync();
            await Connection.CompleteTransportCloseAsync();
            TransportClosed = true;
        }
        public async ValueTask DisposeAsync()
        {
            var errors = new List<Exception>();
            try { if (Connection is not null) await CloseAsync(); } catch (Exception e) { errors.Add(e); }
            try { if (Runtime is not null) await Runtime.DisposeAsync(); } catch (Exception e) { errors.Add(e); }
            try { Closed.Dispose(); Beasts.Dispose(); Log.Dispose(); } catch (Exception e) { errors.Add(e); }
            if (errors.Count == 0 && !RetainEvidence) Fixture.Dispose(); // retain owned evidence on failed close/drain
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }

    private static ItemType Item(uint id)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "Synthetic" + fields[0];
        fields[ItemType.DurabilityFieldIndex] = "321"; fields[ItemType.StackSizeFieldIndex] = "1";
        return new ItemType(checked((int)id), fields[1], fields);
    }
    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
    private static T Field<T>(object instance, string name) where T : class => Assert.IsType<T>(instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance));
}
