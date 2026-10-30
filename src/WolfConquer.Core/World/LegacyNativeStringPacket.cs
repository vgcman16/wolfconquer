using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgBuilder2790..2821 string-packet primitive contract.
internal static class LegacyNativeStringPacket
{
    public static void GetStringPacket(this LegacyNativePacket stream, out LegacyNativeMsgStringPacket str)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var parsed = stream.ReadPrepared(packet => new LegacyNativeMsgStringPacket
        {
            UID = packet.ReadUInt32(),
            ID = (LegacyNativeMsgStringPacket.StringID)packet.ReadUInt8(),
            Strings = packet.ReadStringList()
        });
        str = parsed;
    }

    public static LegacyNativePacket StringPacketCreate(this LegacyNativePacket stream, LegacyNativeMsgStringPacket pac)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(pac);
        uint uid = pac.UID;
        var id = pac.ID;
        ushort x = pac.X, y = pac.Y;
        string[]? strings = pac.Strings is { } values ? (string[])values.Clone() : null;
        stream.PrepareFrame(2036, prepared =>
        {
            if (id == LegacyNativeMsgStringPacket.StringID.LocationEffect)
            {
                prepared.Write(x);
                prepared.Write(y);
            }
            else
            {
                prepared.Write(uid);
            }
            prepared.Write((byte)id);
            if (strings is not null) prepared.Write(strings);
            else prepared.ZeroFill(3);
        });
        return stream;
    }
}
