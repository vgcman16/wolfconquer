#nullable disable
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeNpcReply
{
    public static void GetNpcRequest(this LegacyNativePacket stream, out uint npcID, out byte option,
        out byte type, out LegacyNativeNpcServerReplay.Mode action, out string input)
    {
        NpcRequestProto obj = new NpcRequestProto();
        obj = stream.ProtoBufferDeserialize(obj);
        npcID = obj.NpcID;
        type = obj.InteractType;
        input = obj.Input;
        option = obj.OptionID;
        action = (LegacyNativeNpcServerReplay.Mode)obj.Action;
    }

    public static LegacyNativePacket NpcReplyCreate(this LegacyNativePacket stream, InteractTypes interactType,
        string text, ushort InputMaxLength, byte OptionID, bool display = true)
    {
        stream.InitWriter();
        stream.Write(0);
        stream.Write(InputMaxLength);
        stream.Write(OptionID);
        stream.Write((byte)interactType);
        if (display)
        {
            stream.Write(text);
        }
        stream.Finalize(2255);
        return stream;
    }
}
