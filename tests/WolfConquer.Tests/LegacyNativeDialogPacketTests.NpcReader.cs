using System.Buffers.Binary;
using WolfConquer.Core.World;
using Reply = WolfConquer.Core.World.LegacyNativeNpcReply;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDialogPacketTests
{
    [Fact]
    public Task RequestDecoderUsesDeclaredFrameFromZeroWithinCapturedInputAndPreservesEveryScalarWidth() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            byte[] frame = Frame(9999, Convert.FromHexString("08FFFF0310FFFFFFFF0F18FF0120FF012A03E78BBC"));
            scope.Input(frame);
            scope.Packet.GetNpcRequest(out uint uid, out byte option, out byte type, out var action, out string input);
            Assert.Equal(uint.MaxValue, uid); Assert.Equal(byte.MaxValue, option); Assert.Equal(byte.MaxValue, type);
            Assert.Equal((LegacyNativeNpcServerReplay.Mode)ushort.MaxValue, action); Assert.Equal("狼", input);
            Assert.Equal(frame.Length, scope.Packet.Position); Assert.Equal(frame.Length, scope.Packet.Size);
            Assert.Equal(frame, scope.Raw(0, frame.Length)); Assert.Empty(scope.Native.Queue);
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RequestDecoderAllowsAbsentRequiredScalarsAndDistinguishesAbsentFromEmptyInput(bool emptyInput) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); byte[] payload = emptyInput ? [0x2A, 0] : [];
            scope.Input(Frame(2255, payload));
            uint uid = 91; byte option = 92, type = 93; var action = LegacyNativeNpcServerReplay.Mode.Cursor; string input = "before";
            scope.Packet.GetNpcRequest(out uid, out option, out type, out action, out input);
            Assert.Equal(0u, uid); Assert.Equal((byte)0, option); Assert.Equal((byte)0, type);
            Assert.Equal((LegacyNativeNpcServerReplay.Mode)0, action);
            if (emptyInput) Assert.Equal("", input); else Assert.Null(input);
            Assert.Equal(4 + payload.Length, scope.Packet.Position); Assert.Empty(scope.Native.Queue);
        });

    [Theory]
    [InlineData("null")]
    [InlineData("negative")]
    [InlineData("truncated")]
    public Task DecodeFailureRetainsAllCallerOutputsAndOriginalNativeCursor(string fault) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            if (fault == "negative")
            {
                byte[] header = Frame(2255, []); BinaryPrimitives.WriteUInt16LittleEndian(header, 3); scope.Input(header);
            }
            else if (fault == "truncated") scope.Input(Frame(2255, [0x08]));
            uint uid = 91; byte option = 92, type = 93; var action = LegacyNativeNpcServerReplay.Mode.Cursor; string input = "before";
            void Decode() => Reply.GetNpcRequest(fault == "null" ? null! : scope.Packet, out uid, out option, out type, out action, out input);
            if (fault == "null") Assert.Throws<NullReferenceException>(Decode);
            else if (fault == "negative") Assert.Throws<InvalidDataException>(Decode);
            else Assert.Throws<EndOfStreamException>(Decode);
            Assert.Equal(91u, uid); Assert.Equal((byte)92, option); Assert.Equal((byte)93, type);
            Assert.Equal(LegacyNativeNpcServerReplay.Mode.Cursor, action); Assert.Equal("before", input);
            Assert.Equal(237, scope.Packet.Position);
            Assert.Empty(scope.Native.Queue);
        });
}
