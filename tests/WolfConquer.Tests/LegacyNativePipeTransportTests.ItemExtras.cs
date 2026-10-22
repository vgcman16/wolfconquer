using System.Buffers.Binary;
using Pipe = WolfConquer.Core.World.LegacyNativePipeClientRuntime;
using Extra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePipeTransportTests
{
    [Theory]
    [InlineData(724440u)]
    [InlineData(724444u)]
    [InlineData(3004140u)]
    [InlineData(3006165u)]
    [InlineData(3006170u)]
    public Task PipeItemSendsRawRefineryIdentityAfterItemNotClientEffect301(uint effect) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();
        item.Refinary = Refinery();
        item.Refinary.EffectID = effect;

        item.Send(scope.Pipe, scope.Packet);

        byte[][] frames = scope.Queue.ToArray();
        Assert.Equal(2, frames.Length);
        Assert.Equal(ItemFrame(), frames[0]);
        byte[] expectedExtra = ExtraFrame(2,
            71, 8, 800001, 3, 23, 0, 0, 0,
            82, 3, effect, 5, 11, 22, 0);
        Assert.Equal(expectedExtra, frames[1]);
        Assert.NotSame(frames[0], frames[1]);
        Assert.Equal(expectedExtra, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        Assert.Equal(Extra.Typing.Stabilization, item.Purification.Typ);
        Assert.Equal(Extra.Typing.PermanentRefinery, item.Refinary.Typ);
        Assert.True(PurificationAvailable(item.Purification));
        Assert.True(item.Refinary.Available);
        Assert.Equal(effect, item.Refinary.EffectID);
        Assert.Equal(71u, item.Purification.ItemUID);
        Assert.Equal(82u, item.Refinary.ItemUID);
        // The first queued frame remains independent after the shared writer is reused.
        scope.Packet.InitWriter();
        scope.Packet.Write(0u);
        scope.Packet.Finalize(104);
        Assert.Equal(ItemFrame(), frames[0]);
        Assert.Equal(expectedExtra, frames[1]);
    });

    [Fact]
    public Task NonzeroGateOnOneEffectDoesNotSuppressLiveOtherEffectWithZeroUid() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification(0);
        item.Refinary = Refinery(82);

        item.Send(scope.Pipe, scope.Packet);

        byte[][] frames = scope.Queue.ToArray();
        Assert.Equal(2, frames.Length);
        Assert.Equal(ItemFrame(), frames[0]);
        Assert.Equal(ExtraFrame(2,
            0, 8, 800001, 3, 23, 0, 0, 0,
            82, 3, 724440, 5, 11, 22, 0), frames[1]);
        Assert.True(PurificationAvailable(item.Purification));
        Assert.Equal(0u, item.Purification.ItemUID);
    });

    [Fact]
    public Task RetainedExpiredEffectUidsProduceItemThenRealZeroCountExtraAndClearAvailability() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification();
        Assert.True(item.Purification.InLife);
        item.Purification.AddedOn = DateTime.Now.AddMinutes(-1);
        item.Purification.PurificationDuration = 1;
        item.Refinary = Refinery();
        item.Refinary.Available = true;
        item.Refinary.AddedOn = DateTime.Now.AddMinutes(-1);
        item.Refinary.EffectDuration = 1;

        item.Send(scope.Pipe, scope.Packet);

        byte[][] frames = scope.Queue.ToArray();
        Assert.Equal(2, frames.Length);
        Assert.Equal(ItemFrame(), frames[0]);
        Assert.Equal(ExtraFrame(0), frames[1]);
        Assert.Equal(ExtraFrame(0), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.False(PurificationAvailable(item.Purification));
        Assert.False(item.Refinary.Available);
        Assert.Equal(Extra.Typing.ExpireTime, item.Purification.Typ);
        Assert.Equal(Extra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
        Assert.Equal(71u, item.Purification.ItemUID);
        Assert.Equal(82u, item.Refinary.ItemUID);
    });

    [Fact]
    public Task LiveTimedEffectsUseNativeClockAndAddingTypesWithRawInterserverEffect() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        DateTime start = DateTime.Now.AddSeconds(-7);
        item.Purification = Purification();
        item.Purification.AddedOn = start;
        item.Purification.PurificationDuration = 300;
        item.Refinary = Refinery();
        item.Refinary.AddedOn = start;
        item.Refinary.EffectDuration = 600;
        DateTime earliest = DateTime.Now;

        item.Send(scope.Pipe, scope.Packet);

        DateTime latest = DateTime.Now;
        byte[][] frames = scope.Queue.ToArray();
        Assert.Equal(2, frames.Length);
        Assert.Equal(ItemFrame(), frames[0]);
        uint purificationSeconds = BinaryPrimitives.ReadUInt32LittleEndian(frames[1].AsSpan(32, 4));
        uint refinerySeconds = BinaryPrimitives.ReadUInt32LittleEndian(frames[1].AsSpan(64, 4));
        Assert.InRange(purificationSeconds,
            (uint)((start.AddSeconds(300) - latest).TotalSeconds - 1),
            (uint)((start.AddSeconds(300) - earliest).TotalSeconds + 1));
        Assert.InRange(refinerySeconds,
            (uint)((start.AddSeconds(600) - latest).TotalSeconds - 1),
            (uint)((start.AddSeconds(600) - earliest).TotalSeconds + 1));
        Assert.Equal(ExtraFrame(2,
            71, 6, 800001, 3, 23, 300, purificationSeconds, 0,
            82, 2, 724440, 5, 11, 22, refinerySeconds), frames[1]);
        Assert.Equal(Extra.Typing.PurificationAdding, item.Purification.Typ);
        Assert.Equal(Extra.Typing.RefinaryAdding, item.Refinary.Typ);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DeadPipeOrDeadSocketStillMutatesEffectsAndEncodesFinalExtra(bool alivePipe) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var pipe = alivePipe ? scope.Pipe : new Pipe();
        if (alivePipe) scope.Socket.Alive = false;
        var item = PhysicalItem();
        item.Purification = Purification();
        item.Refinary = Refinery();

        item.Send(pipe, scope.Packet);

        Assert.Empty(scope.Queue);
        Assert.Equal(ExtraFrame(2,
            71, 8, 800001, 3, 23, 0, 0, 0,
            82, 3, 724440, 5, 11, 22, 0), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.True(PurificationAvailable(item.Purification));
        Assert.True(item.Refinary.Available);
        Assert.Equal(Extra.Typing.Stabilization, item.Purification.Typ);
        Assert.Equal(Extra.Typing.PermanentRefinery, item.Refinary.Typ);
        Assert.Equal(alivePipe, pipe.Alive);
        Assert.Equal(4, scope.Packet.Position);
    });
}
