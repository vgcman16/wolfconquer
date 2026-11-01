using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class OwnedEquipmentBehaviorFixture
{
    // Real fresh connection and native client, retaining the same configured
    // Frame/Store/runtime. The original failed send remains recorded by the
    // fixture; this direct Frame dispatch does not erase that observation.
    internal async Task ReconnectContinuationAsync()
    {
        await connections[actorUid].CompleteTransportCloseAsync();
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
