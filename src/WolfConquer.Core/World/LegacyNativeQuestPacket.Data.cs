using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeQuestPacket
{
    public static void GetQuestData(this LegacyNativePacket stream, out uint UnKnow, out uint UID, out uint[] intents)
    {
        UnKnow = stream.ReadUInt32();
        UID = stream.ReadUInt32();
        intents = new uint[1];
        intents[0] = stream.ReadUInt32();
    }

    public static LegacyNativePacket MsgQuestDataCreate(this LegacyNativePacket stream, uint UnKnow, uint UID, params uint[] intents)
    {
        stream.InitWriter();
        stream.Write((ushort)0);
        stream.Write(unchecked((ushort)intents.Length));
        stream.Write(UID);
        for (int i = 0; i < intents.Length; i++)
        {
            stream.Write(intents[i]);
        }
        if (UID == 35024)
        {
            for (int j = 0; j < intents.Length; j++)
            {
                stream.Write(intents[j]);
            }
        }
        stream.Write(0);
        if (UID == 35007)
        {
            stream.ZeroFill(12);
            for (int k = 0; k < intents.Length; k++)
            {
                stream.Write(intents[k]);
            }
        }
        stream.Finalize(2136);
        return stream;
    }

    public static LegacyNativePacket MsgQuestDataCreate(this LegacyNativePacket stream, uint UnKnow, uint UID,
        uint OwnerUID, params uint[] intents)
    {
        stream.InitWriter();
        stream.Write((ushort)0);
        stream.Write(unchecked((ushort)intents.Length));
        stream.Write(UID);
        for (int i = 0; i < intents.Length; i++)
        {
            stream.Write(intents[i]);
        }
        stream.Finalize(2136);
        return stream;
    }
}
