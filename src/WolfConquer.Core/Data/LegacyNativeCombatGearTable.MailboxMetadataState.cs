namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    // Legacy aliases remain mutable. Authority therefore pins exact owners AND
    // values; an unchanged aggregate pointer alone cannot bless mutated rows.
    private sealed class MailboxMetadataState
    {
        private readonly object[] owners;
        private readonly uint[] values;
        private MailboxMetadataState(object[] owners, uint[] values) { this.owners = owners; this.values = values; }

        internal static MailboxMetadataState Capture(MailboxGraph graph)
        {
            var state = Read(graph);
            return new(state.Owners, state.Values);
        }

        internal bool Matches(MailboxGraph graph)
        {
            var current = Read(graph);
            if (current.Owners.Length != owners.Length || !current.Values.AsSpan().SequenceEqual(values)) return false;
            for (var index = 0; index < owners.Length; index++)
                if (!ReferenceEquals(owners[index], current.Owners[index])) return false;
            return true;
        }

        private static (object[] Owners, uint[] Values) Read(MailboxGraph graph)
        {
            if (graph.Gear is null || graph.Jade is null || graph.Lottery is null || graph.Collection is null || graph.Ranks is null)
                throw new InvalidDataException("Complete gear metadata owners are absent.");
            var owners = new List<object> { graph.Gear, graph.Jade, graph.Lottery, graph.Collection, graph.Ranks };
            var values = new List<uint>();
            var budget = 524_288; // At most one level-map family per staged metadata row.
            void Count(int count)
            {
                if (count < 0 || count > budget) throw new InvalidDataException("Gear metadata row bound changed.");
                budget -= count;
                values.Add(checked((uint)count));
            }
            Count(graph.Gear.Count);
            foreach (var family in graph.Gear.OrderBy(pair => pair.Key))
            {
                if (family.Value is null) throw new InvalidDataException("Gear level map is absent.");
                owners.Add(family.Value); values.Add(family.Key); Count(family.Value.Count);
                foreach (var pair in family.Value.OrderBy(pair => pair.Key))
                {
                    var row = pair.Value;
                    if (row is null || row.Type != family.Key || row.Level != pair.Key || row.Skills is not { Length: 10 })
                        throw new InvalidDataException("Gear metadata mapping is invalid.");
                    owners.Add(row); owners.Add(row.Skills);
                    values.AddRange([pair.Key, row.ID, row.Type, row.Level, row.Exp, row.MaxHP, row.PAttack, row.PDefense, row.MDefense]);
                    values.AddRange(row.Skills); values.Add(row.Score);
                }
            }
            Count(graph.Jade.Count);
            foreach (var pair in graph.Jade.OrderBy(pair => pair.Key))
            {
                var row = pair.Value;
                if (row is null || row.ID != pair.Key) throw new InvalidDataException("Jade metadata mapping is invalid.");
                owners.Add(row);
                values.AddRange([pair.Key, row.ID, (uint)row.Type, row.attr_type1, row.attr_value1, row.attr_type2, row.attr_value2,
                    row.score, row.points, row.rate, row.TypeRune, row.Level, row.Exp, row.u12, row.u13, row.TypeCombatGear]);
            }
            Count(graph.Lottery.Count);
            foreach (var pair in graph.Lottery.OrderBy(pair => pair.Key))
            {
                var row = pair.Value;
                if (row is null || row.Id != pair.Key) throw new InvalidDataException("Lottery metadata mapping is invalid.");
                owners.Add(row); values.AddRange([pair.Key, row.Id, row.Type, row.Value1, row.Value2, row.Value3, row.rate]);
            }
            Count(graph.Collection.Count);
            foreach (var pair in graph.Collection.OrderBy(pair => pair.Key))
            {
                var row = pair.Value;
                if (row is null || row.Id != pair.Key) throw new InvalidDataException("Collection metadata mapping is invalid.");
                owners.Add(row); values.AddRange([pair.Key, row.Id, row.Type, row.Min, row.Mix, (uint)row.attr_type1, row.attr_value1, row.value2]);
            }
            if (graph.Ranks.Count != Enum.GetValues<LegacyNativeCombatGearRankTable.Type>().Length)
                throw new InvalidDataException("Complete rank owners are absent.");
            foreach (var pair in graph.Ranks.OrderBy(pair => (uint)pair.Key))
            {
                var rank = pair.Value;
                if (rank is null || rank._Type != pair.Key || rank.SynRoot is null || rank.MaxItems != 10)
                    throw new InvalidDataException("Rank owner shape is invalid.");
                owners.Add(rank); owners.Add(rank.SynRoot);
                values.AddRange([(uint)pair.Key, (uint)rank._Type, checked((uint)rank.MaxItems)]);
                // RankingList/entries are mutable runtime state, not immutable
                // lookup metadata. Ordinary source rank updates stay independent.
            }
            return (owners.ToArray(), values.ToArray());
        }
    }
}
