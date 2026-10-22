using System.Buffers.Binary;
using WolfConquer.Core.World;
using Query = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaInfoBehaviorTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public Task InfoCodecKeepsFullUIntFieldsAndUnknownSignedFlagsWithActualDecoder(int type) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new PacketScope();
            var source = new Query { Type = (Query.TypeID)type, ID = uint.MaxValue, Points = 17, Position = uint.MaxValue };
            byte[] expected = Expected(source);
            Assert.Same(scope.Packet, scope.Packet.CreateNinjaInfo(source));
            Assert.Equal(expected, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position); Assert.Equal(expected.Length, scope.Packet.Size); Tail(scope.Packet);
            scope.Packet.Seek(237);
            scope.Packet.GetNinjaInfo(out var parsed);
            Assert.NotSame(source, parsed); AssertValues(source, parsed);
            Assert.Equal(expected.Length, scope.Packet.Position); Assert.Equal(expected.Length, scope.Packet.Size);
            AssertValues(new Query { Type = (Query.TypeID)type, ID = uint.MaxValue, Points = 17, Position = uint.MaxValue }, source);
            scope.NoRecycleOrSend();
        });

    [Fact]
    public Task DefaultInfoResetsWriterAndEmitsEveryRequiredZeroWithoutTouchingTailOrRecycleBin() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new PacketScope();
            Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
            Assert.Same(scope.Packet, scope.Packet.CreateNinjaInfo(new Query()));
            Assert.Equal(Frame(Convert.FromHexString("0800100018002000")), NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(12, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position); Tail(scope.Packet);
            scope.NoRecycleOrSend();
        });

    [Fact]
    public Task NullInfoObjectProducesOnlyNativeHeaderAfterWriterReset() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new PacketScope();
            Assert.Same(scope.Packet, scope.Packet.CreateNinjaInfo(null!));
            Assert.Equal(new byte[] { 4, 0, 90, 9 }, NativeSpellTestFixture.Bytes(scope.Packet));
            Assert.Equal(4, scope.Packet.Position); Assert.Equal(4, scope.Packet.Size); Tail(scope.Packet);
            scope.NoRecycleOrSend();
        });

    [Fact]
    public Task ReencodingReadsLiveMutableFieldsWithoutChangingTheEarlierDecodedObject() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new PacketScope();
            var source = new Query { Type = Query.TypeID.UpLevel, ID = 1, Points = 2, Position = 3 };
            scope.Packet.CreateNinjaInfo(source);
            scope.Packet.GetNinjaInfo(out var first);
            source.Type = Query.TypeID.Hat; source.ID = uint.MaxValue; source.Points = uint.MaxValue; source.Position = uint.MaxValue;
            scope.Packet.CreateNinjaInfo(source);
            Assert.Equal(Expected(source), NativeSpellTestFixture.Bytes(scope.Packet));
            scope.Packet.GetNinjaInfo(out var second);
            AssertValues(source, second); Assert.NotSame(source, second); Assert.NotSame(first, second);
            AssertValues(new Query { Type = Query.TypeID.UpLevel, ID = 1, Points = 2, Position = 3 }, first);
            scope.NoRecycleOrSend();
        });

    [Fact]
    public Task DecoderResetsCursorIgnoresOpcodeAndConsumesDeclaredBodyWithoutTrailingGarbage()
        => NativePacketTestThread.Run(() =>
        {
            using var scope = new PacketScope();
            var wanted = new Query { Type = Query.TypeID.BoostAptitude, ID = 5, Points = 6, Position = 7 };
            byte[] frame = Expected(wanted);
            byte[] input = frame.Concat(new byte[] { 0xFF, 0xFF, 0xFF }).ToArray();
            BinaryPrimitives.WriteUInt16LittleEndian(input.AsSpan(2), 9999);
            Copy(scope.Packet, input); scope.Packet.Seek(300);
            scope.Packet.GetNinjaInfo(out var parsed);
            AssertValues(wanted, parsed); Assert.NotSame(wanted, parsed);
            Assert.Equal(frame.Length, scope.Packet.Position); Assert.Equal(input.Length, scope.Packet.Size);
            Assert.Equal(input, NativeSpellTestFixture.Bytes(scope.Packet));
            scope.NoRecycleOrSend();
        });
}
