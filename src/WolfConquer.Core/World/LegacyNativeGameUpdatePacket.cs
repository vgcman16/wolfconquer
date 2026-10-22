using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal class LegacyNativeMsgGameUpdate
{
    public enum DataType : uint
    {
        Accelerated = 52u,
        Decelerated = 53u,
        Flustered = 54u,
        Sprint = 55u,
        DivineShield = 57u,
        Stun = 58u,
        Freeze = 59u,
        Dizzy = 60u,
        AzureShield = 93u,
        SoulShacle = 111u
    }
}

internal static class LegacyNativeGameUpdatePacket
{
    // Complete native MsgBuilder1134..1155 primitive contract; no protobuf involved.
    public static LegacyNativePacket GameUpdateCreate(this LegacyNativePacket stream, uint UID,
        LegacyNativeMsgGameUpdate.DataType ID, bool active, uint shownamount, uint time, uint amount = 0u)
    {
        stream.InitWriter();
        stream.Write(UID);
        stream.Write(1u);
        stream.Write((uint)ID);
        stream.Write((!active) ? 1u : 256u);
        stream.Write(shownamount);
        stream.Write(time);
        if (ID == LegacyNativeMsgGameUpdate.DataType.Decelerated)
        {
            stream.Write(uint.MaxValue - amount);
        }
        else
        {
            stream.Write(amount);
        }
        stream.Finalize(2215);
        return stream;
    }
}
