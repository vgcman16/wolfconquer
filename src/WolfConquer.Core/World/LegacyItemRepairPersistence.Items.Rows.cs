using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyItemRepairPersistence
{
    private readonly record struct RawRow(uint Uid, int Offset, uint Parent, uint Children);
    private sealed record RawSections(RawRow[] Order, Dictionary<uint, RawRow> Items,
        Dictionary<uint, int> Perfections, int PerfectionHeader, int RootCount);

    // Every caller validates the complete actual grammar before indexing offsets.
    private static RawSections ReadSections(byte[] image)
    {
        LegacyItemUidRawCatalog.ValidateAdmissionPlayerItems(image);
        var order = new List<RawRow>(); var rows = new Dictionary<uint, RawRow>();
        int roots = BinaryPrimitives.ReadInt32LittleEndian(image), offset = sizeof(int);
        for (int index = 0; index < roots; index++)
        {
            var row = Read(0); order.Add(row); rows.Add(row.Uid, row);
            for (uint child = 0; child < row.Children; child++)
            { var next = Read(row.Uid); order.Add(next); rows.Add(next.Uid, next); }
        }
        int header = offset, count = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(offset)); offset += sizeof(int);
        var perfections = new Dictionary<uint, int>();
        for (int index = 0; index < count; index++)
        {
            perfections.Add(BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset)), offset);
            offset = checked(offset + PlayerItemSnapshotLoader.LegacyPerfectionSize);
        }
        return new(order.ToArray(), rows, perfections, header, roots);

        RawRow Read(uint parent)
        {
            var row = new RawRow(BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset)), offset, parent,
                BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset + 136)));
            offset = checked(offset + PlayerItemSnapshotLoader.LegacyDbItemSize); return row;
        }
    }

    private static bool ModeledEqual(byte[] first, RawSections left, byte[] second, RawSections right)
    {
        if (left.Items.Count != right.Items.Count || left.Perfections.Count != right.Perfections.Count) return false;
        foreach (var (uid, row) in left.Items)
            if (!right.Items.TryGetValue(uid, out var other) || row.Parent != other.Parent ||
                !first.AsSpan(row.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize)
                    .SequenceEqual(second.AsSpan(other.Offset, PlayerItemSnapshotLoader.LegacyDbItemSize))) return false;
        foreach (var (uid, offset) in left.Perfections)
            if (!right.Perfections.TryGetValue(uid, out var other) ||
                !first.AsSpan(offset, PlayerItemSnapshotLoader.LegacyPerfectionSize)
                    .SequenceEqual(second.AsSpan(other, PlayerItemSnapshotLoader.LegacyPerfectionSize))) return false;
        return true;
    }

    private static byte[] RemoveRows(byte[] patched, RawSections raw, HashSet<uint> removed)
    {
        if (removed.Count == 0) return patched;
        var ranges = new List<(int Offset, int Length)>(); int removedPerfections = 0;
        foreach (uint uid in removed)
        {
            ranges.Add((raw.Items[uid].Offset, PlayerItemSnapshotLoader.LegacyDbItemSize));
            if (raw.Perfections.TryGetValue(uid, out int offset))
            { ranges.Add((offset, PlayerItemSnapshotLoader.LegacyPerfectionSize)); removedPerfections++; }
        }
        ranges.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        int bytes = checked(removed.Count * PlayerItemSnapshotLoader.LegacyDbItemSize +
            removedPerfections * PlayerItemSnapshotLoader.LegacyPerfectionSize);
        var result = new byte[checked(patched.Length - bytes)]; int source = 0, target = 0;
        foreach (var range in ranges)
        {
            patched.AsSpan(source, range.Offset - source).CopyTo(result.AsSpan(target));
            target += range.Offset - source; source = checked(range.Offset + range.Length);
        }
        patched.AsSpan(source).CopyTo(result.AsSpan(target));
        BinaryPrimitives.WriteInt32LittleEndian(result, checked(raw.RootCount - removed.Count));
        int header = checked(raw.PerfectionHeader - removed.Count * PlayerItemSnapshotLoader.LegacyDbItemSize);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(header), checked(raw.Perfections.Count - removedPerfections));
        return result;
    }
}
