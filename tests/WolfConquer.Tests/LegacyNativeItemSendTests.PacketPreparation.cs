using System.Runtime.InteropServices;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

// Corrected-only owned failure controls; no old oversized/OOB native baseline.
public sealed unsafe partial class LegacyNativeItemSendTests
{
    [Theory]
    [InlineData("null-stream")] [InlineData("short-frame")]
    [InlineData("oversized-relics")] [InlineData("sender-limit")]
    public Task RefusedLockedOutputPreservesActualLockModeTimerAndTarget(string fault) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        using var small = fault == "short-frame" ? InitializedItemPacket(205) : null;
        var target = small ?? scope.Packet;
        var item = Item(); item.Locked = 2; item.Mode = Flags.ItemMode.View; item.Plus = 9;
        if (fault == "oversized-relics") item.RelicAttributes = new LegacyNativeRelicAttribute[5000];
        if (fault == "sender-limit") item.RelicAttributes = new LegacyNativeRelicAttribute[300];
        var relics = item.RelicAttributes; byte[] before = ItemAllocation(target);
        int position = target.Position, size = target.Size;
        if (fault == "null-stream") Assert.Throws<ArgumentNullException>(() => item.SendItemLocked(scope.Client, null!));
        else Assert.Throws<InvalidOperationException>(() => item.SendItemLocked(scope.Client, target));
        Assert.Equal((byte)2, item.Locked); Assert.Equal(Flags.ItemMode.View, item.Mode); Assert.Equal(0, item.UnLockTimer);
        Assert.Equal((byte)9, item.Plus); Assert.Same(relics, item.RelicAttributes);
        Assert.Equal(before, ItemAllocation(target)); Assert.Equal(position, target.Position); Assert.Equal(size, target.Size);
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task FutureUnlockDateDoesNotRequireUnserializedItemStringsOrRelicMetadata() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = Item(); item.Locked = 2; item.Mode = Flags.ItemMode.View;
        item.UnLockTimer = 20991231; item.OwnerName = null!; item.Signature = null!; item.RelicAttributes = null!;
        item.SendItemLocked(scope.Client, scope.Packet);
        byte[] expected = Convert.FromHexString("10001309110000000200000000000000"); Put32(expected, 12, 20991231);
        Assert.Equal(expected, Assert.Single(scope.Queue)); Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
        Assert.Equal((byte)2, item.Locked); Assert.Equal(Flags.ItemMode.View, item.Mode);
        Assert.Equal(20991231, item.UnLockTimer); Assert.Null(item.OwnerName); Assert.Null(item.Signature); Assert.Null(item.RelicAttributes);
    });

    [Theory]
    [InlineData(205, false)] [InlineData(206, true)]
    public Task ItemFrameUsesExactPhysicalCapacityOrPreservesTheWholeOldPacket(int capacity, bool fits) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var target = InitializedItemPacket(capacity); var item = Item();
        byte[] before = ItemAllocation(target);
        if (!fits)
        {
            Assert.Throws<InvalidOperationException>(() => item.ItemCreate(target, item));
            Assert.Equal(before, ItemAllocation(target)); Assert.Equal(capacity - 1, target.Position); Assert.Equal(13, target.Size);
            return;
        }
        item.ItemCreate(target, item); Assert.Equal(PlainItemFrame(), ItemAllocation(target));
        Assert.Equal(206, target.Size); Assert.Equal(4, target.Position);
    });

    [Fact]
    public Task PreparedLockPlanCannotRetargetToANewerMatchingServerIdentity() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); var item = Item(); item.Locked = 2; item.Mode = Flags.ItemMode.View;
        byte[] before = ItemAllocation(scope.Packet);
        using var plan = item.PrepareLockedSend(scope.Client, scope.Packet);
        var admitted = Assert.IsType<LegacyNativeGameItemRuntime.LockedSendPlan>(plan);
        Assert.True(admitted.IsCurrent());
        LegacyNativeGroupServerList.MyServerInfo.ID = 3; scope.Client.Player.ServerID = 3;
        Assert.False(admitted.IsCurrent()); Assert.Equal((byte)2, item.Locked); Assert.Equal(Flags.ItemMode.View, item.Mode);
        Assert.Equal(before, ItemAllocation(scope.Packet)); Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
        Assert.Empty(scope.Queue);
    });

    private static LegacyNativePacket InitializedItemPacket(int capacity)
    {
        var packet = new LegacyNativePacket(capacity);
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, capacity).ToArray(), 0, (nint)packet.Memory, capacity);
        packet.Size = 13; packet.Seek(capacity - 1); return packet;
    }

    private static byte[] ItemAllocation(LegacyNativePacket packet)
    {
        byte[] bytes = new byte[packet.Capacity]; Marshal.Copy((nint)packet.Memory, bytes, 0, bytes.Length); return bytes;
    }
}
