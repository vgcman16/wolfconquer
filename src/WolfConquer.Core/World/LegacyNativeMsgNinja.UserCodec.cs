using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMsgNinja
{
    public const ushort MsgNinjaUser_ID = 2395;

    public static LegacyNativePacket CreateNinjaUser(this LegacyNativePacket stream, MsgNinjaUser obj)
    {
        LegacyNativePreparedPacketStorage.Validate(stream, 4);
        var seal = LegacyNativePreparedPacketFrames.CaptureSeal();
        var frame = PrepareNinjaUserFrame(obj, seal);
        LegacyNativePreparedPacketStorage.Write(stream, frame);
        return stream;
    }
}
