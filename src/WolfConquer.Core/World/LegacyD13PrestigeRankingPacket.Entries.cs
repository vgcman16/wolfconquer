using System.Buffers.Binary;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial record LegacyD13PrestigeRankingPacket
{
    private static LegacyD13PrestigeRankingEntry[] SnapshotEntries(IReadOnlyList<LegacyD13PrestigeRankingEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var count = entries.Count;
        const int maximumRows = (PacketHeader.DefaultMaxFrameSize - PacketHeader.Size - EntriesOffset) / EntryWireLength;
        if (count < 0 || count > maximumRows)
        {
            throw new ArgumentOutOfRangeException(nameof(Entries), count, "Prestige rows exceed the transport frame bound.");
        }

        var snapshot = new LegacyD13PrestigeRankingEntry[count];
        for (var index = 0; index < count; index++)
        {
            var entry = entries[index];
            ArgumentNullException.ThrowIfNull(entry);
            snapshot[index] = entry;
        }
        return snapshot;
    }

    private static LegacyD13PrestigeRankingEntry ReadEntry(ReadOnlySpan<byte> row)
    {
        return new LegacyD13PrestigeRankingEntry(
            row[0],
            BinaryPrimitives.ReadInt64LittleEndian(row[1..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[9..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[13..]),
            row.Slice(17, 32), row.Slice(49, 32),
            BinaryPrimitives.ReadUInt32LittleEndian(row[81..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[85..]),
            BinaryPrimitives.ReadUInt32LittleEndian(row[89..]),
            row.Slice(93, 16),
            BinaryPrimitives.ReadUInt32LittleEndian(row[109..]));
    }

    private static void WriteEntry(Span<byte> row, LegacyD13PrestigeRankingEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        row[0] = entry.Rank;
        BinaryPrimitives.WriteInt64LittleEndian(row[1..], entry.Amount);
        BinaryPrimitives.WriteUInt32LittleEndian(row[9..], entry.CategoryCode);
        BinaryPrimitives.WriteUInt32LittleEndian(row[13..], entry.CharacterUid);
        entry.NameBytes.CopyTo(row.Slice(17, 32));
        entry.SecondaryNameBytes.CopyTo(row.Slice(49, 32));
        BinaryPrimitives.WriteUInt32LittleEndian(row[81..], entry.Level);
        BinaryPrimitives.WriteUInt32LittleEndian(row[85..], entry.Class);
        BinaryPrimitives.WriteUInt32LittleEndian(row[89..], entry.Mesh);
        entry.OpaqueTailBytes.CopyTo(row.Slice(93, 16));
        BinaryPrimitives.WriteUInt32LittleEndian(row[109..], entry.OpaqueValue);
    }
}
