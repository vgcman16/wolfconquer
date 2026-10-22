using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeActionPacket
{
    // Native MsgDataPacket55..60. Timestamp is the actual query field; no clock
    // sampling is introduced. Spell removal uses query.Type109, not opcode109.
    public static LegacyNativePacket ActionCreate(this LegacyNativePacket stream, LegacyNativeActionQuery pQuery)
    {
        stream.InitWriter();
        SerializeTypedActionQueryIntoPacket(stream, pQuery);
        stream.Finalize(2046);
        return stream;
    }
}
