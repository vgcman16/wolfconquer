using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Deliberate parent-reviewed interface adoption only after full genuine GetArray
// and all its typed incoming owner prerequisites are integrated and proven.
// Explicit bridges preserve the existing internal enum/Packet type accessibility.
public sealed partial class LegacyNativePlayerRuntime : ILegacyNativeMapObj
{
    LegacyNativeMapObjectType ILegacyNativeMapObj.ObjType => ObjType;

    LegacyNativePacket ILegacyNativeMapObj.GetArray(LegacyNativePacket stream, bool View, bool st)
    {
        return GetArray(stream, View, st);
    }

    void ILegacyNativeMapObj.Send(LegacyNativePacket msg)
    {
        Send(msg);
    }
}
