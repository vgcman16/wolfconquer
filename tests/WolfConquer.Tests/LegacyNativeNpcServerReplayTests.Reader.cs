using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Replay = WolfConquer.Core.World.LegacyNativeNpcServerReplay;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNpcServerReplayTests
{
    [Fact]
    public Task ReaderUsesCurrentCursorIgnoresManagedSizeAndReadsByteActionThenUnalignedLengthWithoutStringCount() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            byte[] record = Record(uint.MaxValue, ushort.MaxValue, 0xFE, 0xFD, 0xFF, 8,
                Convert.FromHexString("E78BBC007461696C"));
            scope.Seed(record, 237);
            Assert.Same(scope.Packet, scope.Packet.NpcDialog(out uint uid, out ushort mesh, out byte option,
                out byte type, out var action, out string input));
            Assert.Equal(uint.MaxValue, uid); Assert.Equal(ushort.MaxValue, mesh);
            Assert.Equal((byte)0xFE, option); Assert.Equal((byte)0xFD, type);
            Assert.Equal((Replay.Mode)255, action); Assert.Equal("狼", input);
            Assert.Equal(237 + record.Length, scope.Packet.Position); Assert.Equal(1, scope.Packet.Size);
            Assert.Equal(record, scope.Raw(237, record.Length));
            Assert.All(scope.Raw(0, 237), value => Assert.Equal((byte)0xA5, value));
        });

    [Theory]
    [InlineData("", "")]
    [InlineData("00", "")]
    [InlineData("410042", "A")]
    [InlineData("E78BBC", "狼")]
    [InlineData("C328", "\uFFFD(")]
    [InlineData("41FF0042", "A\uFFFD")]
    public Task CStringConsumesFullDeclaredByteCountButReturnsUtf8TextOnlyBeforeFirstZero(string hex, string expected) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); byte[] value = Convert.FromHexString(hex);
            byte[] record = Record(0x89ABCDEF, 0x1234, 17, 18, 5, checked((ushort)value.Length), value);
            scope.Seed(record, 4, managedSize: 0);
            scope.Packet.NpcDialog(out uint uid, out ushort mesh, out byte option, out byte type, out var action, out string input);
            Assert.Equal(0x89ABCDEFu, uid); Assert.Equal((ushort)0x1234, mesh);
            Assert.Equal((byte)17, option); Assert.Equal((byte)18, type);
            Assert.Equal(Replay.Mode.Cursor, action); Assert.Equal(expected, input);
            Assert.Equal(4 + record.Length, scope.Packet.Position); Assert.Equal(0, scope.Packet.Size);
            Assert.Equal(record, scope.Raw(4, record.Length));
        });

    [Theory]
    [InlineData(16370, 2, false)]
    [InlineData(16370, 3, true)]
    [InlineData(16370, 65535, true)]
    public Task CStringStrictPhysicalBoundaryRejectsWholeDeclaredReadWithoutAdvanceOrRepair(int start, int size, bool rejected) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            byte[] record = Record(17, 19, 23, 29, 6, (ushort)size, [0x41, 0x42, 0x43]);
            scope.Seed(record, start);
            scope.Packet.NpcDialog(out uint uid, out ushort mesh, out byte option, out byte type, out var action, out string input);
            Assert.Equal(17u, uid); Assert.Equal((ushort)19, mesh); Assert.Equal((byte)23, option); Assert.Equal((byte)29, type);
            Assert.Equal(Replay.Mode.PlaceFurniture, action);
            Assert.Equal(rejected ? "" : "AB", input);
            Assert.Equal(start + 11 + (rejected ? 0 : size), scope.Packet.Position);
            Assert.Equal(1, scope.Packet.Size); Assert.Equal(record, scope.Raw(start, record.Length));
        });

    [Fact]
    public Task RejectedLengthScalarReturnsZeroWithoutAdvancingAfterEarlierOutputsHaveAlreadyBeenAssigned() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            byte[] record = Record(0x12345678, 0xBEEF, 0xFE, 0xFD, 0xFF, ushort.MaxValue, []);
            scope.Seed(record, LegacyNativePacket.MAX_SIZE - 11);
            scope.Packet.NpcDialog(out uint uid, out ushort mesh, out byte option, out byte type, out var action, out string input);
            Assert.Equal(0x12345678u, uid); Assert.Equal((ushort)0xBEEF, mesh);
            Assert.Equal((byte)0xFE, option); Assert.Equal((byte)0xFD, type); Assert.Equal((Replay.Mode)255, action);
            Assert.Equal("", input); Assert.Equal(LegacyNativePacket.MAX_SIZE - 2, scope.Packet.Position);
            Assert.Equal(record, scope.Raw(LegacyNativePacket.MAX_SIZE - 11, 11));
        });

    [Fact]
    public Task EarlyScalarRejectionDoesNotStopLaterNarrowerReadsAtTheSamePhysicalCursor() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Seed([0x34, 0x12, 0xAB, 0xCD], LegacyNativePacket.MAX_SIZE - 4);
            scope.Packet.NpcDialog(out uint uid, out ushort mesh, out byte option, out byte type, out var action, out string input);
            Assert.Equal(0u, uid); Assert.Equal((ushort)0x1234, mesh); Assert.Equal((byte)0xAB, option);
            Assert.Equal((byte)0, type); Assert.Equal((Replay.Mode)0, action); Assert.Equal("", input);
            Assert.Equal(LegacyNativePacket.MAX_SIZE - 1, scope.Packet.Position);
            Assert.Equal(new byte[] { 0x34, 0x12, 0xAB, 0xCD }, scope.Raw(LegacyNativePacket.MAX_SIZE - 4, 4));
        });

    [Theory]
    [InlineData(16383)]
    [InlineData(16384)]
    public Task AtPhysicalEndEveryScalarIsZeroAndCStringIsEmptyWithoutAnyUnownedRead(int start) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); scope.Seed([], start);
            scope.Packet.NpcDialog(out uint uid, out ushort mesh, out byte option, out byte type, out var action, out string input);
            Assert.Equal(0u, uid); Assert.Equal((ushort)0, mesh); Assert.Equal((byte)0, option); Assert.Equal((byte)0, type);
            Assert.Equal((Replay.Mode)0, action); Assert.Equal("", input); Assert.Equal(start, scope.Packet.Position);
            Assert.Equal(1, scope.Packet.Size);
        });

    [Fact]
    public Task NullReaderStreamFaultRetainsAllCallerOutputsBeforeFirstAssignment() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            uint uid = 91; ushort mesh = 92; byte option = 93, type = 94;
            var action = (Replay.Mode)0xFEDC; string input = "before";
            Assert.Throws<NullReferenceException>(() => Replay.NpcDialog(null!, out uid, out mesh, out option, out type, out action, out input));
            Assert.Equal(91u, uid); Assert.Equal((ushort)92, mesh); Assert.Equal((byte)93, option); Assert.Equal((byte)94, type);
            Assert.Equal((Replay.Mode)0xFEDC, action); Assert.Equal("before", input);
            Assert.Equal(321, scope.Packet.Size); Assert.Equal(237, scope.Packet.Position);
            Assert.All(scope.Raw(0, 321), value => Assert.Equal((byte)0xA5, value));
        });

    [Fact]
    public Task CreatorAndDialogReaderRemainIntentionallyAsymmetricRatherThanInventingARoundTripLayout() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Packet.NpcServerCreate(new LegacyNativeNpcServerQuery
            {
                Stamp = int.MaxValue, ID = LegacyNativeNpcID.TheStorekeeper,
                Mesh = 17, OptionID = 19, InteractType = 23,
                Action = (Replay.Mode)0x0105, NpcType = LegacyNativeNpcType.Stun
            });
            byte[] written = scope.Raw(0, 16);
            scope.Packet.NpcDialog(out uint uid, out ushort mesh, out byte option, out byte type, out var action, out string input);
            Assert.Equal(1u, uid); Assert.Equal((ushort)17, mesh); Assert.Equal((byte)19, option); Assert.Equal((byte)23, type);
            Assert.Equal(Replay.Mode.Cursor, action); // Only 0x05, not producer's ushort 0x0105.
            Assert.Equal("", input); // Length=1 from Action high byte + NpcType low byte; next byte is zero.
            Assert.Equal(16, scope.Packet.Position); Assert.Equal(16, scope.Packet.Size);
            Assert.Equal(written, scope.Raw(0, 16));
        });
}
