using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Fact]
    public async Task OrdinaryPinLoginWithoutActualDisconnectRouteRefusesBeforeAdmission()
    {
        const uint uid = 1297981;
        Seed(uid, "0,0,0");
        var owner = NewOwner();
        var sent = new ConcurrentQueue<byte[]>();
        var native = new LegacyNativeGameClientRuntime();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, token) => { token.ThrowIfCancellationRequested(); sent.Enqueue(bytes.ToArray()); return ValueTask.CompletedTask; },
            nativeGameClient: native);
        using var lifetime = new CancellationTokenSource();
        byte[] character = File.ReadAllBytes(UserPath(uid));
        byte[] items = File.ReadAllBytes(ItemPath(uid));
        try
        {
            await owner.Handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(uid).ToFrameBytes()), lifetime.Token);
            Assert.False(owner.Sessions.TryGet(connection, out _));
            Assert.DoesNotContain(sent, IsAnswerOk);
            Assert.Equal(0U, native.Player.UID);
            Assert.Null(native.OrdinarySpellBinding);
            Assert.Null(native.MySpells); Assert.Null(native.MyProfs);
            Assert.False(native.Player.IsCheckedPass);
            Assert.Equal(character, File.ReadAllBytes(UserPath(uid)));
            Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
            Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
            Assert.Contains("ordinaryAdmissionPreparation:InvalidOperationException", owner.Log.ToString());
        }
        finally
        {
            lifetime.Cancel();
            await connection.CompleteTransportCloseAsync();
        }
    }
}
