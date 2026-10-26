using System.Buffers.Binary;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyMailboxStore
{
    // Existing managed WriteEntry order, not the native unsafe struct's padding.
    private const int ClaimEntrySize = 740;
    private const int ClaimMoneyOffset = 708;
    private const int ClaimCpOffset = 716;
    private const int ClaimItemOffset = 720;

    internal void PublishClaims(ClaimCapture capture, ClaimProjection projection) => capture.Publish(this, projection);

    internal bool TryCaptureClaims(uint uid, byte[] bytes, out ClaimCapture capture, out string reason)
    {
        capture = null!;
        reason = "mailboxClaimCacheOrImageInvalid";
        if (!Monitor.IsEntered(SyncRoot) || _claimFailure is not null ||
            !mailboxByPlayerUid.TryGetValue(uid, out var cached) ||
            !TryParseCompleteImage(bytes, out var rows)) return false;
        var now = nowProvider();
        var live = rows.Where(row => row.IsLive(now)).ToArray();
        var current = cached.Where(row => row is not null && row.IsLive(now)).ToArray();
        if (cached.Any(row => row is null) || live.Length != current.Length ||
            !live.Zip(current).All(pair => SameEntry(pair.First, pair.Second))) return false;
        capture = new(this, uid, cached, cached.ToArray(), rows, bytes.ToArray());
        reason = string.Empty;
        return true;
    }

    private bool TryParseCompleteImage(byte[] bytes, out LegacyMailboxEntry[] rows)
    {
        rows = [];
        if (bytes.Length is < 4 or > CharacterSnapshotFileBaseline.MaximumBytes) return false;
        var count = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (count < 0 || count > (bytes.Length - 4) / ClaimEntrySize ||
            4L + (long)count * ClaimEntrySize != bytes.Length) return false;
        using var stream = new MemoryStream(bytes, writable: false);
        using var reader = new BinaryReader(stream, encoding, leaveOpen: false);
        reader.ReadInt32();
        var captured = new LegacyMailboxEntry[count];
        var ids = new HashSet<uint>();
        var attachments = new HashSet<uint>();
        for (var index = 0; index < count; index++)
        {
            var row = ReadEntry(reader);
            if (row.Id == 0 || !ids.Add(row.Id) || row.Timer <= 0 ||
                row.ItemId != 0 && !attachments.Add(row.ItemId)) return false;
            _ = DateTime.FromBinary(row.Timer); // Validate expired tails as well.
            captured[index] = row;
        }
        if (stream.Position != stream.Length) return false;
        rows = captured;
        return true;
    }

    private static bool SameEntry(LegacyMailboxEntry left, LegacyMailboxEntry right) =>
        left.Id == right.Id && left.Money == right.Money && left.EMoney == right.EMoney && left.ItemId == right.ItemId &&
        left.EmoneyRecordType == right.EmoneyRecordType && left.Flag == right.Flag && left.Timer == right.Timer &&
        string.Equals(left.Title, right.Title, StringComparison.Ordinal) &&
        string.Equals(left.SenderName, right.SenderName, StringComparison.Ordinal) &&
        string.Equals(left.Content, right.Content, StringComparison.Ordinal);

    internal sealed class ClaimCapture
    {
        private readonly LegacyMailboxStore _store;
        private readonly uint _uid;
        private readonly List<LegacyMailboxEntry> _cache;
        private readonly LegacyMailboxEntry[] _expected;
        private readonly LegacyMailboxEntry[] _rows;
        private readonly byte[] _bytes;
        internal DateTime Now => _store.nowProvider();

        internal ClaimCapture(LegacyMailboxStore store, uint uid, List<LegacyMailboxEntry> cache,
            LegacyMailboxEntry[] expected, LegacyMailboxEntry[] rows, byte[] bytes)
        { _store = store; _uid = uid; _cache = cache; _expected = expected; _rows = rows; _bytes = bytes; }

        internal LegacyMailboxEntry? FindLive(uint id, DateTime now) =>
            _rows.FirstOrDefault(row => row.Id == id && row.IsLive(now));
        internal bool MatchesImage(ReadOnlySpan<byte> bytes) => bytes.SequenceEqual(_bytes);
        internal byte[] CopyImage() => _bytes.ToArray();

        internal bool IsCurrent() => Monitor.IsEntered(_store.SyncRoot) && _store._claimFailure is null &&
            _store.mailboxByPlayerUid.TryGetValue(_uid, out var current) && ReferenceEquals(current, _cache) &&
            current.Count == _expected.Length && current.SequenceEqual(_expected);

        internal ClaimProjection Project(IReadOnlyDictionary<uint, LegacyMailboxEntry> replacements)
        {
            var image = _bytes.ToArray();
            for (var index = 0; index < _rows.Length; index++)
            {
                var original = _rows[index];
                if (!replacements.TryGetValue(original.Id, out var next)) continue;
                // A claim may only clear these reward fields, never add or reprice a reward.
                var permitted = original.WithClaimedMoney().WithClearedItemId();
                if (next.Money != 0 && next.Money != original.Money ||
                    next.EMoney != 0 && next.EMoney != original.EMoney ||
                    next.ItemId != 0 && next.ItemId != original.ItemId ||
                    !SameEntry(next.WithClaimedMoney().WithClearedItemId(), permitted))
                    throw new InvalidOperationException("Invalid mailbox claim projection.");
                var offset = checked(4 + index * ClaimEntrySize);
                BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(offset + ClaimMoneyOffset), next.Money);
                BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + ClaimCpOffset), next.EMoney);
                BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset + ClaimItemOffset), next.ItemId);
            }
            var cache = _expected.Select(row => replacements.TryGetValue(row.Id, out var next) ? next : row).ToList();
            return new(this, image, cache);
        }

        internal void Publish(LegacyMailboxStore owner, ClaimProjection projection)
        {
            if (!ReferenceEquals(owner, _store) || !ReferenceEquals(projection.Owner, this) || !IsCurrent())
                throw new InvalidOperationException("Mailbox cache changed after durable claim.");
            // The existing key and prepared List avoid post-write collection allocation.
            // Caller retains SyncRoot and revalidated this capture before the real journal.
            _store.mailboxByPlayerUid[_uid] = projection.Cache;
        }
    }

    internal sealed record ClaimProjection(ClaimCapture Owner, byte[] Image, List<LegacyMailboxEntry> Cache);
}
