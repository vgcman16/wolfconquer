using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgBuilder quest-list primitive methods; no receive/host adoption.
internal static partial class LegacyNativeQuestPacket
{
    public static void GetQuestList(this LegacyNativePacket stream, out LegacyNativeMsgQuestList.QuestMode Mode,
        out ushort Count, out uint QuestID, out LegacyNativeMsgQuestList.QuestListItem.QuestStatus QuestMode, out uint QuestTimer)
    {
        Mode = (LegacyNativeMsgQuestList.QuestMode)stream.ReadUInt16();
        Count = stream.ReadUInt16();
        QuestID = stream.ReadUInt32();
        QuestMode = (LegacyNativeMsgQuestList.QuestListItem.QuestStatus)stream.ReadUInt32();
        QuestTimer = stream.ReadUInt32();
    }

    public static LegacyNativePacket QuestListCreate(this LegacyNativePacket stream, LegacyNativeMsgQuestList.QuestMode Mode, ushort Count)
    {
        stream.InitWriter();
        stream.Write((ushort)Mode);
        stream.Write(Count);
        stream.Write(0uL);
        stream.Write(0uL);
        return stream;
    }

    public static LegacyNativePacket AddItemQuestList(this LegacyNativePacket stream, LegacyNativeMsgQuestList.QuestListItem item)
    {
        stream.Write(item.UID);
        stream.Write((uint)item.Status);
        stream.Write(item.Time);
        return stream;
    }

    public static LegacyNativePacket QuestListFinalize(this LegacyNativePacket stream)
    {
        stream.Finalize(2120);
        return stream;
    }
}
