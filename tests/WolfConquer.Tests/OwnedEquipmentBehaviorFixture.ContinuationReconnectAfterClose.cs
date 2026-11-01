using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class OwnedEquipmentBehaviorFixture
{
    // Caller must have awaited the real old transport close. This creates only
    // a new socket/native owner and sends the actual login on the same Frame.
    internal async Task ReconnectContinuationAfterAwaitedCloseAsync()
    {
        var output = frames[actorUid]; output.Clear();
        var cancellation = new CancellationTokenSource(); closed.Add(cancellation);
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
        {
            token.ThrowIfCancellationRequested(); output.Add(memory.ToArray()); return ValueTask.CompletedTask;
        }, _ => { cancellation.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime());
        connections[actorUid] = connection;
        await Handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(actorUid).ToFrameBytes()), cancellation.Token);
    }
}
