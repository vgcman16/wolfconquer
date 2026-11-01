namespace WolfConquer.Core.World;

// Read-only source effect selection. This proof cannot authorize an attack.
internal sealed partial class LegacyBasicPhysicalRuneSelection
{
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot worn;
    private readonly Metadata? metadata;
    internal bool HasDeadlySight { get; }
    internal byte Level { get; }

    private LegacyBasicPhysicalRuneSelection(LegacyOrdinarySpellBinding binding,
        LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot worn)
    {
        this.binding = binding; this.worn = worn;
        // Original IsEquipped returns before metadata selection here. Do not
        // impose a new all-Rune metadata requirement on ineligible players.
        if (worn.Reborn < 2 || worn.PlayerLevel < 15) return;
        var rows = worn.Rows;
        var requiredIds = rows.Where(row => LegacyNativeItemTypeRuneMetadata.IsRune(row.ItemId))
            .Select(row => row.ItemId).Distinct().Order().ToArray();
        if (requiredIds.Length == 0) return;
        metadata = new(binding.ItemCatalog, requiredIds);
        foreach (var row in rows)
        {
            if (!LegacyNativeItemTypeRuneMetadata.IsRune(row.ItemId)) continue;
            var required = metadata.Require(row.ItemId);
            if (LegacyNativeItemTypeRuneMetadata.EquipPassJobReq(required.Class, worn.Class) &&
                required.Name.Contains("DeadlySight", StringComparison.OrdinalIgnoreCase))
            {
                HasDeadlySight = true; Level = checked((byte)(row.ItemId % 100)); return;
            }
        }
    }

    internal static bool TryCapture(LegacyOrdinarySpellBinding binding, WorldSession expected,
        out LegacyBasicPhysicalRuneSelection? proof)
    {
        proof = null;
        try
        {
            if (binding is null || expected is null ||
                !LegacyNativeRuneItemStorage.BasicPhysicalWornSnapshot.TryCapture(binding.Items.Rune, binding, expected, out var worn) ||
                worn is null) return false;
            var captured = new LegacyBasicPhysicalRuneSelection(binding, worn);
            if (!captured.IsCurrentPlain()) return false;
            proof = captured; return true;
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or OverflowException or ArgumentException)
        { return false; }
    }

    internal bool IsCurrentPlain() => worn.IsCurrentPlain() && (metadata is null || metadata.IsCurrentPlain(binding.ItemCatalog));
}
