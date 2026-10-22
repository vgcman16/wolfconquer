using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeNpcServerReplay
{
    public static LegacyNativePacket NpcDialog(this LegacyNativePacket stream, out uint npcid, out ushort Mesh,
        out byte option, out byte type, out Mode Action, out string input)
    {
        npcid = stream.ReadUInt32();
        Mesh = stream.ReadUInt16();
        option = stream.ReadUInt8();
        type = stream.ReadUInt8();
        Action = (Mode)stream.ReadUInt8();
        int size = stream.ReadUInt16();
        input = stream.ReadCString(size);
        return stream;
    }

    public static LegacyNativePacket NpcServerCreate(this LegacyNativePacket stream, LegacyNativeNpcServerQuery PQuery)
    {
        stream.InitWriter();
        stream.Write((uint)PQuery.ID);
        stream.Write(PQuery.Mesh);
        stream.Write(PQuery.OptionID);
        stream.Write(PQuery.InteractType);
        stream.Write((ushort)PQuery.Action);
        stream.Write((ushort)PQuery.NpcType);
        stream.Finalize(2084);
        return stream;
    }
}
