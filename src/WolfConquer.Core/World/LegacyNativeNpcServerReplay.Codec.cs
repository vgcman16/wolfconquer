using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeNpcServerReplay
{
    public static LegacyNativePacket NpcDialog(this LegacyNativePacket stream, out uint npcid, out ushort Mesh,
        out byte option, out byte type, out Mode Action, out string input)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prepared = stream.ReadPrepared(static packet =>
        {
            uint parsedNpcId = packet.ReadUInt32();
            ushort parsedMesh = packet.ReadUInt16();
            byte parsedOption = packet.ReadUInt8();
            byte parsedType = packet.ReadUInt8();
            var parsedAction = (Mode)packet.ReadUInt8();
            int length = packet.ReadUInt16();
            string parsedInput = packet.ReadCString(length);
            return (NpcId: parsedNpcId, Mesh: parsedMesh, Option: parsedOption,
                Type: parsedType, Action: parsedAction, Input: parsedInput);
        });
        npcid = prepared.NpcId;
        Mesh = prepared.Mesh;
        option = prepared.Option;
        type = prepared.Type;
        Action = prepared.Action;
        input = prepared.Input;
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
