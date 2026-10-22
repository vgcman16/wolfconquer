using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Reply = WolfConquer.Core.World.LegacyNativeNpcReply;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDialogPacketTests
{
    [Fact]
    public Task ReplyWriterUsesNativeUtf8ListAndFullWidthsWithoutTextOrEnumValidation() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); const string text = "Wö lf狼\0X";
            Assert.Same(scope.Packet, scope.Packet.NpcReplyCreate((Reply.InteractTypes)255, text, ushort.MaxValue, byte.MaxValue));
            byte[] expected = Npc((Reply.InteractTypes)255, text, ushort.MaxValue, byte.MaxValue);
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position); Assert.Equal(expected.Length, scope.Packet.Size);
            Assert.Equal((byte)0xA5, scope.Raw(expected.Length, 1)[0]); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task DisplayFalseOmitsEntireStringListWhileDisplayedNullHasOneEmptySlotAndInputMaxDoesNotCapText() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Packet.NpcReplyCreate(Reply.InteractTypes.Finish, null!, 0, 0, display: false);
            Assert.Equal(Npc(Reply.InteractTypes.Finish, null, display: false), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(12, scope.Packet.Size); Assert.Equal((byte)0xA5, scope.Raw(12, 1)[0]);
            scope.Packet.NpcReplyCreate(Reply.InteractTypes.Input, null!, 0, 0);
            Assert.Equal(Npc(Reply.InteractTypes.Input, null), NativeSpellTestFixture.Bytes(scope.Packet)); Assert.Equal(15, scope.Packet.Size);
            scope.Packet.NpcReplyCreate(Reply.InteractTypes.Input, "uncapped", 1, 3);
            Assert.Equal(Npc(Reply.InteractTypes.Input, "uncapped", 1, 3), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task ReplyKeepsWrappedUtf8LengthAndNativeStrictBufferLimitWithoutWritingLastAllocatedByte() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            // The entire MAX_SIZE allocation is initialized. This primitive is not sent to a client.
            scope.Packet.NpcReplyCreate(Reply.InteractTypes.Dialog, new string('x', 65536), 0, 0);
            Assert.Equal(LegacyNativePacket.MAX_SIZE - 1, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
            byte[] raw = scope.Raw(0, LegacyNativePacket.MAX_SIZE);
            Assert.Equal((ushort)16383, BinaryPrimitives.ReadUInt16LittleEndian(raw));
            Assert.Equal((ushort)2255, BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2)));
            Assert.Equal(new byte[] { 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0 }, raw[4..15]);
            Assert.All(raw[15..16383], value => Assert.Equal((byte)'x', value));
            Assert.Equal((byte)0xA5, raw[16383]); Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task ReplyFinalizerAppendsActualSealButDeclaredLengthStillExcludesEightSealBytes() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); LegacyNativePacket.SealString = "TQServer";
            scope.Packet.NpcReplyCreate(Reply.InteractTypes.Avatar, "", 123, 9);
            byte[] withoutSeal = Npc(Reply.InteractTypes.Avatar, "", 123, 9);
            Assert.Equal(withoutSeal.Concat(Encoding.ASCII.GetBytes("TQServer")), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(withoutSeal.Length + 8, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
            Assert.Equal((ushort)withoutSeal.Length, BinaryPrimitives.ReadUInt16LittleEndian(scope.Raw(0, 2)));
            Assert.Empty(scope.Native.Queue);
        });

    [Fact]
    public Task NullReplyStreamFaultsAtInitWriterWithoutTouchingAnyOwnedPacket() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Throws<NullReferenceException>(() => Reply.NpcReplyCreate(null!, Reply.InteractTypes.Dialog, "text", 0, 0));
            scope.Untouched(); Assert.Empty(scope.Native.Queue);
        });
}
