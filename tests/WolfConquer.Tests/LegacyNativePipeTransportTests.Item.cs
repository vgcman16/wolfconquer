using WolfConquer.Core.World;
using Pipe = WolfConquer.Core.World.LegacyNativePipeClientRuntime;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePipeTransportTests
{
    [Fact]
    public Task WholePipeItemSendUsesActualItemAndSuppliedStreamForOneLiteralNativeFrame() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();

        item.Send(scope.Pipe, scope.Packet);

        Assert.Equal(ItemFrame(), Assert.Single(scope.Queue));
        Assert.Equal(ItemFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        Assert.Equal(206, scope.Packet.Size);
        Assert.Equal(17u, item.Uid);
        Assert.Equal(1_000_000u, item.ITEM_ID);
        Assert.Equal((ushort)0, item.StackSize);
        Assert.Equal((ushort)0, item.Position);
        Assert.Equal(Flags.ItemMode.None, item.Mode);
        Assert.Null(scope.Owner.Inventory);
        Assert.Null(LegacyNativePoolItems.ItemsBase);
    });

    [Fact]
    public Task PipeItemSendDoesNotRunGameClientItemNormalizationOrLockedUnlockPath() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.ITEM_ID = 730007;
        item.Plus = 37;
        item.MaximDurability = 0;
        item.Position = ushort.MaxValue;
        item.Mode = Flags.ItemMode.Update;
        item.Locked = 2;
        item.UnLockTimer = 20261301; // Invalid timer would fault in a locked-item route.
        scope.Owner.Player = null!;
        scope.Owner.Fake = true;

        item.Send(scope.Pipe, scope.Packet);

        byte[] expected = ItemFrame(730007, 3, ushort.MaxValue);
        expected[30] = 37;
        expected[40] = 2;
        Assert.Equal(expected, Assert.Single(scope.Queue));
        Assert.Equal((byte)37, item.Plus);
        Assert.Equal((ushort)0, item.MaximDurability);
        Assert.Equal((ushort)0, item.StackSize);
        Assert.Equal(ushort.MaxValue, item.Position);
        Assert.Equal(Flags.ItemMode.Update, item.Mode);
        Assert.Equal((byte)2, item.Locked);
        Assert.Equal(20261301, item.UnLockTimer);
        Assert.Null(LegacyNativePoolItems.ItemsBase);
    });

    [Fact]
    public Task PipeItemSendDoesNotApplyGameClientHugePacketRejection() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.RelicAttributes = new LegacyNativeRelicAttribute[300];

        item.Send(scope.Pipe, scope.Packet);

        var expected = new byte[1386];
        Put16(expected, 0, 1386);
        Put16(expected, 2, 2116);
        Put32(expected, 4, 17);
        Put32(expected, 8, 1_000_000);
        Assert.Equal(expected, Assert.Single(scope.Queue));
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(300, item.RelicAttributes.Length);
        Assert.Equal(4, scope.Packet.Position);
        Assert.Null(scope.Socket.Connection);
    });

    [Fact]
    public Task DeadPipeReturnsBeforeItemMetadataOrCallerOutputWork() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.OwnerName = null!; item.Signature = null!; item.RelicAttributes = null!;
        var dead = new Pipe();
        var before = new PipeItemOutputState(scope.Packet);

        item.Send(dead, scope.Packet);
        item.Send(dead, null!);

        before.AssertUnchanged(scope.Packet);
        Assert.Empty(scope.Queue);
        Assert.False(dead.Alive);
        Assert.Null(dead.Socket);
        Assert.Null(dead.Owner);
        Assert.Null(item.OwnerName); Assert.Null(item.Signature); Assert.Null(item.RelicAttributes);
    });

    [Fact]
    public Task ZeroExtraUidsGateExpiryEvenWhenBothStoredEffectsHaveInvalidDates() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = PhysicalItem();
        item.Purification = Purification(0);
        item.Purification.AddedOn = DateTime.MaxValue;
        item.Purification.PurificationDuration = 1;
        item.Refinary = Refinery(0);
        item.Refinary.AddedOn = DateTime.MaxValue;
        item.Refinary.EffectDuration = 1;

        item.Send(scope.Pipe, scope.Packet);

        Assert.Equal(ItemFrame(), Assert.Single(scope.Queue));
        Assert.Equal(ItemFrame(), NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.False(PurificationAvailable(item.Purification));
        Assert.False(item.Refinary.Available);
        Assert.Equal(LegacyNativeItemExtra.Typing.ExpireTime, item.Purification.Typ);
        Assert.Equal(LegacyNativeItemExtra.Typing.StabilizationEffectRefined, item.Refinary.Typ);
    });
}
