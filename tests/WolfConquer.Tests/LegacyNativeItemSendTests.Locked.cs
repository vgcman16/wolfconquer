using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeItemSendTests
{
    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData(byte.MaxValue)]
    public Task OtherLockValuesDoNotDereferenceClientStreamOrServerIdentity(byte locked) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            LegacyNativeGroupServerList.MyServerInfo = null!;
            var item = Item();
            item.Locked = locked;
            item.UnLockTimer = -1;
            item.Mode = Flags.ItemMode.View;

            item.SendItemLocked(null!, null!);

            Assert.Equal(locked, item.Locked);
            Assert.Equal(Flags.ItemMode.View, item.Mode);
            Assert.Equal(-1, item.UnLockTimer);
            Assert.Empty(scope.Queue);
            Assert.Equal(237, scope.Packet.Position);
            AssertTail(scope, 0);
        });

    [Fact]
    public Task ForeignServerIdentityIsComparedWithoutUshortNarrowingAndSkipsInvalidTimer() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            LegacyNativeGroupServerList.MyServerInfo.ID = 0x10002;
            var item = Item();
            item.Locked = 2;
            item.Mode = Flags.ItemMode.Trade;
            item.UnLockTimer = -1;
            Assert.False(scope.Client.Player.OnMyOwnServer);

            item.SendItemLocked(scope.Client, scope.Packet);

            Assert.Equal((byte)2, item.Locked);
            Assert.Equal(Flags.ItemMode.Trade, item.Mode);
            Assert.Empty(scope.Queue);
            Assert.Equal(237, scope.Packet.Position);
            AssertTail(scope, 0);
        });

    [Fact]
    public Task LockedTwoRequiresPhysicalServerIdentityBeforeAnyMutationOrStreamAccess() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            LegacyNativeGroupServerList.MyServerInfo = null!;
            var item = Item();
            item.Locked = 2;
            item.Mode = Flags.ItemMode.Trade;
            Assert.Throws<InvalidOperationException>(() => item.SendItemLocked(scope.Client, null!));
            Assert.Equal((byte)2, item.Locked);
            Assert.Equal(Flags.ItemMode.Trade, item.Mode);
            Assert.Empty(scope.Queue);
            AssertTail(scope, 0);
        });

    [Theory]
    [InlineData(0)]
    [InlineData(20000101)]
    public Task ZeroOrElapsedTimerUnlocksThenQueuesExactUpdateItemAndRetainsTimer(int timer) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = Item();
            item.Locked = 2;
            item.Mode = Flags.ItemMode.View;
            item.UnLockTimer = timer;
            item.Plus = 9; // SendItemLocked is not full Send and must not clear this.

            item.SendItemLocked(scope.Client, scope.Packet);

            byte[] expected = PlainItemFrame(mode: 3);
            expected[30] = 9;
            Assert.Equal(expected, Assert.Single(scope.Queue));
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal((byte)0, item.Locked);
            Assert.Equal(Flags.ItemMode.Update, item.Mode);
            Assert.Equal(timer, item.UnLockTimer);
            Assert.Equal((ushort)0, item.MaximDurability);
            Assert.Equal(4, scope.Packet.Position);
            AssertTail(scope, 206);
        });

    [Fact]
    public Task FutureTimerQueuesNativeUnlockDateFrameWithoutItemMutation() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var item = Item();
        item.Locked = 2;
        item.Mode = Flags.ItemMode.View;
        item.UnLockTimer = 20991231;

        item.SendItemLocked(scope.Client, scope.Packet);

        byte[] expected = Convert.FromHexString("10001309110000000200000000000000");
        Put32(expected, 12, 20991231);
        Assert.Equal(expected, Assert.Single(scope.Queue));
        Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal(16, scope.Packet.Size);
        Assert.Equal(4, scope.Packet.Position);
        Assert.Equal((byte)2, item.Locked);
        Assert.Equal(Flags.ItemMode.View, item.Mode);
        Assert.Equal(20991231, item.UnLockTimer);
        AssertTail(scope, 16);
    });

    [Theory]
    [InlineData(-1)]
    [InlineData(20261301)]
    public Task InvalidNativeDateThrowsBeforeUnlockMutationOrPacketWrites(int timer) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = Item();
            item.Locked = 2;
            item.Mode = Flags.ItemMode.Trade;
            item.UnLockTimer = timer;
            Assert.Throws<ArgumentOutOfRangeException>(() => item.SendItemLocked(scope.Client, scope.Packet));
            Assert.Equal((byte)2, item.Locked);
            Assert.Equal(Flags.ItemMode.Trade, item.Mode);
            Assert.Equal(timer, item.UnLockTimer);
            Assert.Empty(scope.Queue);
            Assert.Equal(237, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
            AssertTail(scope, 0);
        });

    [Fact]
    public Task RefusedItemOutputCannotUnlockOrChangeModeOrPublishAPrefix() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var item = Item();
            item.Locked = 2;
            item.Mode = Flags.ItemMode.View;
            item.OwnerName = null!;

            byte[] before = scope.Memory(16384);
            Assert.Throws<ArgumentNullException>(() => item.SendItemLocked(scope.Client, scope.Packet));

            Assert.Equal((byte)2, item.Locked);
            Assert.Equal(Flags.ItemMode.View, item.Mode);
            Assert.Equal(237, scope.Packet.Position);
            Assert.Equal(321, scope.Packet.Size);
            Assert.Equal(before, scope.Memory(16384));
            Assert.Empty(scope.Queue);
        });
}
