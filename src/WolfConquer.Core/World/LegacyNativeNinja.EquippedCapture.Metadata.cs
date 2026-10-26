namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private const int MaximumEquippedMetadataRows = 16384;

    private readonly record struct EquippedMetadataRow(
        LegacyNativeNinjaFile.NinjaSprintItem Row, uint ItemId, uint Level, int Power, uint Damage)
    {
        internal bool IsCurrent(LegacyNativeNinjaFile.NinjaSprintItem row) => ReferenceEquals(Row, row) && row is not null &&
            row.ItemID == ItemId && row.Level == Level && row.Power == Power && row.Damage == Damage;
    }

    private static EquippedMetadataRow[] CaptureEquippedMetadata(Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem> table)
    {
        if (table.Count > MaximumEquippedMetadataRows) throw UnavailableEquippedSigil();
        var rows = new List<EquippedMetadataRow>(Math.Min(table.Count, MaximumEquippedMetadataRows));
        foreach (var row in table.Values)
        {
            if (row is null || rows.Count >= MaximumEquippedMetadataRows) throw UnavailableEquippedSigil();
            rows.Add(new(row, row.ItemID, row.Level, row.Power, row.Damage));
        }
        return rows.ToArray();
    }

    private static bool EquippedMetadataCurrent(
        Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem> table, EquippedMetadataRow[] rows)
    {
        if (table.Count != rows.Length || rows.Length > MaximumEquippedMetadataRows) return false;
        var index = 0;
        foreach (var row in table.Values)
        {
            if (index >= rows.Length || !rows[index++].IsCurrent(row)) return false;
        }
        return index == rows.Length;
    }
}
