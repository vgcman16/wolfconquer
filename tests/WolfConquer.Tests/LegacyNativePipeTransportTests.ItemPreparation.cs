using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Item = WolfConquer.Core.World.LegacyNativeGameItemRuntime;
using Pipe = WolfConquer.Core.World.LegacyNativePipeClientRuntime;
using Extra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePipeTransportTests
{
    private sealed unsafe class PipeItemOutputState
    {
        private readonly nuint memory, cursor;
        private readonly int size;
        private readonly byte[] bytes;
        internal PipeItemOutputState(LegacyNativePacket packet)
        {
            memory = (nuint)packet.Memory; cursor = (nuint)packet.stream; size = packet.Size;
            bytes = PipeItemAllocation(packet);
        }
        internal void AssertUnchanged(LegacyNativePacket packet)
        {
            Assert.Equal(memory, (nuint)packet.Memory); Assert.Equal(cursor, (nuint)packet.stream);
            Assert.Equal(size, packet.Size); Assert.Equal(bytes, PipeItemAllocation(packet));
        }
    }

    private static unsafe byte[] PipeItemAllocation(LegacyNativePacket packet) =>
        new ReadOnlySpan<byte>(packet.Memory, packet.Capacity).ToArray();

    private static unsafe void FillPipeItemTarget(LegacyNativePacket packet)
    {
        new Span<byte>(packet.Memory, packet.Capacity).Fill(0xA5);
        packet.Size = Math.Min(321, packet.Capacity); packet.Seek(Math.Min(237, packet.Capacity));
    }

    private static object PrepareActualPipePlan(Item item, int capacity) =>
        typeof(Item).GetMethod("PreparePipeItem", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(item, [capacity])!;

    private static void RequireActualPipePlan(object plan) => plan.GetType()
        .GetMethod("RequireCurrent", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plan, null);

    private static LegacyNativePacket ActualPlanPacket(object plan, string name)
    {
        object frame = plan.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(plan)!;
        return (LegacyNativePacket)frame.GetType().GetField("Packet", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(frame)!;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LateExtraDateRefusalPreservesPriorWholeOutputQueueAndRealExtrasThenRepairs(bool purificationFault) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        item.Purification = Purification(); item.Refinary = Refinery();
        if (purificationFault)
        {
            item.Purification.AddedOn = DateTime.MaxValue; item.Purification.PurificationDuration = 1;
        }
        else { item.Refinary.AddedOn = DateTime.MaxValue; item.Refinary.EffectDuration = 1; }
        var purification = item.Purification; var refinery = item.Refinary;
        var output = new PipeItemOutputState(scope.Packet);
        byte[] prior = [0xD3, 0xC1]; scope.Queue.Enqueue(prior);
        Assert.Throws<ArgumentOutOfRangeException>(() => item.Send(scope.Pipe, scope.Packet));
        output.AssertUnchanged(scope.Packet); Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);

        item.Purification = Purification(); item.Refinary = Refinery(); scope.Queue.Clear();
        item.Send(scope.Pipe, scope.Packet);
        Assert.Equal(ItemFrame(), scope.Queue.Dequeue());
        Assert.Equal(ExtraFrame(2, 71, 8, 800001, 3, 23, 0, 0, 0,
            82, 3, 724440, 5, 11, 22, 0), scope.Queue.Dequeue());
        Assert.Empty(scope.Queue); Assert.True(PurificationAvailable(item.Purification));
        Assert.True(item.Refinary.Available);
    });

    [Theory]
    [InlineData(205, false)]
    [InlineData(206, true)]
    public Task PhysicalCapacityChecksAllFramesBeforeFinalCallerPublication(int capacity, bool exact) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        item.Purification = Purification(); item.Refinary = Refinery();
        var purification = item.Purification; var refinery = item.Refinary;
        using var target = new LegacyNativePacket(capacity); FillPipeItemTarget(target);
        var output = new PipeItemOutputState(target); byte[] prior = [0xD3, 0xC2]; scope.Queue.Enqueue(prior);
        if (!exact)
        {
            Assert.Throws<InvalidOperationException>(() => item.Send(scope.Pipe, target));
            output.AssertUnchanged(target); Assert.Same(prior, Assert.Single(scope.Queue));
            Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
        }
        else
        {
            item.Send(scope.Pipe, target); Assert.Same(prior, scope.Queue.Dequeue());
            Assert.Equal(ItemFrame(), scope.Queue.Dequeue());
            byte[] extra = ExtraFrame(2, 71, 8, 800001, 3, 23, 0, 0, 0,
                82, 3, 724440, 5, 11, 22, 0);
            Assert.Equal(extra, scope.Queue.Dequeue()); Assert.Empty(scope.Queue);
            Assert.Equal(extra, NativeSpellTestFixture.Bytes(target)); Assert.Equal(4, target.Position);
        }
    });

    [Fact]
    public Task UInt16OversizedItemRefusesWithoutShrinkingRealRelicsOrPublishingPrefix() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        var relics = new LegacyNativeRelicAttribute[16338]; item.RelicAttributes = relics;
        item.Purification = Purification(); item.Refinary = Refinery();
        var purification = item.Purification; var refinery = item.Refinary;
        using var target = new LegacyNativePacket(70000); FillPipeItemTarget(target);
        var output = new PipeItemOutputState(target); byte[] prior = [0xD3, 0xC3]; scope.Queue.Enqueue(prior);
        Assert.Throws<InvalidOperationException>(() => item.Send(scope.Pipe, target));
        output.AssertUnchanged(target); Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.Same(relics, item.RelicAttributes); Assert.Equal(16338, relics.Length);
        Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ConfiguredSealPreservesBothIndependentFramesAtExactPhysicalCapacity(bool extras) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        if (extras) { item.Purification = Purification(); item.Refinary = Refinery(); }
        FieldInfo seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? old = seal.GetValue(null);
        try
        {
            LegacyNativePacket.SealString = "ABCDEFGH";
            using var target = new LegacyNativePacket(214); FillPipeItemTarget(target);
            item.Send(scope.Pipe, target);
            byte[] trailer = Encoding.ASCII.GetBytes("ABCDEFGH");
            byte[] expectedItem = ItemFrame().Concat(trailer).ToArray();
            Assert.Equal(expectedItem, scope.Queue.Dequeue());
            byte[] expectedFinal = expectedItem;
            if (extras)
            {
                expectedFinal = ExtraFrame(2, 71, 8, 800001, 3, 23, 0, 0, 0,
                    82, 3, 724440, 5, 11, 22, 0).Concat(trailer).ToArray();
                Assert.Equal(expectedFinal, scope.Queue.Dequeue());
            }
            Assert.Equal(expectedFinal, NativeSpellTestFixture.Bytes(target)); Assert.Empty(scope.Queue);
        }
        finally { seal.SetValue(null, old); }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public unsafe Task EachOwnedFrameValidatesItsOriginalSealEvenAfterGlobalSealWasRestored(bool extraFrame) =>
        NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        item.Purification = Purification(); item.Refinary = Refinery();
        var purification = item.Purification; var refinery = item.Refinary;
        var output = new PipeItemOutputState(scope.Packet); byte[] prior = [0xD3, 0xC4]; scope.Queue.Enqueue(prior);
        FieldInfo seal = typeof(LegacyNativePacket).GetField("seal", BindingFlags.Static | BindingFlags.NonPublic)!;
        object? old = seal.GetValue(null);
        try
        {
            LegacyNativePacket.SealString = "ABCDEFGH";
            object plan = PrepareActualPipePlan(item, scope.Packet.Capacity);
            using var cleanup = (IDisposable)plan;
            var actual = ActualPlanPacket(plan, extraFrame ? "Extra" : "Item");
            LegacyNativePacket.SealString = "12345678";
            actual.Memory[actual.Size - 1] = (byte)'8';
            LegacyNativePacket.SealString = "ABCDEFGH";
            var exception = Assert.Throws<TargetInvocationException>(() => RequireActualPipePlan(plan));
            Assert.IsType<InvalidOperationException>(exception.InnerException);
            output.AssertUnchanged(scope.Packet); Assert.Same(prior, Assert.Single(scope.Queue));
            Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
        }
        finally { seal.SetValue(null, old); }
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ActualPreparedItemWitnessRejectsReplacedOrMutatedRealInputs(int change) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        item.Purification = Purification(); item.Refinary = Refinery();
        var relics = item.RelicAttributes;
        object plan = PrepareActualPipePlan(item, scope.Packet.Capacity); using var cleanup = (IDisposable)plan;
        if (change == 0) item.RelicAttributes = (LegacyNativeRelicAttribute[])relics.Clone();
        else if (change == 1) relics[0] = new LegacyNativeRelicAttribute(0x01020304u);
        else if (change == 2) item.Uid++;
        else item.Refinary.EffectID++;
        var output = new PipeItemOutputState(scope.Packet); byte[] prior = [0xD3, 0xC5]; scope.Queue.Enqueue(prior);
        var exception = Assert.Throws<TargetInvocationException>(() => RequireActualPipePlan(plan));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        output.AssertUnchanged(scope.Packet); Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.False(PurificationAvailable(item.Purification)); Assert.False(item.Refinary.Available);
    });

    [Fact]
    public Task EscapedPipeCannotUseReboundSocketButActualNewPipeMayHaveNoOwner() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        item.Purification = Purification(); item.Refinary = Refinery();
        var purification = item.Purification; var refinery = item.Refinary;
        var next = new Pipe(scope.Socket, null); scope.Owner.Player = null!; scope.Owner.Fake = true;
        var output = new PipeItemOutputState(scope.Packet); byte[] prior = [0xD3, 0xC6]; scope.Queue.Enqueue(prior);
        Assert.Throws<InvalidOperationException>(() => item.Send(scope.Pipe, scope.Packet));
        output.AssertUnchanged(scope.Packet); Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
        scope.Queue.Clear(); item.Send(next, scope.Packet);
        Assert.Equal(ItemFrame(), scope.Queue.Dequeue());
        Assert.Equal(ExtraFrame(2, 71, 8, 800001, 3, 23, 0, 0, 0,
            82, 3, 724440, 5, 11, 22, 0), scope.Queue.Dequeue());
        Assert.Empty(scope.Queue); Assert.Null(next.Owner);
    });

    [Fact]
    public Task RealSharedRelicArrayRemainsAliasedWithoutDomainItemReplacement() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var first = PhysicalItem(); var second = PhysicalItem();
        var relics = first.RelicAttributes; second.RelicAttributes = relics;
        relics[0] = new LegacyNativeRelicAttribute(0x01020304u);
        byte[] expected = ItemFrame(); Put32(expected, 170, 0x01020304u);
        first.Send(scope.Pipe, scope.Packet); second.Send(scope.Pipe, scope.Packet);
        Assert.Equal(expected, scope.Queue.Dequeue()); Assert.Equal(expected, scope.Queue.Dequeue());
        Assert.Empty(scope.Queue); Assert.Same(relics, first.RelicAttributes); Assert.Same(relics, second.RelicAttributes);
        Assert.Equal(new LegacyNativeRelicAttribute(0x01020304u), relics[0]);
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public unsafe Task InvalidActualTargetMetadataPreservesItemExtrasAndExistingQueue(int change) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        item.Purification = Purification(); item.Refinary = Refinery();
        var purification = item.Purification; var refinery = item.Refinary;
        if (change == 0) scope.Packet.Size = scope.Packet.Capacity + 1;
        else if (change == 1) scope.Packet.stream = scope.Packet.Memory - 1;
        else
        {
            Assert.True(Environment.Is64BitProcess);
            scope.Packet.stream = (byte*)((nuint)scope.Packet.Memory + ((nuint)1 << 32) + 237);
        }
        var output = new PipeItemOutputState(scope.Packet); byte[] prior = [0xD3, 0xC7]; scope.Queue.Enqueue(prior);
        Assert.Throws<InvalidOperationException>(() => item.Send(scope.Pipe, scope.Packet));
        output.AssertUnchanged(scope.Packet); Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingRequiredItemTextRefusesWithoutOutputOrActualExtraChanges(bool signature) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        item.Purification = Purification(); item.Refinary = Refinery();
        if (signature) item.Signature = null!; else item.OwnerName = null!;
        var purification = item.Purification; var refinery = item.Refinary;
        var output = new PipeItemOutputState(scope.Packet); byte[] prior = [0xD3, 0xC9]; scope.Queue.Enqueue(prior);
        Assert.Throws<ArgumentNullException>(() => item.Send(scope.Pipe, scope.Packet));
        output.AssertUnchanged(scope.Packet); Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
        if (signature) Assert.Null(item.Signature); else Assert.Null(item.OwnerName);
    });

    [Fact]
    public unsafe Task DisposedActiveTargetRefusesBeforePreparingItemOrExtras() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        item.Purification = Purification(); item.Refinary = Refinery();
        var purification = item.Purification; var refinery = item.Refinary;
        using var target = new LegacyNativePacket(256); FillPipeItemTarget(target); target.Dispose();
        nuint cursor = (nuint)target.stream; int size = target.Size;
        byte[] prior = [0xD3, 0xCA]; scope.Queue.Enqueue(prior);
        Assert.Throws<ObjectDisposedException>(() => item.Send(scope.Pipe, target));
        Assert.True(target.Memory == null); Assert.Equal(cursor, (nuint)target.stream); Assert.Equal(size, target.Size);
        Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
    });

    [Fact]
    public Task PostcommitTransportRebindingIsObservableAndDoesNotPretendExtraRollback() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = PhysicalItem();
        item.Purification = Purification(); item.Refinary = Refinery();
        object plan = PrepareActualPipePlan(item, scope.Packet.Capacity); using var cleanup = (IDisposable)plan;
        // Invoke the genuine private publication/commit/send phases with a
        // real constructor rebind between them; this is a bounded phase control,
        // not proof of an arbitrary public-field race or whole-call interleaving.
        var targetType = typeof(Item).GetNestedType("PipeItemTarget", BindingFlags.NonPublic)!;
        object target = targetType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
            [typeof(LegacyNativePacket)], null)!.Invoke([scope.Packet]);
        byte[] final = (byte[])plan.GetType().GetProperty("FinalBytes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(plan)!;
        targetType.GetMethod("Publish", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, [scope.Packet, final]);
        plan.GetType().GetMethod("CommitExtras", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(plan, null);
        var replacement = new Pipe(scope.Socket, null);
        byte[] prior = [0xD3, 0xC8]; scope.Queue.Enqueue(prior);
        var exception = Assert.Throws<TargetInvocationException>(() => typeof(Item)
            .GetMethod("SendCommittedPipeItem", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [scope.Pipe, scope.Socket, plan]));
        var committed = Assert.IsType<Item.PipeItemPublicationException>(exception.InnerException);
        Assert.True(committed.ItemStateCommitted); Assert.Equal(0, committed.SendCallsCompleted);
        Assert.IsType<InvalidOperationException>(committed.InnerException);
        Assert.True(PurificationAvailable(item.Purification)); Assert.True(item.Refinary.Available);
        Assert.Equal(Extra.Typing.Stabilization, item.Purification.Typ);
        Assert.Equal(Extra.Typing.PermanentRefinery, item.Refinary.Typ);
        Assert.Equal(final, NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Same(prior, Assert.Single(scope.Queue));
        Assert.Same(replacement, scope.Socket.Client);
    });
}
