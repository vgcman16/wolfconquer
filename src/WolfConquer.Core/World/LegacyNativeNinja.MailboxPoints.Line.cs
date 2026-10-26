using System.Globalization;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    internal sealed partial class MailboxPointsCapture
    {
        private const int MaximumMailboxNinjaLine = 256 * 1024;
        private static readonly int[] ScalarIndices = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14, 15, 16, 17, 18];

        internal bool MatchesLine(string line, out string[]? parts)
        {
            parts = null;
            if (line is null || line.Length is 0 or > MaximumMailboxNinjaLine || !IsCurrent()) return false;
            var fields = line.Split('/');
            if (fields.Length < 21 || fields[^1].Length != 0 || !CanonicalUInt64(fields[0], out var storedFlag) ||
                storedFlag != flag || !CanonicalUInt32(fields[19], out var count) ||
                count != rows.Length || fields.Length != 21 + 5 * (long)count) return false;
            for (var i = 0; i < ScalarIndices.Length; i++)
                if (!CanonicalUInt32(fields[ScalarIndices[i]], out var value) || value != scalars[i]) return false;
            // Native Load intentionally ignores the stored derived Score at slot12.
            // Retain that unrelated field verbatim; do not trigger mutable metadata queries.
            if (!CanonicalUInt32(fields[12], out _)) return false;
            var seen = new HashSet<uint>();
            var byKey = rows.ToDictionary(row => row.Key);
            for (var i = 0; i < count; i++)
                if (!MatchesLineItem(fields, 20 + i * 5, byKey, seen)) return false;
            if (!IsCurrent()) return false;
            parts = fields;
            return true;
        }

        private static bool MatchesLineItem(string[] fields, int offset,
            Dictionary<uint, MailboxPointsRow> rows, HashSet<uint> seen)
        {
            if (!CanonicalUInt32(fields[offset], out var id) || !seen.Add(id) || !rows.TryGetValue(id, out var row) ||
                !CanonicalUInt32(fields[offset + 1], out var level) || level != row.Level ||
                !CanonicalUInt32(fields[offset + 2], out var position) || position != row.Position ||
                !CanonicalUInt32(fields[offset + 3], out var exp) || exp != row.Exp ||
                !CanonicalUInt32(fields[offset + 4], out var uk) || uk != row.UK) return false;
            return true;
        }

        private static bool CanonicalUInt32(string text, out uint value) =>
            uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            text == value.ToString(CultureInfo.InvariantCulture);

        private static bool CanonicalUInt64(string text, out ulong value) =>
            ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            text == value.ToString(CultureInfo.InvariantCulture);
    }
}
