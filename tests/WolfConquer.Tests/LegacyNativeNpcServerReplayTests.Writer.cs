using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Replay = WolfConquer.Core.World.LegacyNativeNpcServerReplay;
using Query = WolfConquer.Core.World.LegacyNativeNpcServerQuery;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNpcServerReplayTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public Task WriterUsesNativeWidthsIgnoresStampResetsCursorAndDoesNotNormalizeUnknownEnumValues(int scenario) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Query query = scenario switch
            {
                0 => default,
                1 => new() { Stamp = int.MinValue, ID = (LegacyNativeNpcID)uint.MaxValue, Mesh = ushort.MaxValue,
                    OptionID = byte.MaxValue, InteractType = byte.MaxValue, Action = (Replay.Mode)ushort.MaxValue, NpcType = (LegacyNativeNpcType)ushort.MaxValue },
                _ => new() { Stamp = int.MaxValue, ID = (LegacyNativeNpcID)0x89ABCDEFu, Mesh = 0xBEEF,
                    OptionID = 0xFE, InteractType = 0xFD, Action = (Replay.Mode)0xFEDC, NpcType = (LegacyNativeNpcType)0xFDFB }
            };
            byte[] expected = Convert.FromHexString(scenario switch
            {
                0 => "10002408000000000000000000000000",
                1 => "10002408FFFFFFFFFFFFFFFFFFFFFFFF",
                _ => "10002408EFCDAB89EFBEFEFDDCFEFBFD"
            });
            Query retained = query;
            Assert.Same(scope.Packet, scope.Packet.NpcServerCreate(query));
            Assert.Equal(expected, scope.Raw(0, 16)); Assert.Equal(16, scope.Packet.Size); Assert.Equal(4, scope.Packet.Position);
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 8), scope.Raw(16, 8));
            Assert.Equal(retained.Stamp, query.Stamp); Assert.Equal(retained.ID, query.ID); Assert.Equal(retained.Mesh, query.Mesh);
            Assert.Equal(retained.OptionID, query.OptionID); Assert.Equal(retained.InteractType, query.InteractType);
            Assert.Equal(retained.Action, query.Action); Assert.Equal(retained.NpcType, query.NpcType);
            query.Stamp = unchecked(~query.Stamp);
            Assert.Same(scope.Packet, scope.Packet.NpcServerCreate(query)); Assert.Equal(expected, scope.Raw(0, 16));
        });

    [Fact]
    public Task WriterUsesActualSealWhileDeclaredLengthStillExcludesItsEightBytes() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope(); LegacyNativePacket.SealString = "TQServer";
            scope.Packet.NpcServerCreate(new Query { ID = LegacyNativeNpcID.TheStorekeeper, Action = Replay.Mode.Cursor, NpcType = LegacyNativeNpcType.Talker });
            byte[] body = Convert.FromHexString("10002408010000000000000005000200");
            Assert.Equal(body.Concat(Encoding.ASCII.GetBytes("TQServer")), scope.Raw(0, 24));
            Assert.Equal(24, scope.Packet.Size); Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(scope.Raw(0, 2)));
            Assert.Equal(4, scope.Packet.Position); Assert.Equal((byte)0xA5, Assert.Single(scope.Raw(24, 1)));
        });

    [Fact]
    public Task NullWriterStreamFaultsBeforeQuerySerializationAndLeavesOwnedPacketUntouched() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var query = new Query { Stamp = 17, ID = LegacyNativeNpcID.TheStorekeeper };
            Assert.Throws<NullReferenceException>(() => Replay.NpcServerCreate(null!, query));
            Assert.Equal(17, query.Stamp); Assert.Equal(LegacyNativeNpcID.TheStorekeeper, query.ID);
            Assert.Equal(321, scope.Packet.Size); Assert.Equal(237, scope.Packet.Position);
            Assert.All(scope.Raw(0, 321), value => Assert.Equal((byte)0xA5, value));
        });
}
