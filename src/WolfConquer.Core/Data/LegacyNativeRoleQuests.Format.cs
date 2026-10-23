using System.Buffers.Binary;
using Row = WolfConquer.Core.World.LegacyNativeMsgQuestList.QuestListItem;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeRoleQuests
{
    // Operational memory/I/O bound, not a quest or intention gameplay limit.
    private const int MaximumImageBytes = 64 * 1024 * 1024;

    private static byte[] Encode(BookGraph graph)
    {
        var bytes = new byte[graph.ImageLength];
        int offset = 0;
        Put32(bytes, ref offset, (uint)graph.Source.Length);
        foreach (var entry in graph.Source)
        {
            Put32(bytes, ref offset, entry.Uid);
            Put32(bytes, ref offset, (uint)entry.Status);
            Put32(bytes, ref offset, entry.Time);
            Put32(bytes, ref offset, (uint)entry.Values.Length);
            foreach (uint value in entry.Values) Put32(bytes, ref offset, value);
        }
        return bytes;
    }

    private static Row[] Decode(byte[] image)
    {
        if (image.Length < 4 || image.Length > MaximumImageBytes)
            throw new InvalidDataException("Invalid native quest image length.");
        int offset = 0;
        int count = unchecked((int)Get32(image, ref offset));
        if (count < 0 || count > (image.Length - offset) / 16)
            throw new InvalidDataException("Invalid native quest row count.");
        var rows = new Row[count];
        var seen = new HashSet<uint>();
        for (int index = 0; index < count; index++)
        {
            uint uid = Get32(image, ref offset);
            uint status = Get32(image, ref offset);
            uint time = Get32(image, ref offset);
            int intentions = unchecked((int)Get32(image, ref offset));
            if (!seen.Add(uid) || intentions < 0 || intentions > (image.Length - offset) / 4)
                throw new InvalidDataException("Duplicate quest UID or invalid intention count.");
            var values = new uint[intentions];
            for (int value = 0; value < values.Length; value++) values[value] = Get32(image, ref offset);
            rows[index] = new Row { UID = uid, Status = (Row.QuestStatus)status, Time = time, Intentions = values };
        }
        if (offset != image.Length) throw new InvalidDataException("Native quest image has an unconsumed tail.");
        return rows;
    }

    private static uint Get32(byte[] image, ref int offset)
    {
        if (offset > image.Length - 4) throw new InvalidDataException("Truncated native quest image.");
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset, 4));
        offset += 4;
        return value;
    }

    private static void Put32(byte[] image, ref int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset, 4), value);
        offset += 4;
    }
}
