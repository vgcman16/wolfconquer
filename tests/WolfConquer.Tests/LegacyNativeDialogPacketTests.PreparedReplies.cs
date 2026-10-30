using System.Runtime.InteropServices;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Reply = WolfConquer.Core.World.LegacyNativeNpcReply;

namespace WolfConquer.Tests;

// Corrected-only owned controls; this package never executes old oversized native writes.
public sealed unsafe partial class LegacyNativeDialogPacketTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ReplyUsesAnExactTinyAllocationIncludingItsActualSeal(bool seal) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); if (seal) LegacyNativePacket.SealString = "TQServer";
        using var packet = InitializedReplyPacket(seal ? 27 : 19);
        packet.NpcReplyCreate(Reply.InteractTypes.Dialog, "wolf", 7, 3);
        byte[] expected = Npc(Reply.InteractTypes.Dialog, "wolf", 7, 3);
        if (seal) expected = expected.Concat(Encoding.ASCII.GetBytes("TQServer")).ToArray();
        Assert.Equal(expected, ReplyAllocation(packet)); Assert.Equal(expected.Length, packet.Size); Assert.Equal(4, packet.Position);
        Assert.Empty(scope.Native.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ReplyRefusesAWholeFrameOneByteBeyondItsCapacityWithoutDirtyOutput(bool seal) => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); if (seal) LegacyNativePacket.SealString = "TQServer";
        int capacity = seal ? 26 : 18;
        using var packet = InitializedReplyPacket(capacity); byte[] before = ReplyAllocation(packet);
        Assert.Throws<InvalidOperationException>(() => packet.NpcReplyCreate(Reply.InteractTypes.Dialog, "wolf", 7, 3));
        Assert.Equal(before, ReplyAllocation(packet)); Assert.Equal(7, packet.Size); Assert.Equal(capacity - 1, packet.Position);
        Assert.Empty(scope.Native.Queue);
    });

    [Fact]
    public Task HiddenOversizedTextRemainsOmittedRatherThanValidatedAsDisplayedData() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = InitializedReplyPacket(12);
        packet.NpcReplyCreate(Reply.InteractTypes.Finish, new string('x', 65536), 0, 0, display: false);
        Assert.Equal(Npc(Reply.InteractTypes.Finish, null, display: false), ReplyAllocation(packet));
        Assert.Equal(12, packet.Size); Assert.Equal(4, packet.Position); Assert.Empty(scope.Native.Queue);
    });

    [Fact]
    public Task DisposedReplyTargetRetainsItsMetadataWithoutNativeAccess() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope(); using var packet = InitializedReplyPacket(19);
        nuint cursor = (nuint)packet.stream; packet.Dispose();
        Assert.Throws<ObjectDisposedException>(() => packet.NpcReplyCreate(Reply.InteractTypes.Dialog, "wolf", 0, 0));
        Assert.Equal(cursor, (nuint)packet.stream); Assert.Equal(7, packet.Size); Assert.Equal((nuint)0, (nuint)packet.Memory);
        Assert.Empty(scope.Native.Queue);
    });

    private static LegacyNativePacket InitializedReplyPacket(int capacity)
    {
        var packet = new LegacyNativePacket(capacity);
        Marshal.Copy(Enumerable.Repeat((byte)0xA5, capacity).ToArray(), 0, (nint)packet.Memory, capacity);
        packet.Size = 7; packet.Seek(capacity - 1);
        return packet;
    }

    private static byte[] ReplyAllocation(LegacyNativePacket packet)
    {
        var bytes = new byte[packet.Capacity];
        Marshal.Copy((nint)packet.Memory, bytes, 0, bytes.Length);
        return bytes;
    }
}
