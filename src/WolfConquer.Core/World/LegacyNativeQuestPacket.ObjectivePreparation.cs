using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeQuestPacket
{
    // Complete source first-overload2136 layout, including both UID-specific
    // repeated intention tails. One captured seal avoids Finalize's mutable reread.
    internal static int GetQuestObjectiveFrameLength(uint uid, int count, string? seal)
    {
        if (seal is not null && seal.Length != 8) throw new InvalidDataException("Quest packet seal is invalid.");
        int sealSize = seal is null ? 0 : 8;
        int repetitions = uid is 35024 or 35007 ? 2 : 1;
        long length = 16L + count * 4L * repetitions + (uid == 35007 ? 12 : 0) + sealSize;
        // Real GameClient.Send limit, in addition to the genuine caller allocation
        // checks at preparation/commit. No gameplay objective-count limit is guessed.
        if (count < 0 || length > 1032 || count > ushort.MaxValue)
            throw new InvalidDataException("Quest objective frame exceeds the native send/count bound.");
        return (int)length;
    }

    internal static byte[] PrepareQuestObjectiveFrame(uint uid, ReadOnlySpan<uint> intentions, string? seal)
    {
        int length = GetQuestObjectiveFrameLength(uid, intentions.Length, seal);
        int sealSize = seal is null ? 0 : 8;
        var frame = new byte[length];
        BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)(length - sealSize)));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2136);
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6), checked((ushort)intentions.Length));
        BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8), uid);
        int offset = 12;
        WriteIntentions(frame, ref offset, intentions);
        if (uid == 35024) WriteIntentions(frame, ref offset, intentions);
        offset += 4; // source trailing UInt32 zero
        if (uid == 35007)
        {
            offset += 12; // source ZeroFill12
            WriteIntentions(frame, ref offset, intentions);
        }
        if (seal is not null)
            Buffer.BlockCopy(Encoding.UTF8.GetBytes(seal), 0, frame, offset, 8);
        return frame;
    }

    private static void WriteIntentions(byte[] frame, ref int offset, ReadOnlySpan<uint> intentions)
    {
        foreach (uint value in intentions)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(offset), value);
            offset += 4;
        }
    }
}
