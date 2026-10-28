using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyMailboxStore
{
    internal bool TryCaptureOperatorDelivery(uint uid, byte[]? exactBefore, out DeliveryCapture capture, out string reason)
    {
        capture = null!;
        reason = "mailboxDeliveryCacheOrImageInvalid";
        if (!Monitor.IsEntered(SyncRoot) || uid == 0 || _claimFailure is not null) return false;
        try
        {
            var bytes = exactBefore?.ToArray();
            LegacyMailboxEntry[] rows = [];
            if (bytes is not null && !TryParseCompleteImage(bytes, out rows)) return false;
            mailboxByPlayerUid.TryGetValue(uid, out var cached);
            var expected = cached?.ToArray() ?? [];
            var now = nowProvider();
            var live = rows.Where(row => row.IsLive(now)).ToArray();
            var currentLive = expected.Where(row => row is not null && row.IsLive(now)).ToArray();
            if (cached is not null && (expected.Any(row => row is null) || live.Length != currentLive.Length ||
                !live.Zip(currentLive).All(pair => SameEntry(pair.First, pair.Second)))) return false;
            var candidate = new DeliveryCapture(this, uid, cached, expected, rows, bytes, mailboxByPlayerUid.Count);
            if (!candidate.IsCurrent()) return false;
            capture = candidate;
            reason = string.Empty;
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or OverflowException)
        { return false; }
    }

    internal void PublishOperatorDelivery(DeliveryCapture capture, DeliveryProjection projection) => capture.Publish(this, projection);

    internal sealed class DeliveryCapture
    {
        private readonly LegacyMailboxStore store;
        private readonly uint uid;
        private readonly List<LegacyMailboxEntry>? cache;
        private readonly LegacyMailboxEntry[] expected;
        private readonly LegacyMailboxEntry[] rows;
        private readonly byte[]? before;
        private readonly int cacheCount;

        internal DeliveryCapture(LegacyMailboxStore store, uint uid, List<LegacyMailboxEntry>? cache,
            LegacyMailboxEntry[] expected, LegacyMailboxEntry[] rows, byte[]? before, int cacheCount)
        { this.store = store; this.uid = uid; this.cache = cache; this.expected = expected; this.rows = rows; this.before = before; this.cacheCount = cacheCount; }

        internal bool IsCurrent()
        {
            if (!Monitor.IsEntered(store.SyncRoot) || store._claimFailure is not null) return false;
            var exists = store.mailboxByPlayerUid.TryGetValue(uid, out var current);
            if (cache is null) return !exists && store.mailboxByPlayerUid.Count == cacheCount;
            return exists && ReferenceEquals(cache, current) && current!.Count == expected.Length && current.SequenceEqual(expected);
        }

        internal byte[]? CopyBeforeImage() => before?.ToArray();
        internal bool MatchesBeforeImage(byte[]? image) => before is null ? image is null : image is not null && before.AsSpan().SequenceEqual(image);

        internal bool TryAllocateMailIds(int count, out uint[] ids)
        {
            ids = [];
            if (!IsCurrent() || count is < 1 or > 32) return false;
            var used = rows.Select(row => row.Id).ToHashSet();
            var result = new uint[count];
            uint next = 1;
            for (var i = 0; i < count; i++)
            {
                while (next < uint.MaxValue && used.Contains(next)) next++;
                if (next == uint.MaxValue) return false;
                result[i] = next++;
            }
            if (!IsCurrent()) return false;
            ids = result;
            return true;
        }

        internal bool TryPrepare(IReadOnlyList<LegacyMailboxEntry> additions, out DeliveryProjection projection, out string reason)
        {
            projection = null!;
            reason = "mailboxDeliveryInvalid";
            if (!IsCurrent()) return false;
            try
            {
                // Only known concrete containers are enumerated under the mailbox gate.
                LegacyMailboxEntry[] added = additions switch
                {
                    LegacyMailboxEntry[] array => array.ToArray(),
                    List<LegacyMailboxEntry> list when list.GetType() == typeof(List<LegacyMailboxEntry>) => list.ToArray(),
                    _ => []
                };
                if (added.Length is < 1 or > 32 || added.Any(row => row is null)) return false;
                var ids = rows.Select(row => row.Id).ToHashSet();
                var attachments = rows.Where(row => row.ItemId != 0).Select(row => row.ItemId).ToHashSet();
                var now = store.nowProvider();
                var strict = (Encoding)store.encoding.Clone();
                strict.EncoderFallback = EncoderFallback.ExceptionFallback;
                strict.DecoderFallback = DecoderFallback.ExceptionFallback;
                foreach (var row in added)
                {
                    if (row.Id == 0 || !ids.Add(row.Id) || row.ItemId != 0 && !attachments.Add(row.ItemId) ||
                        !row.IsLive(now) || !ValidText(strict, row.Title, LegacyMailboxEntry.TitleByteLength) ||
                        !ValidText(strict, row.SenderName, LegacyMailboxEntry.SenderNameByteLength) ||
                        !ValidText(strict, row.Content, LegacyMailboxEntry.ContentByteLength)) return false;
                }
                var count = checked(rows.Length + added.Length);
                var length = checked(4 + count * ClaimEntrySize);
                if (length > CharacterSnapshotFileBaseline.MaximumBytes) return false;
                var image = new byte[length];
                BinaryPrimitives.WriteInt32LittleEndian(image, count);
                if (before is not null) Array.Copy(before, 4, image, 4, before.Length - 4);
                using (var stream = new MemoryStream(image, writable: true))
                using (var writer = new BinaryWriter(stream, store.encoding, leaveOpen: false))
                {
                    stream.Position = 4L + (long)rows.Length * ClaimEntrySize;
                    foreach (var row in added) store.WriteEntry(writer, row);
                }
                if (!store.TryParseCompleteImage(image, out var parsed) ||
                    !parsed.Skip(rows.Length).Zip(added).All(pair => SameEntry(pair.First, pair.Second))) return false;
                var nextCache = rows.Where(row => row.IsLive(now)).Concat(added).ToList();
                if (cache is null) store.mailboxByPlayerUid.EnsureCapacity(checked(cacheCount + 1));
                if (!IsCurrent()) return false;
                projection = new DeliveryProjection(this, image, nextCache);
                reason = string.Empty;
                return true;
            }
            catch (Exception error) when (error is ArgumentException or InvalidDataException or OverflowException)
            { return false; }
        }

        private static bool ValidText(Encoding strict, string value, int length)
        {
            if (value is null || value.Contains('\0')) return false;
            var bytes = strict.GetBytes(value);
            return bytes.Length <= length && !bytes.Contains((byte)0) && string.Equals(strict.GetString(bytes), value, StringComparison.Ordinal);
        }

        internal void Publish(LegacyMailboxStore owner, DeliveryProjection projection)
        {
            if (!ReferenceEquals(owner, store) || !ReferenceEquals(projection.Owner, this) || !IsCurrent())
                throw new InvalidOperationException("Mailbox cache changed after durable delivery.");
            store.mailboxByPlayerUid[uid] = projection.Cache;
        }
    }

    internal sealed class DeliveryProjection
    {
        internal DeliveryProjection(DeliveryCapture owner, byte[] image, List<LegacyMailboxEntry> cache)
        { Owner = owner; Image = image; Cache = cache; }
        internal DeliveryCapture Owner { get; }
        private byte[] Image { get; }
        internal List<LegacyMailboxEntry> Cache { get; }
        internal byte[] CopyAfterImage() => Image.ToArray();
    }
}
