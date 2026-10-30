using System.Buffers.Binary;
using WolfConquer.Core.World;
using Query = WolfConquer.Core.World.LegacyNativeMsgNinja.MsgNinjaInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaInfoBehaviorTests
{
    [Fact]
    public Task NullStreamGetPreservesTheOriginalOutObject() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new PacketScope();
            var old = new Query { Type = Query.TypeID.Hat, ID = 1, Points = 2, Position = 3 };
            Query output = old;
            Assert.Throws<ArgumentNullException>(() => LegacyNativeMsgNinja.GetNinjaInfo(null!, out output));
            Assert.Same(old, output);
            AssertValues(new Query { Type = Query.TypeID.Hat, ID = 1, Points = 2, Position = 3 }, old);
            Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
            scope.NoRecycleOrSend();
        });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public Task ShortDeclaredLengthFaultKeepsTheOriginalOutAndCallerCursor(int length) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new PacketScope();
            var old = new Query { Type = Query.TypeID.InstantUpgrade, ID = 123 };
            Query output = old;
            byte[] input = [0, 0, 90, 9];
            BinaryPrimitives.WriteUInt16LittleEndian(input, (ushort)length);
            Copy(scope.Packet, input); scope.Packet.Seek(237);
            Assert.Throws<InvalidDataException>(() => scope.Packet.GetNinjaInfo(out output));
            Assert.Same(old, output); AssertValues(new Query { Type = Query.TypeID.InstantUpgrade, ID = 123 }, output);
            Assert.Equal(Query.TypeID.InstantUpgrade, old.Type); Assert.Equal(123u, old.ID);
            Assert.Equal(237, scope.Packet.Position); Assert.Equal(4, scope.Packet.Size);
            Assert.Equal(input, NativeSpellTestFixture.Bytes(scope.Packet));
            scope.NoRecycleOrSend();
        });

    [Fact]
    public Task NullStreamEncoderFaultsBeforeAnyOwnedPacketStateOrRecycleMutation() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new PacketScope();
            var source = new Query { Type = Query.TypeID.AddPoints, ID = 1, Points = 2, Position = 3 };
            Assert.Throws<NullReferenceException>(() => LegacyNativeMsgNinja.CreateNinjaInfo(null!, source));
            AssertValues(new Query { Type = Query.TypeID.AddPoints, ID = 1, Points = 2, Position = 3 }, source);
            Assert.Equal(237, scope.Packet.Position); Assert.Equal(321, scope.Packet.Size);
            scope.NoRecycleOrSend();
        });
}
