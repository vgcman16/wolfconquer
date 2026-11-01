namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    internal sealed partial class MailboxLoadProof
    {
        // Compare both actual source columns and the semantic views consumed by
        // native/managed code. Raw equality alone never resolves a schema conflict.
        internal bool MatchesCatalog(WorldDataCatalog catalog)
        {
            lock (MailboxLoadGate)
            {
                try
                {
                    if (catalog is null || !IsCurrent()) return false;
                    var graph = ReadMailboxGraph();
                    var read = new CatalogRead();
                    return MatchGear(graph, catalog, read) && MatchDictionaries(graph, catalog, read) &&
                        MatchRanks(catalog, read) && read.IsCurrent() && IsCurrent() &&
                        ReferenceEquals(ReadMailboxGraph(), graph);
                }
                catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or
                    OverflowException or FormatException or IndexOutOfRangeException) { return false; }
            }
        }

        private static bool MatchGear(MailboxGraph graph, WorldDataCatalog catalog, CatalogRead read)
        {
            var rows = read.Rows(catalog.CombatGear);
            if (graph.Gear is not { } gear) return false;
            int count = 0;
            foreach (var family in gear.Values)
            { if (family is null) return false; count = checked(count + family.Count); }
            if (rows.Length != count) return false;
            foreach (var row in rows)
            {
                if (row.GearTypeId < 0 || row.Level < 0 || !gear.TryGetValue((uint)row.GearTypeId, out var levels) ||
                    levels is null || !levels.TryGetValue((uint)row.Level, out var native) || native is null) return false;
                var columns = native.RawColumns; var nativeSkills = native.Skills; var tail = native.OpaqueTail;
                if (columns is null || nativeSkills is null || tail is null) return false;
                var fields = read.Numbers(row.Fields, columns.Length);
                var skills = read.Rows(row.MagicTypeIds);
                if (!fields.AsSpan().SequenceEqual(columns) || row.Id != native.ID || row.GearTypeId != native.Type ||
                    row.Level != native.Level || row.UpExperience != native.Exp || row.Life != native.MaxHP ||
                    row.Attack != native.PAttack || row.Defense != native.PDefense || row.MagicDefense != native.MDefense ||
                    row.UnknownStat0 != fields[7] || skills.Length != nativeSkills.Length ||
                    !skills.Select(value => checked((uint)value)).SequenceEqual(nativeSkills) || row.Score != native.Score) return false;
                if (tail.Length == 3 &&
                    (row.Unknown0 != tail[0] || row.Unknown1 != tail[1] || row.Unknown2 != tail[2]))
                    return false;
            }
            return read.Unique(catalog.CombatGearById, rows, row => row.Id) &&
                read.Unique(catalog.CombatGearByGearTypeAndLevel, rows, row => (row.GearTypeId, row.Level)) &&
                read.Grouped(catalog.CombatGearByGearTypeId, rows, row => row.GearTypeId);
        }

        private bool MatchRanks(WorldDataCatalog catalog, CatalogRead read)
        {
            var rows = read.Rows(catalog.CombatGearRanks);
            if (rows.Length != rankSeed.Length) return false;
            for (int index = 0; index < rows.Length; index++)
            {
                var row = rows[index]; var source = rankSeed[index];
                var fields = read.Rows(row.Fields); var values = read.Rows(row.RankingFields);
                if (fields.Length != 23 || fields[22] != "" || values.Length != 20 ||
                    row.CharacterId != CatalogRead.Number(source[0]) || row.CharacterName != source[1]) return false;
                for (int column = 0; column < 22; column++)
                {
                    if (column is 1 or 20)
                    { if (!string.Equals(fields[column], source[column], StringComparison.Ordinal)) return false; }
                    else if (CatalogRead.Number(fields[column]) != CatalogRead.Number(source[column])) return false;
                    if (column >= 2 && !string.Equals(values[column - 2], fields[column], StringComparison.Ordinal)) return false;
                }
            }
            return read.Grouped(catalog.CombatGearRanksByCharacterId, rows, row => row.CharacterId);
        }
    }
}
