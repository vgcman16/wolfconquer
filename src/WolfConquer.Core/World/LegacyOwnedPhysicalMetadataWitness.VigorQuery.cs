using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Preallocated DATA witness for the actual query's selected Vigor catalog
// inputs. No caller collection/comparer or serializer is called by IsCurrent.
internal sealed class LegacyVigorCatalogWitness
{
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldDataCatalog? catalog;
    private Dictionary<(int, int), ItemAddBonus>? index;
    private Dictionary<(int, int), ItemAddBonus>.Enumerator indexWitness;
    private KeyValuePair<(int, int), ItemAddBonus>[] entries = [];
    private ItemAddBonus[]? rawArray;
    private List<ItemAddBonus>? rawList;
    private List<ItemAddBonus>.Enumerator rawWitness;
    private ItemAddBonus[] rawRows = [];
    private (int ItemTypeId, int Level)[] rawKeys = [];
    private readonly List<ValuesProof> rows = [];
    private ValuesProof[] frozenRows = [];

    private sealed class ValuesProof
    {
        private readonly ItemAddBonus row;
        private readonly int[]? array;
        private readonly List<int>? list;
        private readonly List<int>.Enumerator iterator;
        private readonly int[] saved;
        internal ValuesProof(ItemAddBonus row)
        {
            this.row = row;
            if (row.Values.GetType() == typeof(int[])) array = (int[])row.Values;
            else if (row.Values.GetType() == typeof(List<int>)) { list = (List<int>)row.Values; iterator = list.GetEnumerator(); }
            else throw Invalid();
            saved = array is not null ? (int[])array.Clone() : list!.ToArray();
        }
        internal bool IsCurrentPlain()
        {
            if (array is not null)
            {
                if (!ReferenceEquals(row.Values, array) || array.Length != saved.Length) return false;
                for (int i = 0; i < saved.Length; i++) if (array[i] != saved[i]) return false;
                return true;
            }
            if (!ReferenceEquals(row.Values, list) || list!.Count != saved.Length) return false;
            var read = iterator; int index = 0;
            while (read.MoveNext()) if (index == saved.Length || read.Current != saved[index++]) return false;
            return index == saved.Length;
        }
    }

    private LegacyVigorCatalogWitness(LegacyOrdinarySpellBinding binding, WorldDataCatalog? catalog)
    { this.binding = binding; this.catalog = catalog; }

    internal static LegacyVigorCatalogWitness Capture(LegacyOrdinarySpellBinding binding,
        WorldDataCatalog? catalog, WorldSession source, IReadOnlyList<PlayerItemSnapshotItem> equipment)
    {
        if (!ReferenceEquals(source.NativeGameClient, binding.Client) || source.AccountId != binding.AccountId ||
            source.Character.Uid != binding.Uid || !binding.Store.IsOwnedPhysicalMetadataCatalogCurrent(binding, catalog)) throw Invalid();
        var proof = new LegacyVigorCatalogWitness(binding, catalog);
        LegacyMaxHitpointsCalculator.CaptureVigorQueryMetadata(proof, equipment, catalog);
        proof.frozenRows = proof.rows.ToArray();
        if (!proof.IsCurrentPlain()) throw Invalid();
        return proof;
    }

    internal void CaptureFallback(uint baseId, int level)
    {
        if (catalog is null) return;
        if (index is null) CaptureContainers();
        ItemAddBonus? chosen = null;
        for (int offset = 0; offset <= 20; offset += 10)
        {
            int id = checked((int)checked(baseId + (uint)offset));
            var key = (id, level);
            index!.TryGetValue(key, out var indexed);
            ItemAddBonus? raw = null; int count = 0;
            foreach (var row in rawRows)
                if (row.ItemTypeId == id && row.Level == level) { raw = row; count++; }
            if (count > 1 || (indexed is null) != (count == 0) || indexed is not null && !ReferenceEquals(indexed, raw)) throw Invalid();
            if (indexed is not null) { chosen = indexed; break; }
        }
        if (chosen is not null) rows.Add(new ValuesProof(chosen));
    }

