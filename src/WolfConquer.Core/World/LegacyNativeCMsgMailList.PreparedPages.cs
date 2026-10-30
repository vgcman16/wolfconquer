using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCMsgMailList
{
    // Only genuine rows and private DTO/frame snapshots. Map keys are opaque;
    // neither key == row.id nor unique row ids are native producer requirements.
    private sealed class MailRowSnapshot
    {
        internal readonly uint Key;
        internal readonly LegacyNativeMailList Source;
        internal readonly Mail Wire;

        internal MailRowSnapshot(KeyValuePair<uint, LegacyNativeMailList> entry)
        {
            if (entry.Value is null)
                throw new ArgumentException("Mail map contains a missing row.", nameof(entry));
            Key = entry.Key;
            Source = entry.Value;
            Wire = new Mail(Source);
        }

        internal bool IsCurrent() =>
            Source.id == Wire.Id && Source.Money == Wire.Money &&
            Source.EMoney == Wire.EMoney && Source.Expiration_Date == Wire.Expiration_Date &&
            Source.itemid == Wire.Itemid && Source.Emoney_Record_Type == Wire.Emoney_Record_Type &&
            Source.Flag == Wire.Flag && string.Equals(Source.title, Wire.Title, StringComparison.Ordinal) &&
            string.Equals(Source.Sender_name, Wire.Sender_Name, StringComparison.Ordinal);
    }

    private sealed class PreparedMailPages
    {
        private readonly LegacyNativeDbSafeDictionary<uint, LegacyNativeMailList> owner;
        private readonly long revision;
        private readonly MailRowSnapshot[] rows;
        internal readonly byte[][] Frames;
        internal readonly string? Seal;

        private PreparedMailPages(LegacyNativeDbSafeDictionary<uint, LegacyNativeMailList> owner,
            long revision, MailRowSnapshot[] rows, byte[][] frames, string? seal)
        {
            this.owner = owner;
            this.revision = revision;
            this.rows = rows;
            Frames = frames;
            Seal = seal;
        }

        internal static PreparedMailPages Create(LegacyNativeGameClientRuntime client,
            LegacyNativeDbSafeDictionary<uint, LegacyNativeMailList> owner, bool sortById)
        {
            long revision = owner.Revision;
            var rows = owner.ToArray().Select(entry => new MailRowSnapshot(entry)).ToArray();
            if (rows.Length == 0)
                throw new InvalidOperationException("A nonempty mail producer lost its bound rows.");
            var ordered = sortById ? rows.OrderByDescending(row => row.Wire.Id).ToArray() : rows;
            string? seal = LegacyNativePacket.SealString;
            byte[] sealBytes = CaptureMailSeal(seal);
            var frames = new byte[rows.Length / 10 + 1][];
            int remaining = ordered.Length;
            for (int page = 0; page < frames.Length; page++)
            {
                var selected = new Mail[Math.Min(remaining, 10)];
                for (int index = 0; index < selected.Length; index++)
                    selected[index] = ordered[--remaining].Wire;
                // Real component serializer and complete native framing. The
                // owned buffer is bounded by the actual sender, not fake Size.
                using var frame = new LegacyNativePacket(1032);
                frame.CreateMailList(new LegacyNativeCMsgMailList { Type = 0, Count = 1, MailList = selected });
                frames[page] = CaptureMailFrame(frame, seal, sealBytes);
            }
            var prepared = new PreparedMailPages(owner, revision, rows, frames, seal);
            prepared.RequireCurrent(client);
            return prepared;
        }

        internal void RequireCurrent(LegacyNativeGameClientRuntime client)
        {
            if (!ReferenceEquals(client.MailList, owner) || owner.Revision != revision || owner.Count != rows.Length)
                throw new InvalidOperationException("Mail producer owner changed during preparation.");
            foreach (var row in rows)
                if (!owner.TryGetValue(row.Key, out var current) || !ReferenceEquals(current, row.Source))
                    throw new InvalidOperationException("Mail producer membership changed during preparation.");
            // Run after actual map lookups, which may use an application comparer.
            foreach (var row in rows)
                if (!row.IsCurrent())
                    throw new InvalidOperationException("Mail row wire values changed during preparation.");
            if (!ReferenceEquals(client.MailList, owner) || owner.Revision != revision)
                throw new InvalidOperationException("Mail producer owner changed during revalidation.");
        }
    }

    private static byte[] CaptureMailSeal(string? seal)
    {
        if (seal is null) return Array.Empty<byte>();
        if (seal.Length != 8) throw new InvalidOperationException("Mail framing seal has invalid width.");
        // Preserve the native first-eight UTF8 bytes, including split codepoints.
        return Encoding.UTF8.GetBytes(seal).AsSpan(0, 8).ToArray();
    }

    private static unsafe byte[] CaptureMailFrame(LegacyNativePacket frame, string? seal, byte[] sealBytes)
    {
        if (!string.Equals(seal, LegacyNativePacket.SealString, StringComparison.Ordinal) ||
            frame.Memory == null || frame.stream != frame.Memory + 4 || frame.Size < 4 ||
            frame.Size > frame.Capacity || frame.Size > 1032)
            throw new InvalidOperationException("Mail page has invalid completed framing state.");
        byte[] bytes = new ReadOnlySpan<byte>(frame.Memory, frame.Size).ToArray();
        int declared = BinaryPrimitives.ReadUInt16LittleEndian(bytes);
        if (declared < 4 || declared + sealBytes.Length != bytes.Length ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)) != LegacyNativeMail.CMsgMailList ||
            !bytes.AsSpan(declared).SequenceEqual(sealBytes))
            throw new InvalidOperationException("Mail page does not match its captured frame contract.");
        return bytes;
    }
}
