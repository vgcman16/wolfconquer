namespace WolfConquer.Core.World;

internal partial class LegacyNativeHairfaceStorageRuntime
{
    private sealed class PrefixHairfaceRow
    {
        internal LegacyNativeHairfaceStorageTable.Hairface Row { get; }
        private readonly LegacyHairFaceStorageType type;
        private readonly uint id, cost;
        private readonly string? name;
        private readonly byte rare, vip, sex, equipped;
        private readonly List<byte> classes;
        private readonly byte[] colors;
        private readonly byte[] classValues, colorValues;

        internal PrefixHairfaceRow(LegacyNativeHairfaceStorageTable.Hairface row)
        {
            if (row is null || row.Classes is null || row.Colors is null || row.Classes.Count > 256 || row.Colors.Length > 256)
                throw new InvalidDataException("Hairface row is incomplete.");
            Row = row; type = row.Type; id = row.ID; cost = row.Cost; name = row.Name;
            rare = row.RareLevel; vip = row.RequiredVIPLevel; sex = row.Sex; equipped = row.EquippedColor;
            classes = row.Classes; colors = row.Colors;
            classValues = new byte[classes.Count];
            for (var index = 0; index < classValues.Length; index++) classValues[index] = classes[index];
            colorValues = colors.ToArray();
        }

        internal uint Id => id;
        internal LegacyHairFaceStorageType Type => type;

        internal LegacyNativeHairfaceStorageTable.Hairface CopyLoaded(byte equippedColor, byte[] savedColors) => new()
        {
            ID = id, Type = type, Name = name, RareLevel = rare, RequiredVIPLevel = vip, Cost = cost,
            Sex = sex, Classes = new List<byte>(classValues), EquippedColor = equippedColor, Colors = savedColors,
        };

        internal bool Matches(LegacyNativeHairfaceStorageTable.Hairface row) => ReferenceEquals(row, Row) &&
            row.Type == type && row.ID == id && row.Cost == cost && row.Name == name && row.RareLevel == rare &&
            row.RequiredVIPLevel == vip && row.Sex == sex && row.EquippedColor == equipped &&
            ReferenceEquals(row.Classes, classes) && ReferenceEquals(row.Colors, colors) &&
            ClassesCurrent() && colors.AsSpan().SequenceEqual(colorValues);

        private bool ClassesCurrent()
        {
            if (classes.Count != classValues.Length) return false;
            for (var index = 0; index < classValues.Length; index++) if (classes[index] != classValues[index]) return false;
            return true;
        }
    }

    private sealed class PrefixCatalog
    {
        private readonly List<LegacyNativeHairfaceStorageTable.Hairface> table;
        private readonly PrefixHairfaceRow[] rows;
        private PrefixCatalog(List<LegacyNativeHairfaceStorageTable.Hairface> table, PrefixHairfaceRow[] rows)
        { this.table = table; this.rows = rows; }

        internal static bool TryCapture(out PrefixCatalog? captured)
        {
            captured = null;
            var table = LegacyNativeHairfaceStorageTable.Hairfaces;
            if (table is null || table.Count > 16_384) return false;
            var rows = new PrefixHairfaceRow[table.Count];
            for (var index = 0; index < rows.Length; index++) rows[index] = new PrefixHairfaceRow(table[index]);
            var candidate = new PrefixCatalog(table, rows);
            if (!candidate.IsCurrent()) return false;
            captured = candidate; return true;
        }

        internal PrefixHairfaceRow? Find(uint id)
        {
            PrefixHairfaceRow? found = null;
            foreach (var row in rows)
            {
                if (row.Id != id) continue;
                // The persisted row contains no Type discriminator. A selected
                // duplicate ID cannot safely resolve to an arbitrary first row.
                if (found is not null) return null;
                found = row;
            }
            return found;
        }

        internal bool IsCurrent()
        {
            if (!ReferenceEquals(table, LegacyNativeHairfaceStorageTable.Hairfaces) || table.Count != rows.Length) return false;
            for (var index = 0; index < rows.Length; index++) if (!rows[index].Matches(table[index])) return false;
            return ReferenceEquals(table, LegacyNativeHairfaceStorageTable.Hairfaces) && table.Count == rows.Length;
        }
    }
}
