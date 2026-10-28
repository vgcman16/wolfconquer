using System.Collections.ObjectModel;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed class FullAccountStringMap
    {
        private readonly IReadOnlyDictionary<string, string> source;
        private readonly KeyValuePair<string, string>[] fields;
        internal readonly Dictionary<string, string> Copy = new(StringComparer.OrdinalIgnoreCase);

        internal FullAccountStringMap(IReadOnlyDictionary<string, string> source)
        {
            if (source is null) throw InvalidFullAccountClassState();
            this.source = source;
            var rows = new List<KeyValuePair<string, string>>();
            var iterator = Open();
            while (iterator.MoveNext())
            {
                var pair = iterator.Current;
                if (string.IsNullOrEmpty(pair.Key) || pair.Value is null || !Copy.TryAdd(pair.Key, pair.Value))
                    throw InvalidFullAccountClassState();
                rows.Add(pair);
            }
            fields = rows.ToArray();
        }

        internal Dictionary<string, string>.Enumerator Open()
        {
            if (source.GetType() == typeof(Dictionary<string, string>))
                return ((Dictionary<string, string>)source).GetEnumerator();
            if (source.GetType() != typeof(ReadOnlyDictionary<string, string>)) throw InvalidFullAccountClassState();
            // A ReadOnlyDictionary can wrap arbitrary IDictionary code. Acquire
            // its enumerator before the final fence, then require the real BCL
            // Dictionary enumerator so all subsequent reads are callback-free.
            if (source.GetEnumerator() is not Dictionary<string, string>.Enumerator iterator)
                throw InvalidFullAccountClassState();
            return iterator;
        }

        internal bool Matches(Dictionary<string, string>.Enumerator iterator)
        {
            var index = 0;
            while (iterator.MoveNext())
            {
                if (index == fields.Length || iterator.Current.Key != fields[index].Key ||
                    iterator.Current.Value != fields[index++].Value) return false;
            }
            return index == fields.Length;
        }
    }

    private sealed class LegacyFullAccountClassInputs
    {
        private const int MaximumInputBytes = 16 * 1024 * 1024;
        private const int MaximumItemRows = MaximumInputBytes / PlayerItemSnapshotLoader.LegacyDbItemSize;
        private readonly WorldSession expected;
        private readonly FullAccountStringMap attributes, paid;
        private readonly LegacyPerformerSpellSource spells;
        private readonly LegacyItemSaveGraph itemGraph;
        private readonly byte[] itemImage;
        private readonly string gearText;
        internal WorldSession Stable { get; }
        internal PlayerSpellSnapshotEntry[] SpellRows => spells.Rows;

        internal LegacyFullAccountClassInputs(WorldSession expected)
        {
            if (expected.Character?.Attributes is null || expected.Character.PaidNobility is null ||
                expected.PlayerSpells?.Spells is null || expected.PlayerItems is null) throw InvalidFullAccountClassState();
            this.expected = expected;
            var rawSpells = expected.PlayerSpells.Spells;
            var spellRows = rawSpells is LegacyPerformerSpellRows frozen ? frozen.Values : FullAccountKnownRows(rawSpells, ushort.MaxValue);
            var spellIds = new HashSet<ushort>();
            foreach (var row in spellRows)
            {
                if (row is null) throw InvalidFullAccountClassState();
                if (!spellIds.Add(row.Id)) throw new FullAccountClassRefusal("duplicate-player-spell-id");
            }
            spells = new(rawSpells);
            ValidateItemShape(expected.PlayerItems);
            if (!LegacyItemSaveGraph.TryCapture(expected.PlayerItems, out var capturedItems)) throw InvalidFullAccountClassState();
            itemGraph = capturedItems;
            itemImage = PlayerItemSnapshotLoader.ToLegacyBytes(expected.PlayerItems);
            if (itemImage.Length > MaximumInputBytes || !itemGraph.HasBytes(itemImage)) throw InvalidFullAccountClassState();
            var items = PlayerItemSnapshotLoader.Parse(itemImage);
            var gear = CopyFullAccountGear(expected.CombatGear ?? CombatGearSnapshot.Empty);
            gearText = CombatGearSnapshotLoader.ToLegacyText(gear);
            if (gearText.Length > MaximumInputBytes) throw InvalidFullAccountClassState();
            // Capture callback-free storage first; wrapped attribute enumeration
            // may execute code, so it cannot establish an earlier item baseline.
            attributes = new(expected.Character.Attributes); paid = new(expected.Character.PaidNobility);
            Stable = expected with
            {
                Character = expected.Character with { Attributes = attributes.Copy, PaidNobility = paid.Copy },
                PlayerSpells = new(new LegacyPerformerSpellRows(spells.Rows)),
                PlayerItems = items, CombatGear = gear,
            };
            if (!Matches()) throw InvalidFullAccountClassState();
        }

        internal bool Matches()
        {
            // Obtain both possibly wrapped enumerators BEFORE any plain compare.
            var first = attributes.Open(); var second = paid.Open();
            if (!attributes.Matches(first) || !paid.Matches(second)) return false;
            ValidateItemShape(expected.PlayerItems);
            _ = CopyFullAccountGear(expected.CombatGear ?? CombatGearSnapshot.Empty);
            return itemGraph.IsCurrent(expected.PlayerItems) && itemGraph.HasBytes(itemImage) &&
                CombatGearSnapshotLoader.ToLegacyText(expected.CombatGear ?? CombatGearSnapshot.Empty) == gearText && spells.IsCurrent();
        }

        private static void ValidateItemShape(PlayerItemSnapshot items)
        {
            var remaining = MaximumItemRows;
            IReadOnlyList<PlayerItemSnapshotItem>[] lists = [items.InventoryItems, items.RelicFuseItems,
                items.MythsoulBagItems, items.EquipmentItems, items.RuneItems, items.WardrobeItems, items.WarehouseItems];
            foreach (var list in lists)
            {
                var rows = FullAccountKnownRows(list, remaining); remaining -= rows.Length;
                foreach (var row in rows)
                {
                    if (row is null) throw InvalidFullAccountClassState();
                    var deposits = FullAccountKnownRows(row.DepositedItems, remaining); remaining -= deposits.Length;
                    foreach (var child in deposits)
                        if (child is null || FullAccountKnownRows(child.DepositedItems, 0).Length != 0)
                            throw InvalidFullAccountClassState();
                }
            }
        }

        private static CombatGearSnapshot CopyFullAccountGear(CombatGearSnapshot source)
        {
            if (source is null || source.AirPowers is null) throw InvalidFullAccountClassState();
            var rows = FullAccountKnownRows(source.Items, MaximumItemRows);
            if (source.Count != rows.Length || source.BlackJadeBag is null) throw InvalidFullAccountClassState();
            var copied = new CombatGearSnapshotItem[rows.Length];
            var budget = MaximumInputBytes / sizeof(uint);
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index] ?? throw InvalidFullAccountClassState();
                var jades = FullAccountKnownRows(row.BlackJades, budget); budget -= jades.Length;
                var animas = FullAccountKnownRows(row.Animas, budget); budget -= animas.Length;
                var values = new IReadOnlyList<uint>[animas.Length];
                for (var slot = 0; slot < values.Length; slot++)
                { var value = FullAccountKnownRows(animas[slot], budget); budget -= value.Length; values[slot] = value; }
                copied[index] = row with { Animas = values, BlackJades = jades };
            }
            var bag = FullAccountKnownRows(source.BlackJadeBag.BlackJades, budget);
            if (source.BlackJadeBag.Count != bag.Length) throw InvalidFullAccountClassState();
            return source with { Items = copied, BlackJadeBag = source.BlackJadeBag with { BlackJades = bag } };
        }
    }

    private static T[] FullAccountKnownRows<T>(IReadOnlyList<T> source, int maximum)
    {
        if (source is T[] array)
        {
            if (array.Length > maximum) throw InvalidFullAccountClassState();
            return array.ToArray();
        }
        if (source is List<T> list && list.GetType() == typeof(List<T>))
        {
            if (list.Count > maximum) throw InvalidFullAccountClassState();
            return list.ToArray();
        }
        throw InvalidFullAccountClassState();
    }

    private static InvalidOperationException InvalidFullAccountClassState() =>
        new("FullAccount class reset requires complete stable current input state.");
}
