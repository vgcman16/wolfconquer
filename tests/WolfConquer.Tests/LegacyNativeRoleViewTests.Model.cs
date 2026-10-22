using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRoleViewTests
{
    // MODEL only: explicit test map publication is not native LoadMap or host adoption.
    private static LegacyNativeGameClientRuntime Client(ushort x = 18, ushort y = 18)
    {
        var client = new LegacyNativeGameClientRuntime();
        client.Player.UID = 1;
        client.Player.Map = 1002;
        client.Player.DynamicID = 7;
        client.Player.X = x;
        client.Player.Y = y;
        return client;
    }

    private static LegacyNativeMapView PublishModelView(LegacyNativeGameClientRuntime client)
    {
        var map = new LegacyNativeGameMap(72, 72, 1002);
        var view = new LegacyNativeMapView(72, 72);
        map.View = view;
        client.Map = map;
        return view;
    }

    private static ModelMapObj Model(uint uid, ushort x = 18, ushort y = 18,
        LegacyNativeMapObjectType type = LegacyNativeMapObjectType.Player) => new()
    {
        UID = uid,
        Map = 1002,
        DynamicID = 7,
        X = x,
        Y = y,
        ObjType = type,
        Alive = true,
    };

    // Not a Player subclass. Packet production/trap methods must never be consulted here.
    private sealed class ModelMapObj : ILegacyNativeMapObj
    {
        public bool AllowDynamic { get; set; }
        public uint UID { get; set; }
        public ushort X { get; set; }
        public ushort Y { get; set; }
        public uint Map { get; set; }
        public uint DynamicID { get; set; }
        public bool Alive { get; set; }
        public uint IndexInScreen { get; set; }
        public LegacyNativeMapObjectType ObjType { get; set; }
        public Action<LegacyNativePacket>? OnSend;

        public bool IsTrap() => throw new InvalidOperationException("MODEL trap path is not implemented.");
        public LegacyNativePacket GetArray(LegacyNativePacket stream, bool View, bool st) =>
            throw new InvalidOperationException("MODEL spawn serialization is not implemented.");
        public void Send(LegacyNativePacket msg) =>
            (OnSend ?? throw new InvalidOperationException("Unexpected MODEL send."))(msg);
    }
}
