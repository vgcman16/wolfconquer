using System.Buffers.Binary;

namespace WolfConquer.Core.World;

public sealed partial class LegacyMailboxStore
{
    internal sealed partial class ClaimCapture
    {
        // This API can only remove exact captured, live, empty-reward records.
        // It cannot insert, replace, reorder or normalize any retained record.
        internal ClaimProjection ProjectDeletion(IReadOnlyList<LegacyMailboxEntry> selected)
        {
            ArgumentNullException.ThrowIfNull(selected);
            if (!IsCurrent()) throw new InvalidOperationException("Mailbox deletion capture is no longer current.");
            var removed = new Dictionary<uint, LegacyMailboxEntry>();
            foreach (var row in selected)
                if (row is null || row.Id == 0 || !removed.TryAdd(row.Id, row))
                    throw new InvalidOperationException("Mailbox deletion selection is invalid.");

            var now = Now;
            var matched = 0;
            foreach (var row in _rows)
            {
                if (!removed.TryGetValue(row.Id, out var requested)) continue;
                if (!ReferenceEquals(row, requested) || !row.IsLive(now) || !row.CanDelete)
                    throw new InvalidOperationException("Mailbox deletion requires a captured live empty record.");
                matched++;
            }
            if (matched != removed.Count || _expected.Count(row => removed.ContainsKey(row.Id)) != removed.Count)
                throw new InvalidOperationException("Mailbox deletion selection is not present in the complete owner.");

            var retainedCount = _rows.Length - removed.Count;
            var image = new byte[checked(4 + retainedCount * ClaimEntrySize)];
            BinaryPrimitives.WriteInt32LittleEndian(image, retainedCount);
            var destination = 4;
            for (var index = 0; index < _rows.Length; index++)
            {
                if (removed.ContainsKey(_rows[index].Id)) continue;
                _bytes.AsSpan(checked(4 + index * ClaimEntrySize), ClaimEntrySize)
                    .CopyTo(image.AsSpan(destination, ClaimEntrySize));
                destination += ClaimEntrySize;
            }
            // Keep the existing cached references/order, including any unselected expired entries.
            var cache = _expected.Where(row => !removed.ContainsKey(row.Id)).ToList();
            return new(this, image, cache);
        }
    }
}
