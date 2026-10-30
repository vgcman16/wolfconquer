using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

// Complete native MsgBuilder quest-list primitive methods; no receive/host adoption.
internal static partial class LegacyNativeQuestPacket
{
    public static void GetQuestList(this LegacyNativePacket stream, out LegacyNativeMsgQuestList.QuestMode Mode,
        out ushort Count, out uint QuestID, out LegacyNativeMsgQuestList.QuestListItem.QuestStatus QuestMode, out uint QuestTimer)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prepared = stream.ReadPrepared(static packet =>
        {
            var mode = (LegacyNativeMsgQuestList.QuestMode)packet.ReadUInt16();
            ushort count = packet.ReadUInt16();
            uint id = packet.ReadUInt32();
            var status = (LegacyNativeMsgQuestList.QuestListItem.QuestStatus)packet.ReadUInt32();
            uint timer = packet.ReadUInt32();
            return (Mode: mode, Count: count, Id: id, Status: status, Timer: timer);
        });
        Mode = prepared.Mode;
        Count = prepared.Count;
        QuestID = prepared.Id;
        QuestMode = prepared.Status;
        QuestTimer = prepared.Timer;
    }

    public static LegacyNativePacket QuestListCreate(this LegacyNativePacket stream, LegacyNativeMsgQuestList.QuestMode Mode, ushort Count)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.PrepareWriteAt(4, prepared =>
        {
            prepared.Write((ushort)Mode);
            prepared.Write(Count);
            prepared.Write(0uL);
            prepared.Write(0uL);
        });
        return stream;
    }

    public static LegacyNativePacket AddItemQuestList(this LegacyNativePacket stream, LegacyNativeMsgQuestList.QuestListItem item)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(item);
        uint uid = item.UID, status = (uint)item.Status, time = item.Time;
        stream.PrepareAppend(prepared =>
        {
            prepared.Write(uid);
            prepared.Write(status);
            prepared.Write(time);
        });
        return stream;
    }

    public static LegacyNativePacket QuestListFinalize(this LegacyNativePacket stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Finalize(2120);
        return stream;
    }
}
