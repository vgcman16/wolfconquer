using System.Net;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class OwnedEquipmentBehaviorFixture
{
    // Real next login and native client on the same Frame/runtime. The only
    // injected behavior is one transport failure at the actual status packet.
    internal async Task<(Dictionary<string, byte[]> Files, LegacyOrdinarySpellBinding Binding)> ReconnectWithStartupStatusFailureAsync()
    {
        await connections[actorUid].CompleteTransportCloseAsync();
        var output = frames[actorUid]; output.Clear();
        var cancellation = new CancellationTokenSource(); closed.Add(cancellation);
        var native = new LegacyNativeGameClientRuntime();
        Dictionary<string, byte[]>? captured = null;
        LegacyOrdinarySpellBinding? binding = null;
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
        {
            token.ThrowIfCancellationRequested();
            var bytes = memory.ToArray();
            if (ReadFrame(bytes).Header.Opcode == LegacyPlayerStatusPacket.Opcode)
            {
                captured = Data.Files();
                binding = native.OrdinarySpellBinding;
                throw new IOException("owned pending-startup status transport failure");
            }
            output.Add(bytes);
            return ValueTask.CompletedTask;
        }, _ => { cancellation.Cancel(); return ValueTask.CompletedTask; }, native);
        connections[actorUid] = connection;
        await Handler.HandleFrameAsync(connection, ReadFrame(new LegacyMsgLoginGamePacket(actorUid).ToFrameBytes()), cancellation.Token);
        Assert.NotNull(captured); Assert.NotNull(binding);
        return (captured, binding);
    }
}
