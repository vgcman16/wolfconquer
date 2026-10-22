using WolfConquer.Core.World;
using Pipe = WolfConquer.Core.World.LegacyNativePipeClientRuntime;
using Extra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePipeTransportTests
{
    [Fact]
    public Task NullPipeFaultOccursAfterItemEncodingButBeforeExpiryMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();

        Assert.Throws<NullReferenceException>(() => item.Send((Pipe)null!, scope.Packet));

        Assert.Equal(ItemFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
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

        Assert.Throws<NullReferenceException>(() => item.Send(scope.Pipe, null!));

        Assert.Equal(237, scope.Packet.Position);
        Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.False(item.Refinary.Available);
    });

    [Fact]
    public Task MissingSocketFaultOccursAfterItemEncodingButBeforeExtras() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();
        scope.Pipe.Socket = null!;

        Assert.Throws<NullReferenceException>(() => item.Send(scope.Pipe, scope.Packet));

        Assert.Equal(ItemFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Empty(scope.Queue);
        Assert.True(scope.Pipe.Alive);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.Equal(Extra.Typing.ExpireTime, item.Purification.Typ);
    });

    [Fact]
    public Task NullRelicArrayPreservesNativeItemWritePrefixWithoutFinalHeaderOrSend() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.RelicAttributes = null!;
        item.Purification = Purification();

        Assert.Throws<NullReferenceException>(() => item.Send(scope.Pipe, scope.Packet));

        Assert.Equal(170, scope.Packet.Position);
        Assert.Equal(321, scope.Packet.Size);
        byte[] partial = NativeSpellTestFixture.Bytes(scope.Packet);
        Assert.All(partial.Take(4), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal(ItemFrame().AsSpan(4, 166).ToArray(), partial.AsSpan(4, 166).ToArray());
        Assert.All(partial.Skip(170), value => Assert.Equal((byte)0xA5, value));
        Assert.Empty(scope.Queue);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.Equal(Extra.Typing.ExpireTime, item.Purification.Typ);
    });

    [Fact]
    public Task PurificationExpiryFaultLeavesAlreadyQueuedItemAndNoExtraWriteOrRefineryVisit() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();
        item.Purification.AddedOn = DateTime.MaxValue;
        item.Purification.PurificationDuration = 1;
        item.Refinary = Refinery();

        Assert.Throws<ArgumentOutOfRangeException>(() => item.Send(scope.Pipe, scope.Packet));

        Assert.Equal(ItemFrame(), Assert.Single(scope.Queue));
        Assert.Equal(ItemFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        Assert.False(PurificationAvailable(item.Purification));
        Assert.False(item.Refinary.Available);
        Assert.Equal(Extra.Typing.ExpireTime, item.Purification.Typ);
        Assert.Equal(Extra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
    });

    [Fact]
    public Task RefineryExpiryFaultRetainsEarlierActualPurificationMutationAndQueuedItemOnly() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();
        item.Refinary = Refinery();
        item.Refinary.AddedOn = DateTime.MaxValue;
        item.Refinary.EffectDuration = 1;

        Assert.Throws<ArgumentOutOfRangeException>(() => item.Send(scope.Pipe, scope.Packet));

        Assert.Equal(ItemFrame(), Assert.Single(scope.Queue));
        Assert.Equal(ItemFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        Assert.True(PurificationAvailable(item.Purification));
        Assert.Equal(Extra.Typing.Stabilization, item.Purification.Typ);
        Assert.False(item.Refinary.Available);
        Assert.Equal(Extra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
        Assert.Equal(71u, item.Purification.ItemUID);
        Assert.Equal(82u, item.Refinary.ItemUID);
    });
}
