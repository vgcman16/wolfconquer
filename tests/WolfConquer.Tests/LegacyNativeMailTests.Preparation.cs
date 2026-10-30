using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Mail = WolfConquer.Core.World.LegacyNativeMailList;
using ListMessage = WolfConquer.Core.World.LegacyNativeCMsgMailList;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMailTests
{
    private sealed unsafe class MailOutputState
    {
        private readonly nuint memory, cursor;
        private readonly int size;
        private readonly byte[] bytes;

        internal MailOutputState(LegacyNativePacket packet)
        {
            memory = (nuint)packet.Memory;
            cursor = (nuint)packet.stream;
            size = packet.Size;
            bytes = MailAllocation(packet);
        }

        internal void AssertUnchanged(LegacyNativePacket packet)
        {
            Assert.Equal(memory, (nuint)packet.Memory);
            Assert.Equal(cursor, (nuint)packet.stream);
            Assert.Equal(size, packet.Size);
            Assert.Equal(bytes, MailAllocation(packet));
        }
    }

    private static LegacyNativeDbSafeDictionary<uint, Mail> RequirePreparedMailMap(Scope scope) =>
        Assert.IsAssignableFrom<LegacyNativeDbSafeDictionary<uint, Mail>>(scope.Client.MailList);

    private static unsafe byte[] MailAllocation(LegacyNativePacket packet) =>
        new ReadOnlySpan<byte>(packet.Memory, packet.Capacity).ToArray();

    private static unsafe void FillMailTarget(LegacyNativePacket packet)
    {
        new Span<byte>(packet.Memory, packet.Capacity).Fill(0xA5);
        packet.Size = Math.Min(17, packet.Capacity);
        packet.Seek(Math.Min(9, packet.Capacity));
    }

    private static void ProduceMail(Scope scope, bool request, LegacyNativePacket? target = null)
    {
        if (request) Process(scope.Client, target ?? scope.Packet);
        else ListMessage.Loading(scope.Client);
    }

    [Theory]
    [InlineData(false, 1033)]
    [InlineData(true, 1033)]
    [InlineData(false, 65536)]
    [InlineData(true, 65536)]
    public Task OversizedLaterPageRefusesWholeSequenceAndRepairCanRetry(bool request, int width) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint id = 1; id <= 11; id++) RequirePreparedMailMap(scope).Add(1000 + id, Row(id));
        var bad = RequirePreparedMailMap(scope)[request ? 1011u : 1001u];
        bad.title = new string('X', width); // Only the second native page is oversized.
        var original = RequirePreparedMailMap(scope).ToArray();
        long revision = RequirePreparedMailMap(scope).Revision;
        byte[] prior = [0xD3, 0xB1]; scope.Queue.Enqueue(prior);
        var output = new MailOutputState(scope.Packet);
        var rental = new MailOutputState(scope.Recycled(0));
        Assert.Throws<InvalidOperationException>(() => ProduceMail(scope, request));
        Assert.Same(prior, Assert.Single(scope.Queue));
        output.AssertUnchanged(scope.Packet); rental.AssertUnchanged(scope.Recycled(0));
        Assert.Equal(12, scope.Available); Assert.Equal(revision, RequirePreparedMailMap(scope).Revision);
        foreach (var entry in original) Assert.Same(entry.Value, RequirePreparedMailMap(scope)[entry.Key]);
        Assert.Equal(width, bad.title.Length);

        bad.title = "Repaired"; scope.Queue.Clear();
        ProduceMail(scope, request);
        var rows = request
            ? original.Select(entry => entry.Value).OrderByDescending(row => row.id).Reverse().ToArray()
            : original.Select(entry => entry.Value).Reverse().ToArray();
        Assert.Equal(MailFrame(0, 1, rows.Take(10).ToArray()), scope.Queue.Dequeue());
        Assert.Equal(MailFrame(0, 1, rows.Skip(10).ToArray()), scope.Queue.Dequeue());
        Assert.Empty(scope.Queue); Assert.Equal(request ? 12 : 11, scope.Available);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RequestPreflightsEveryPageAgainstActualCapacityBeforeFirstCopy(bool exactCapacity) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint id = 1; id <= 11; id++) RequirePreparedMailMap(scope).Add(id + 100, Row(id));
        RequirePreparedMailMap(scope)[111].title = new string('X', 800);
        var rows = RequirePreparedMailMap(scope).Values.OrderBy(row => row.id).ToArray();
        byte[] first = MailFrame(0, 1, rows.Take(10).ToArray());
        byte[] last = MailFrame(0, 1, rows.Skip(10).ToArray());
        Assert.True(first.Length < last.Length); Assert.True(last.Length <= 1032);
        using var target = new LegacyNativePacket(exactCapacity ? last.Length : first.Length);
        FillMailTarget(target); var output = new MailOutputState(target);
        byte[] prior = [0xD3, 0xB2]; scope.Queue.Enqueue(prior);
        if (exactCapacity)
        {
            Process(scope.Client, target);
            Assert.Same(prior, scope.Queue.Dequeue());
            Assert.Equal(first, scope.Queue.Dequeue()); Assert.Equal(last, scope.Queue.Dequeue());
            Assert.Equal(last.Length, target.Size); Assert.Equal(4, target.Position);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => Process(scope.Client, target));
            Assert.Same(prior, scope.Queue.Dequeue()); output.AssertUnchanged(target);
        }
        Assert.Empty(scope.Queue); Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task LoadingUsesRealLastRentalAndReportsIncompatibleRentalWithoutDirtyBytesOrPages() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        for (uint id = 1; id <= 11; id++) RequirePreparedMailMap(scope).Add(id + 100, Row(id));
        RequirePreparedMailMap(scope)[101].title = new string('X', 800);
        var rows = RequirePreparedMailMap(scope).Values.Reverse().ToArray();
        byte[] first = MailFrame(0, 1, rows.Take(10).ToArray());
        byte[] last = MailFrame(0, 1, rows.Skip(10).ToArray());
        Assert.True(first.Length < last.Length); Assert.True(last.Length <= 1032);
        using var small = new LegacyNativePacket(first.Length);
        using var exact = new LegacyNativePacket(last.Length);
        FillMailTarget(small); FillMailTarget(exact);
        byte[] smallBefore = MailAllocation(small); int smallSize = small.Size;
        var caller = new MailOutputState(scope.Packet);
        var bin = new ConcurrentQueue<LegacyNativePacket>(); bin.Enqueue(small);
        FieldInfo binField = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        binField.SetValue(null, bin);
        try
        {
            byte[] prior = [0xD3, 0xB3]; scope.Queue.Enqueue(prior);
            Assert.Throws<InvalidOperationException>(() => ListMessage.Loading(scope.Client));
            Assert.Same(prior, Assert.Single(scope.Queue)); caller.AssertUnchanged(scope.Packet);
            Assert.Equal(smallBefore, MailAllocation(small)); Assert.Equal(smallSize, small.Size);
            Assert.Equal(0, small.Position); Assert.Equal(0, scope.Available);
            // This is the genuine allocator limit: Take dequeued/rewound and
            // Dispose rewound again. Neither is represented as pool rollback.
            scope.Queue.Clear(); bin.Enqueue(exact);
            ListMessage.Loading(scope.Client);
            Assert.Equal(first, scope.Queue.Dequeue()); Assert.Equal(last, scope.Queue.Dequeue());
            Assert.Empty(scope.Queue); Assert.Equal(0, scope.Available);
            Assert.Equal(last.Length, exact.Size); Assert.Equal(0, exact.Position);
            Assert.Equal(last, MailAllocation(exact)); // Exact actual rental was used.
        }
        finally { scope.RestoreBin(); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RequiredRealClientAndMapRefuseWithoutUnrelatedPacketOrPoolChanges(bool request) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var output = new MailOutputState(scope.Packet); var rental = new MailOutputState(scope.Recycled(0));
        byte[] prior = [0xD3, 0xB4]; scope.Queue.Enqueue(prior);
        if (request) Assert.Throws<ArgumentNullException>(() => Process(null!, scope.Packet));
        else Assert.Throws<ArgumentNullException>(() => ListMessage.Loading(null!));
        var map = RequirePreparedMailMap(scope); scope.Client.MailList = null!;
        try { Assert.Throws<ArgumentException>(() => ProduceMail(scope, request)); }
        finally { scope.Client.MailList = map; }
        Assert.Same(prior, Assert.Single(scope.Queue)); Assert.Empty(map);
        output.AssertUnchanged(scope.Packet); rental.AssertUnchanged(scope.Recycled(0));
        Assert.Equal(12, scope.Available);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public unsafe Task InvalidRequestTargetMetadataRefusesWholeBufferAndExistingQueue(int fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(); RequirePreparedMailMap(scope).Add(999, row);
        if (fault == 0) scope.Packet.Size = -1;
        else if (fault == 1) scope.Packet.Size = scope.Packet.Capacity + 1;
        else if (fault == 2) scope.Packet.stream = scope.Packet.Memory + scope.Packet.Capacity + 1;
        else if (fault == 3) scope.Packet.stream = scope.Packet.Memory - 1;
        else
        {
            Assert.True(Environment.Is64BitProcess);
            scope.Packet.stream = (byte*)((nuint)scope.Packet.Memory + ((nuint)1 << 32) + 237);
        }
        var output = new MailOutputState(scope.Packet); var rental = new MailOutputState(scope.Recycled(0));
        byte[] prior = [0xD3, 0xB5]; scope.Queue.Enqueue(prior);
        Assert.Throws<InvalidOperationException>(() => Process(scope.Client, scope.Packet));
        output.AssertUnchanged(scope.Packet); rental.AssertUnchanged(scope.Recycled(0));
        Assert.Same(prior, Assert.Single(scope.Queue)); Assert.Same(row, RequirePreparedMailMap(scope)[999]);
        Assert.Equal(12, scope.Available);
    });

    [Fact]
    public unsafe Task DisposedRequestTargetRefusesBeforePublicationOrRental() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); RequirePreparedMailMap(scope).Add(987, Row());
        using var target = new LegacyNativePacket(64); FillMailTarget(target); target.Dispose();
        nuint cursor = (nuint)target.stream; int size = target.Size;
        byte[] prior = [0xD3, 0xB6]; scope.Queue.Enqueue(prior);
        var rental = new MailOutputState(scope.Recycled(0));
        Assert.Throws<ObjectDisposedException>(() => Process(scope.Client, target));
        Assert.True(target.Memory == null); Assert.Equal(cursor, (nuint)target.stream); Assert.Equal(size, target.Size);
        Assert.Same(prior, Assert.Single(scope.Queue)); rental.AssertUnchanged(scope.Recycled(0));
        Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task SameIdRowsAndRealAliasesKeepNativeTieOrderWithoutKeyNormalization() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var a = Row(5); a.title = "A"; var b = Row(5); b.title = "B"; var c = Row(5); c.title = "C";
        RequirePreparedMailMap(scope).Add(901, a); RequirePreparedMailMap(scope).Add(902, b);
        RequirePreparedMailMap(scope).Add(903, c); RequirePreparedMailMap(scope).Add(904, a);
        byte[] expected = MailFrame(0, 1, a, c, b, a);
        ListMessage.Loading(scope.Client); Process(scope.Client, scope.Packet);
        Assert.Equal(expected, scope.Queue.Dequeue()); Assert.Equal(expected, scope.Queue.Dequeue());
        Assert.Empty(scope.Queue); Assert.Equal(4, RequirePreparedMailMap(scope).Count);
        Assert.Same(a, RequirePreparedMailMap(scope)[901]); Assert.Same(a, RequirePreparedMailMap(scope)[904]);
    });

    [Fact]
    public Task UnusedMailContentAndTimestampAreNotNewListPrerequisites() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(); row.content = null!; row.TimeStamp = default;
        RequirePreparedMailMap(scope).Add(800, row);
        ListMessage.Loading(scope.Client); Process(scope.Client, scope.Packet);
        Assert.Equal(MailFrame(0, 1, row), scope.Queue.Dequeue());
        Assert.Equal(MailFrame(0, 1, row), scope.Queue.Dequeue()); Assert.Empty(scope.Queue);
        Assert.Null(row.content); Assert.Equal(default, row.TimeStamp);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public Task ActualPreparedSnapshotRejectsChangedOwnerMembershipAndWireValues(int change) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(); var map = RequirePreparedMailMap(scope); map.Add(900, row);
        var nested = typeof(ListMessage).GetNestedType("PreparedMailPages", BindingFlags.NonPublic)!;
        var create = nested.GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic)!;
        var require = nested.GetMethod("RequireCurrent", BindingFlags.Instance | BindingFlags.NonPublic)!;
        byte[] prior = [0xD3, 0xB7]; scope.Queue.Enqueue(prior);
        var output = new MailOutputState(scope.Packet); var rental = new MailOutputState(scope.Recycled(0));
        lock (map.SyncRoot)
        {
            object prepared = create.Invoke(null, [scope.Client, map, false])!;
            if (change == 0) map[900] = Row();
            else if (change == 1) scope.Client.MailList = new LegacyNativeDbSafeDictionary<uint, Mail>();
            else if (change == 2) row.Money++;
            else if (change == 3) row.Sender_name = "Changed";
            else { Assert.True(map.Remove(900)); map.Add(900, row); }
            var exception = Assert.Throws<TargetInvocationException>(() => require.Invoke(prepared, [scope.Client]));
            Assert.IsType<InvalidOperationException>(exception.InnerException);
        }
        Assert.Same(prior, Assert.Single(scope.Queue)); output.AssertUnchanged(scope.Packet);
        rental.AssertUnchanged(scope.Recycled(0)); Assert.Equal(12, scope.Available);
    });

    [Fact]
    public Task ActualConfiguredSealIsPreparedOnceAndPreservedInEveryPage() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var row = Row(); RequirePreparedMailMap(scope).Add(456, row);
        FieldInfo seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? prior = seal.GetValue(null);
        try
        {
            LegacyNativePacket.SealString = "ABCDEFGH";
            ListMessage.Loading(scope.Client); Process(scope.Client, scope.Packet);
            byte[] expected = MailFrame(0, 1, row).Concat(Encoding.UTF8.GetBytes("ABCDEFGH")).ToArray();
            Assert.Equal(expected, scope.Queue.Dequeue()); Assert.Equal(expected, scope.Queue.Dequeue());
            Assert.Empty(scope.Queue); Assert.Equal(expected.Length, scope.Packet.Size);
        }
        finally { seal.SetValue(null, prior); }
    });
}
