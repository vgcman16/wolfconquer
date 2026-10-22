using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Full native Player4701..4711 scalar overload. Status overloads are in a separate module.
    internal void SendUpdate(LegacyNativePacket stream, long Value,
        LegacyNativeMsgUpdate.DataType datatype, bool scren = false)
    {
        LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
        stream = msgUpdate.Append(stream, datatype, Value);
        stream = msgUpdate.GetArray(stream);
        Owner.Send(stream);
        if (scren)
        {
            View.SendView(stream, me: false);
        }
    }
}