    private void CaptureContainers()
    {
        if (catalog!.ItemAddBonusByItemTypeIdAndLevel.GetType() != typeof(Dictionary<(int, int), ItemAddBonus>)) throw Invalid();
        index = (Dictionary<(int, int), ItemAddBonus>)catalog.ItemAddBonusByItemTypeIdAndLevel;
        if (!ReferenceEquals(index.Comparer, EqualityComparer<(int, int)>.Default)) throw Invalid();
        indexWitness = index.GetEnumerator(); entries = index.ToArray();
        var raw = catalog.ItemAddBonuses;
        if (raw.GetType() == typeof(ItemAddBonus[]))
        { rawArray = (ItemAddBonus[])raw; rawRows = (ItemAddBonus[])rawArray.Clone(); }
        else if (raw.GetType() == typeof(List<ItemAddBonus>))
        { rawList = (List<ItemAddBonus>)raw; rawWitness = rawList.GetEnumerator(); rawRows = rawList.ToArray(); }
        else throw Invalid();
        rawKeys = rawRows.Select(row => (row.ItemTypeId, row.Level)).ToArray();
    }

    internal bool IsCurrentPlain()
    {
        try
        {
            if (!binding.Store.IsOwnedPhysicalMetadataCatalogCurrent(binding, catalog)) return false;
            if (index is not null)
            {
                if (catalog is null || !ReferenceEquals(catalog.ItemAddBonusByItemTypeIdAndLevel, index) ||
                    !ReferenceEquals(index.Comparer, EqualityComparer<(int, int)>.Default) || index.Count != entries.Length) return false;
                var read = indexWitness; int at = 0;
                while (read.MoveNext())
                {
                    if (at == entries.Length || read.Current.Key != entries[at].Key ||
                        !ReferenceEquals(read.Current.Value, entries[at].Value)) return false;
                    at++;
                }
                if (at != entries.Length) return false;
                if (rawArray is not null)
                {
                    if (!ReferenceEquals(catalog.ItemAddBonuses, rawArray) || rawArray.Length != rawRows.Length) return false;
                    for (int i = 0; i < rawRows.Length; i++) if (!ReferenceEquals(rawArray[i], rawRows[i])) return false;
                }
                else
                {
                    if (!ReferenceEquals(catalog.ItemAddBonuses, rawList) || rawList!.Count != rawRows.Length) return false;
                    var raw = rawWitness; int i = 0;
                    while (raw.MoveNext())
                    { if (i == rawRows.Length || !ReferenceEquals(raw.Current, rawRows[i++])) return false; }
                    if (i != rawRows.Length) return false;
                }
            }
            for (int i = 0; i < rawRows.Length; i++)
                if (rawRows[i].ItemTypeId != rawKeys[i].ItemTypeId || rawRows[i].Level != rawKeys[i].Level) return false;
            foreach (var row in frozenRows) if (!row.IsCurrentPlain()) return false;
            return true;
        }
        catch (Exception error) when (error is InvalidOperationException or IndexOutOfRangeException or ArgumentException)
        { return false; }
    }

    private static InvalidDataException Invalid() => new("vigorCatalogWitnessUnavailable");
}

public static partial class LegacyMaxHitpointsCalculator
{
    internal static void CaptureVigorQueryMetadata(LegacyVigorCatalogWitness proof,
        IReadOnlyList<PlayerItemSnapshotItem> equipment, WorldDataCatalog? catalog)
    {
        if (catalog is null) return;
        foreach (var item in equipment)
        {
            if (item.Durability == 0 || item.Position % 50 != 12) continue;
            int plus = Math.Min((int)item.Plus, 12);
            if (plus == 0) continue;
            proof.CaptureFallback(GetLegacyItemAddBaseId(item.ItemId), plus);
        }
    }
}
