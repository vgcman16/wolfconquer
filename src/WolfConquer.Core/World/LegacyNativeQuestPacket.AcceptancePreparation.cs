using System.Buffers.Binary;
using System.Text;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeQuestPacket
{
    // Complete source QuestListCreate(AcceptQuest,1)+AddItemQuestList+Finalize2120.
    // The intention array is state only; it is not a tail in this wire contract.
    internal static byte[] PrepareQuestAcceptanceFrame(uint uid, uint time, string? seal)
    {
        if (seal is not null && seal.Length != 8) throw new InvalidDataException("Quest packet seal is invalid.");
        var frame = new byte[36 + (seal is null ? 0 : 8)];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, 36);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2120);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), (ushort)LegacyNativeMsgQuestList.QuestMode.AcceptQuest);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(24), uid);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(28), (uint)LegacyNativeMsgQuestList.QuestListItem.QuestStatus.Accepted);
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(32), time);
        if (seal is not null) Buffer.BlockCopy(Encoding.UTF8.GetBytes(seal), 0, frame, 36, 8);
        return frame;
    }
}
