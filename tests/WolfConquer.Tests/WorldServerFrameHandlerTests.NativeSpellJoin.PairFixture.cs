using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    internal sealed partial class NativeSpellJoinFixture
    {
        internal async Task<(ConquerTcpConnection Connection, LegacyNativeGameClientRuntime Native, WorldSession Session)> AdmitSecondAsync()
        {
            const uint uid = Uid + 1;
            WriteSnapshot(Users, uid, uid: uid, classCode: 7051, level: 110, reborn: 0, secondClass: 0,
                money: 1000, conquerPoints: 200, boundConquerPoints: 300, minHitPoints: 1000, name: "SecondSpellOwner");
            File.WriteAllBytes(Path.Combine(Spells, uid + ".bin"), PlayerSpellSnapshotLoader.ToLegacyBytes(
                new PlayerSpellSnapshot([new(7001, 2, 50, 1, 2, 1)])));
            var native = new LegacyNativeGameClientRuntime(); var connection = NewConnection(native);
            await Handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(uid).ToFrameBytes()));
            Assert.True(Store.TryGet(connection, out var current));
            return (connection, native, Assert.IsType<WorldSession>(current));
        }
    }
}
