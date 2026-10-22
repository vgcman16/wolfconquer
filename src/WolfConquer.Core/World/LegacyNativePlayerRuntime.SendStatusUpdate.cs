using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Complete native Player4714..4754 overload family, not status-flag adoption.
    internal void SendUpdate(LegacyNativePacket stream, LegacyNativeMsgUpdate.Flags Flag,
        uint Time, uint Dmg, uint Level, LegacyNativeMsgUpdate.DataType datatype, bool scren = false)
    {
        LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
        stream = msgUpdate.Append(stream, datatype, (uint)Flag, Time, Dmg, Level);
        stream = msgUpdate.GetArray(stream);
        Owner.Send(stream);
        if (scren)
        {
            View.SendView(stream, me: false);
        }
    }

    internal void SendUpdate(LegacyNativePacket stream, LegacyNativeMsgUpdate.DataType datatype,
        uint Time, uint Dmg, uint Level, bool scren = false)
    {
        LegacyNativeMsgUpdate msgUpdate = new LegacyNativeMsgUpdate(stream, UID);
        stream = msgUpdate.Append(stream, datatype, 0u, Time, Dmg, Level);
        stream = msgUpdate.GetArray(stream);
        Owner.Send(stream);
        if (scren)
        {
            View.SendView(stream, me: false);
        }
    }

    internal void SendUpdate(uint[] Value, LegacyNativeMsgUpdate.DataType datatype, bool scren = false)
    {
        using LegacyNativeRecycledPacket recycledPacket = new LegacyNativeRecycledPacket();
        LegacyNativePacket stream = recycledPacket.GetStream();
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
