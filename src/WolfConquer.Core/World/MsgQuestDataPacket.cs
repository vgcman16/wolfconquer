using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct MsgQuestDataObjectiveFrame(
    ushort Unknown,
    ushort Count,
    uint Uid,
    IReadOnlyList<uint> Intentions,
    IReadOnlyList<uint> ExtraValues);

public readonly record struct MsgQuestDataTaskDetailRecord(
    uint Uid,
    IReadOnlyList<uint> Intentions);

public readonly record struct MsgQuestDataTaskDetailFrame(
    ushort Control,
    IReadOnlyList<MsgQuestDataTaskDetailRecord> Records);

public readonly record struct MsgQuestDataPacket(uint Unknown, uint Uid, uint Intent)
{
    private const int UnknownOffset = 0;
    private const int UidOffset = UnknownOffset + sizeof(uint);
    private const int IntentOffset = UidOffset + sizeof(uint);
    private const int ObjectiveUnknownOffset = 0;
    private const int ObjectiveCountOffset = ObjectiveUnknownOffset + sizeof(ushort);
    private const int ObjectiveUidOffset = ObjectiveCountOffset + sizeof(ushort);
    private const int ObjectiveIntentionsOffset = ObjectiveUidOffset + sizeof(uint);
    private const int TaskDetailControlOffset = 0;
    private const int TaskDetailCountOffset = TaskDetailControlOffset + sizeof(ushort);
    private const int TaskDetailRecordsOffset = TaskDetailCountOffset + sizeof(ushort);
    private const int TaskDetailIntentionCount = 7;
    private const int TaskDetailRecordPayloadLength = sizeof(uint) + (TaskDetailIntentionCount * sizeof(uint));

    public const ushort Opcode = WorldPacketOpcodes.MsgQuestData;
    public const int PayloadLength = IntentOffset + sizeof(uint);
    public const int ObjectiveHeaderPayloadLength = ObjectiveIntentionsOffset;
    public const int LatestClientPayloadLength = TaskDetailRecordsOffset + TaskDetailRecordPayloadLength;

    public static bool TryParse(PacketFrame frame, out MsgQuestDataPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new MsgQuestDataPacket(
            reader.ReadUInt32(UnknownOffset),
            reader.ReadUInt32(UidOffset),
            reader.ReadUInt32(IntentOffset));
        return true;
    }

    public static bool TryParseTaskDetailFrame(
        PacketFrame frame,
        out MsgQuestDataTaskDetailFrame packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < TaskDetailRecordsOffset ||
            (frame.Payload.Length - TaskDetailRecordsOffset) % TaskDetailRecordPayloadLength != 0 ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var control = reader.ReadUInt16(TaskDetailControlOffset);
        var count = reader.ReadUInt16(TaskDetailCountOffset);
        if (control != 0 ||
            frame.Payload.Length != TaskDetailRecordsOffset + (count * TaskDetailRecordPayloadLength))
        {
            return false;
        }

        var records = new MsgQuestDataTaskDetailRecord[count];
        var offset = TaskDetailRecordsOffset;
        for (var recordIndex = 0; recordIndex < records.Length; recordIndex++)
        {
            var uid = reader.ReadUInt32(offset);
            offset += sizeof(uint);
            var intentions = new uint[TaskDetailIntentionCount];
            for (var intentionIndex = 0; intentionIndex < intentions.Length; intentionIndex++)
            {
                intentions[intentionIndex] = reader.ReadUInt32(offset);
                offset += sizeof(uint);
            }

            records[recordIndex] = new MsgQuestDataTaskDetailRecord(uid, intentions);
        }

        packet = new MsgQuestDataTaskDetailFrame(control, records);
        return true;
    }

    public static bool TryParseObjectiveFrame(PacketFrame frame, out MsgQuestDataObjectiveFrame packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < ObjectiveHeaderPayloadLength ||
            (frame.Payload.Length - ObjectiveHeaderPayloadLength) % sizeof(uint) != 0 ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var count = reader.ReadUInt16(ObjectiveCountOffset);
        var intentionPayloadLength = checked(count * sizeof(uint));
        if (frame.Payload.Length < ObjectiveHeaderPayloadLength + intentionPayloadLength)
        {
            return false;
        }

        var intentions = new uint[count];
        var offset = ObjectiveIntentionsOffset;
        for (var index = 0; index < intentions.Length; index++)
        {
            intentions[index] = reader.ReadUInt32(offset);
            offset += sizeof(uint);
        }

        var extraCount = (frame.Payload.Length - offset) / sizeof(uint);
        var extraValues = new uint[extraCount];
        for (var index = 0; index < extraValues.Length; index++)
        {
            extraValues[index] = reader.ReadUInt32(offset);
            offset += sizeof(uint);
        }

        packet = new MsgQuestDataObjectiveFrame(
            reader.ReadUInt16(ObjectiveUnknownOffset),
            count,
            reader.ReadUInt32(ObjectiveUidOffset),
            intentions,
            extraValues);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UnknownOffset, Unknown);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteUInt32(IntentOffset, Intent);
        return writer.ToArray();
    }

    public static byte[] CreateObjectiveFrame(PlayerQuestSnapshotEntry quest)
    {
        ArgumentNullException.ThrowIfNull(quest);

        return CreateObjectiveFrame(quest.Uid, quest.Intentions);
    }

    public static byte[] CreateObjectiveFrame(uint uid, IReadOnlyList<uint> intentions)
    {
        ArgumentNullException.ThrowIfNull(intentions);

        if (intentions.Count > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(intentions), "Legacy quest objective frames use a ushort count.");
        }

        var extraValueCount = 1;
        if (uid == 35_024)
        {
            extraValueCount += intentions.Count;
        }

        if (uid == 35_007)
        {
            extraValueCount += 3 + intentions.Count;
        }

        var payloadLength = checked(
            ObjectiveHeaderPayloadLength + ((intentions.Count + extraValueCount) * sizeof(uint)));
        var writer = new PacketWriter(Opcode, payloadLength);
        writer.WriteUInt16(ObjectiveUnknownOffset, 0);
        writer.WriteUInt16(ObjectiveCountOffset, checked((ushort)intentions.Count));
        writer.WriteUInt32(ObjectiveUidOffset, uid);

        var offset = ObjectiveIntentionsOffset;
        foreach (var intention in intentions)
        {
            writer.WriteUInt32(offset, intention);
            offset += sizeof(uint);
        }

        if (uid == 35_024)
        {
            foreach (var intention in intentions)
            {
                writer.WriteUInt32(offset, intention);
                offset += sizeof(uint);
            }
        }

        writer.WriteUInt32(offset, 0);
        offset += sizeof(uint);

        if (uid == 35_007)
        {
            for (var index = 0; index < 3; index++)
            {
                writer.WriteUInt32(offset, 0);
                offset += sizeof(uint);
            }

            foreach (var intention in intentions)
            {
                writer.WriteUInt32(offset, intention);
                offset += sizeof(uint);
            }
        }

        return writer.ToArray();
    }
}
