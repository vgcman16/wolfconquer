namespace WolfConquer.Core.World;

internal partial class LegacyNativeBeastsRuntime
{
    private sealed class PrefixMetadata
    {
        private readonly Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute> table;
        private readonly LegacyNativeBeastsAtrribute.Attribute[] rows;
        internal uint[] Required { get; }

        private PrefixMetadata(Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute> table,
            LegacyNativeBeastsAtrribute.Attribute[] rows, uint[] required)
        { this.table = table; this.rows = rows; Required = required; }

        internal static bool TryCapture(out PrefixMetadata? captured)
        {
            captured = null;
            var table = LegacyNativeBeastsAtrribute.Attributes;
            if (table is null || table.Count is < 1 or > byte.MaxValue) return false;
            var entries = ReadRows(table);
            if (entries.Length != table.Count) return false;
            var rows = new LegacyNativeBeastsAtrribute.Attribute[entries.Length];
            var required = new uint[entries.Length];
            for (var index = 0; index < entries.Length; index++)
            {
                var entry = entries[index];
                // Source indexes contiguous keys and uses Values.Last as its
                // terminal row. Refuse a mismatched/holey/shuffled table.
                if (entry.Key != index + 1 || entry.Value is not { } row || row.Level != entry.Key || row.RequiredPoints == 0) return false;
                rows[index] = row; required[index] = row.RequiredPoints;
            }
            var candidate = new PrefixMetadata(table, rows, required);
            if (!candidate.IsCurrent()) return false;
            captured = candidate; return true;
        }

        internal bool IsCurrent()
        {
            if (!ReferenceEquals(table, LegacyNativeBeastsAtrribute.Attributes) || table.Count != rows.Length) return false;
            var current = ReadRows(table);
            if (current.Length != rows.Length) return false;
            for (var index = 0; index < current.Length; index++)
                if (current[index].Key != index + 1 || !ReferenceEquals(current[index].Value, rows[index]) ||
                    rows[index].Level != index + 1 || rows[index].RequiredPoints != Required[index]) return false;
            return ReferenceEquals(table, LegacyNativeBeastsAtrribute.Attributes) && table.Count == rows.Length;
        }

        private static KeyValuePair<byte, LegacyNativeBeastsAtrribute.Attribute>[] ReadRows(
            Dictionary<byte, LegacyNativeBeastsAtrribute.Attribute> table)
        {
            var result = new List<KeyValuePair<byte, LegacyNativeBeastsAtrribute.Attribute>>();
            // Concrete Dictionary's struct enumerator, never a derived
            // ICollection.CopyTo/custom comparer/interface-enumerator callback.
            foreach (var row in table) result.Add(row);
            return result.ToArray();
        }
    }
}
