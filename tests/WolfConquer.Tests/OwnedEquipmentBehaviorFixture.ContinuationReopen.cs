using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class OwnedEquipmentBehaviorFixture
{
    // Close the actual failed runtime and reopen the same owned files using the
    // public configured owner. No marker or admission receipt is fabricated.
    internal async Task ReopenContinuationAsync(WorldDataCatalog catalog)
    {
        var old = connections[actorUid];
        await old.CompleteTransportCloseAsync();
        await Runtime.DisposeAsync();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var reopened, out var reason), reason);
        Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(reopened);
        Handler = new WorldServerFrameHandler(Data.Loader, null, catalog, LegacyRuntimeUpdateLoopOptions.Disabled,
            Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
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
