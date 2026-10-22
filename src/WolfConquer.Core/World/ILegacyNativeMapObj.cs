using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native IMapObj contract; no production Player or serializer stand-in.
internal interface ILegacyNativeMapObj
{
    bool AllowDynamic { get; set; }

    uint UID { get; }

    ushort X { get; }

    ushort Y { get; }

    uint Map { get; }

    uint DynamicID { get; }

    bool Alive { get; }

    uint IndexInScreen { get; set; }

    LegacyNativeMapObjectType ObjType { get; }

    bool IsTrap();

    LegacyNativePacket GetArray(LegacyNativePacket stream, bool View, bool st);

    void Send(LegacyNativePacket msg);
}
