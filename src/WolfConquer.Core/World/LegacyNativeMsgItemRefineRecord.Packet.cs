using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgItemRefineRecord
{
    public static LegacyNativePacket ItemRefineRecordCreate(this LegacyNativePacket stream, ItemRefineRecord obj)
    {
        stream.InitWriter();
        stream.ProtoBufferSerialize(obj);
        stream.Finalize(2266);
        return stream;
    }

    public static void GetItemRefineRecord(this LegacyNativePacket stream, out ItemRefineRecord pQuery)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prepared = stream.ProtoBufferDeserialize(new ItemRefineRecord());
        pQuery = prepared;
    }
}
