using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// The real admitted catalog supplies the selected item's wire-only mapping.
// This witness neither grants UID allocation nor reinterprets saved spell rows.
internal sealed class LegacyRuneBagWireEvidence
{
    private readonly WorldPacketHandlerContext context;
    private readonly WorldSession expected;
    private readonly LegacyOrdinarySpellBinding binding;
    private readonly WorldDataCatalog catalog;
    private readonly Rows<ItemType> raw, group;
    private readonly Dictionary<int, IReadOnlyList<ItemType>> index;
    private readonly KeyValuePair<int, IReadOnlyList<ItemType>>[] indexed;
    private readonly ItemType item;
    private readonly Rows<string> fields;
    internal LegacyGameItemPacket.RuneSpellOverride? RuneOverride { get; }
    internal LegacyRuneBagMetadata Metadata { get; }

    private LegacyRuneBagWireEvidence(WorldPacketHandlerContext context, WorldSession stable, uint itemId)
    {
        this.context = context;
        binding = context.Connection.NativeGameClient?.OrdinarySpellBinding ?? throw Invalid();
        catalog = context.WorldData ?? throw Invalid();
        if (!ReferenceEquals(context.Sessions, binding.Store) || !ReferenceEquals(binding.ItemCatalog, catalog) ||
            !binding.Store.TryGet(context.Connection, out var current) || current is null ||
            current.Character.Uid != stable.Character.Uid || !ReferenceEquals(current.NativeGameClient, binding.Client) ||
            !binding.Store.IsBoundSpellOwnerCurrent(binding)) throw Invalid();
        expected = current; raw = new(catalog.Items);
        index = catalog.ItemsById as Dictionary<int, IReadOnlyList<ItemType>> ?? throw Invalid();
        if (index.GetType() != typeof(Dictionary<int, IReadOnlyList<ItemType>>) ||
            !ReferenceEquals(index.Comparer, EqualityComparer<int>.Default) || itemId is 0 or > int.MaxValue ||
            raw.Values.Any(row => row is null) || !index.TryGetValue((int)itemId, out var source)) throw Invalid();
        indexed = index.ToArray(); group = new(source);
        if (group.Values.Length != 1 || group.Values[0] is not { } selected || selected.Id != itemId ||
            raw.Values.Count(row => row.Id == itemId) != 1 ||
            !raw.Values.Any(row => row.Id == itemId && ReferenceEquals(row, selected))) throw Invalid();
        item = selected; fields = new(item.Fields);
        if (fields.Values.Length == 0 || !uint.TryParse(fields.Values[0], NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var rawId) || rawId != itemId) throw Invalid();
        if (fields.Values.Length <= 46 || !ushort.TryParse(fields.Values[46], NumberStyles.None,
            CultureInfo.InvariantCulture, out var rawCap)) throw Invalid();
        Metadata = new(itemId, rawCap);
        if (itemId / 10_000 is >= 401 and <= 403)
        {
            if (fields.Values.Length <= ItemType.RuneSpellIdFieldIndex ||
                !uint.TryParse(fields.Values[ItemType.RuneSpellIdFieldIndex], NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var encoded)) throw Invalid();
            RuneOverride = LegacyGameItemPacket.CreateRuneSpellOverride(itemId, encoded);
        }
        if (!IsCurrent()) throw Invalid();
    }

    internal static LegacyRuneBagWireEvidence Capture(WorldPacketHandlerContext context, WorldSession stable,
        uint itemId) => new(context, stable, itemId);

    internal bool IsCurrent()
    {
        if (!ReferenceEquals(context.WorldData, catalog) || !ReferenceEquals(binding.ItemCatalog, catalog) ||
            !binding.Store.IsBoundSpellOwnerCurrent(binding) || !binding.Store.TryGet(context.Connection, out var current) ||
            !ReferenceEquals(current, expected) || !binding.Items.Image.IsCurrentPlain() ||
            !ReferenceEquals(catalog.Items, raw.Source) || !raw.IsCurrent() ||
            !ReferenceEquals(catalog.ItemsById, index) || index.Count != indexed.Length ||
            !group.IsCurrent() || !ReferenceEquals(item.Fields, fields.Source) || !fields.IsCurrent()) return false;
        int position = 0;
        foreach (var entry in index)
        {
            var before = indexed[position++];
            if (entry.Key != before.Key || !ReferenceEquals(entry.Value, before.Value)) return false;
        }
        return true;
    }

    private sealed class Rows<T> where T : class
    {
        internal readonly IReadOnlyList<T> Source;
        internal readonly T[] Values;
        private readonly T[]? array;
        private readonly List<T>? list;
        internal Rows(IReadOnlyList<T> source)
        {
            Source = source; array = source as T[]; list = source as List<T>;
            if (array is null && (list is null || list.GetType() != typeof(List<T>))) throw Invalid();
            Values = new T[array?.Length ?? list!.Count];
            for (var i = 0; i < Values.Length; i++) Values[i] = array is not null ? array[i] : list![i];
        }
        internal bool IsCurrent()
        {
            if ((array?.Length ?? list!.Count) != Values.Length) return false;
            for (var i = 0; i < Values.Length; i++)
                if (!ReferenceEquals(Values[i], array is not null ? array[i] : list![i])) return false;
            return true;
        }
    }

    private static InvalidOperationException Invalid() => new("runeBagWireRequiresCurrentAdoptedCatalog");
}
