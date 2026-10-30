using WolfConquer.Core.World;
using Pipe = WolfConquer.Core.World.LegacyNativePipeClientRuntime;
using Extra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePipeTransportTests
{
    [Fact]
    public Task NullPipeRefusesBeforeCallerOutputOrExpiryMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();

        var before = new PipeItemOutputState(scope.Packet);
        Assert.Throws<ArgumentNullException>(() => item.Send((Pipe)null!, scope.Packet));

        before.AssertUnchanged(scope.Packet);
        Assert.Empty(scope.Queue);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.Equal(Extra.Typing.ExpireTime, item.Purification.Typ);
    });

    [Fact]
    public Task NullStreamFaultPrecedesTransportAndBothEffectStateChanges() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();
        item.Refinary = Refinery();

        var before = new PipeItemOutputState(scope.Packet);
        Assert.Throws<ArgumentNullException>(() => item.Send(scope.Pipe, null!));
        before.AssertUnchanged(scope.Packet);

        Assert.Equal(237, scope.Packet.Position);
        Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.False(item.Refinary.Available);
    });

    [Fact]
    public Task MissingActiveSocketRefusesBeforeCallerOutputOrExtras() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();
        scope.Pipe.Socket = null!;

        var before = new PipeItemOutputState(scope.Packet);
        Assert.Throws<InvalidOperationException>(() => item.Send(scope.Pipe, scope.Packet));

        before.AssertUnchanged(scope.Packet);
        Assert.Empty(scope.Queue);
        Assert.True(scope.Pipe.Alive);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.Equal(Extra.Typing.ExpireTime, item.Purification.Typ);
    });

    [Fact]
    public Task NullRelicArrayPreservesCompleteTargetAndActualExtras() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.RelicAttributes = null!;
        item.Purification = Purification();
        var purification = item.Purification; var refinery = item.Refinary;
        var before = new PipeItemOutputState(scope.Packet);

        Assert.Throws<ArgumentNullException>(() => item.Send(scope.Pipe, scope.Packet));

        before.AssertUnchanged(scope.Packet); Assert.Empty(scope.Queue);
        Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
        Assert.Null(item.RelicAttributes);
    });

    [Fact]
    public Task PurificationExpiryFaultRefusesWholeItemAndExtraSequence() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();
        item.Purification.AddedOn = DateTime.MaxValue;
        item.Purification.PurificationDuration = 1;
        item.Refinary = Refinery();

        var before = new PipeItemOutputState(scope.Packet);
        var purification = item.Purification; var refinery = item.Refinary;
        Assert.Throws<ArgumentOutOfRangeException>(() => item.Send(scope.Pipe, scope.Packet));

        Assert.Empty(scope.Queue); before.AssertUnchanged(scope.Packet);
        Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.False(item.Refinary.Available);
        Assert.Equal(Extra.Typing.ExpireTime, item.Purification.Typ);
        Assert.Equal(Extra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
    });

    [Fact]
    public Task RefineryExpiryFaultPreservesEarlierPurificationAndWholeOutputSequence() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();
        item.Refinary = Refinery();
        item.Refinary.AddedOn = DateTime.MaxValue;
        item.Refinary.EffectDuration = 1;

        var before = new PipeItemOutputState(scope.Packet);
        var purification = item.Purification; var refinery = item.Refinary;
        Assert.Throws<ArgumentOutOfRangeException>(() => item.Send(scope.Pipe, scope.Packet));

        Assert.Empty(scope.Queue); before.AssertUnchanged(scope.Packet);
        Assert.Equal(purification, item.Purification); Assert.Equal(refinery, item.Refinary);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.Equal(Extra.Typing.ExpireTime, item.Purification.Typ);
        Assert.False(item.Refinary.Available);
        Assert.Equal(Extra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
        Assert.Equal(71u, item.Purification.ItemUID);
        Assert.Equal(82u, item.Refinary.ItemUID);
    });
}
