using WolfConquer.Core.Networking;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeMapViewTests
{
    // MODEL test double only, never a production Player, GameMap or host-adoption proof.
    // The quartet must not invoke packet generation, trap detection or sending.
    private class ModelMapObj : ILegacyNativeMapObj
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

        public bool IsTrap() => throw new InvalidOperationException("MODEL trap path must not be used.");
        public LegacyNativePacket GetArray(LegacyNativePacket stream, bool View, bool st) =>
            throw new InvalidOperationException("MODEL packet path must not be used.");
        public void Send(LegacyNativePacket msg) =>
            throw new InvalidOperationException("MODEL send path must not be used.");
    }

    private sealed class OtherModelMapObj : ModelMapObj { }

    private static ModelMapObj Model(uint uid, ushort x = 0, ushort y = 0,
        LegacyNativeMapObjectType type = LegacyNativeMapObjectType.Monster) => new()
    {
        UID = uid,
        X = x,
        Y = y,
        ObjType = type,
    };
}
