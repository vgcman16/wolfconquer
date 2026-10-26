using System.Collections.Concurrent;
using System.Globalization;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    internal sealed class MailboxRecoveryLine(ulong flag, uint[] values, MailboxRecoveryItem[] rows, string[] fields)
    {
        private const int MaximumLine = 256 * 1024;

        internal static bool TryRead(string? line, out MailboxRecoveryLine? parsed)
        {
            parsed = null;
            if (line is null || line.Length is 0 or > MaximumLine) return false;
            var fields = line.Split('/');
            if (fields.Length < 21 || fields[^1].Length != 0 ||
                !ulong.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var flag) ||
                fields[0] != flag.ToString(CultureInfo.InvariantCulture)) return false;
            var values = new uint[19];
            for (var index = 0; index < values.Length; index++)
                if (!ReadUInt(fields[index + 1], out values[index])) return false;
            var count = values[18];
            if (count > MaximumEquippedItemRows || fields.Length != 21 + 5L * count) return false;
            var rows = new MailboxRecoveryItem[count];
            var seen = new HashSet<uint>();
            for (var index = 0; index < rows.Length; index++)
            {
                var offset = 20 + index * 5;
                if (!ReadUInt(fields[offset], out var id) || id == 0 || !seen.Add(id) ||
                    !ReadUInt(fields[offset + 1], out var level) || !ReadUInt(fields[offset + 2], out var position) ||
                    !ReadUInt(fields[offset + 3], out var exp) || !ReadUInt(fields[offset + 4], out var uk)) return false;
                rows[index] = new(id, level, position, exp, uk);
            }
            parsed = new(flag, values, rows, fields);
            return true;
        }

        internal string[] CopyFields() => fields.ToArray();

        internal (ulong Flag, uint[] Values, MailboxRecoveryItem[] Rows) CopyPersistence() =>
            (flag, values.ToArray(), rows.ToArray());

        // Compare complete loaded scalar/item state without ToString()/Score:
        // native Load consumes but deliberately ignores stored derived score12.
        internal bool Matches(LegacyNativeNinja ninja, ConcurrentDictionary<uint, Item> expectedItems,
            KeyValuePair<uint, Item>[] expectedRows)
        {
            if (ninja.Flag != flag || !ReferenceEquals(ninja.Items, expectedItems) ||
                expectedItems.Count != rows.Length || expectedRows.Length != rows.Length) return false;
            uint[] current = [ninja.Exp, ninja.Fire, ninja.Water, ninja.Earth, ninja.Wind, ninja.Lightning,
                ninja.tmp_Fire, ninja.tmp_Water, ninja.tmp_Earth, ninja.tmp_Wind, ninja.tmp_Lightning,
                ninja.FireMastery, ninja.WaterMastery, ninja.EarthMastery, ninja.WindMastery, ninja.LightningMastery,
                ninja.UnlockMastery];
            for (var index = 0; index < current.Length; index++)
                if (current[index] != values[index < 11 ? index : index + 1]) return false;
            var actual = expectedItems.Take(MaximumEquippedItemRows + 1).ToArray();
            if (actual.Length != expectedRows.Length) return false;
            var observed = new Dictionary<uint, Item>();
            foreach (var pair in actual)
                if (pair.Value is null || !observed.TryAdd(pair.Key, pair.Value)) return false;
            foreach (var expected in expectedRows)
                if (!observed.TryGetValue(expected.Key, out var item) || !ReferenceEquals(item, expected.Value)) return false;
            foreach (var row in rows)
                if (!observed.TryGetValue(row.Id, out var item) || item.ItemID != row.Id || item.Level != row.Level ||
                    item.Position != row.Position || item.Exp != row.Exp || item.UK != row.UK) return false;
            return ReferenceEquals(ninja.Items, expectedItems) && expectedItems.Count == rows.Length && ninja.Flag == flag;
        }

        private static bool ReadUInt(string text, out uint value) =>
            uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) &&
            text == value.ToString(CultureInfo.InvariantCulture);
    }

    internal readonly record struct MailboxRecoveryItem(uint Id, uint Level, uint Position, uint Exp, uint UK);
}
