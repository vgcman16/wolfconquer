namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    internal sealed partial class MailboxLoadProof
    {
        private static bool MatchDictionaries(MailboxGraph graph, WorldDataCatalog catalog, CatalogRead read)
        {
            var jades = read.Rows(catalog.DaoqiDictTypes);
            var lotteries = read.Rows(catalog.LotteryDictionary);
            var collections = read.Rows(catalog.CollectionRewardTiers);
            if (jades.Length != graph.Jade!.Count || lotteries.Length != graph.Lottery!.Count ||
                collections.Length != graph.Collection!.Count) return false;
            foreach (var row in jades)
            {
                if (row.Id < 0 || !graph.Jade.TryGetValue((uint)row.Id, out var n) || n is null) return false;
                uint[] actual = [n.ID, (uint)n.Type, n.attr_type1, n.attr_value1, n.attr_type2, n.attr_value2,
                    n.score, n.points, n.rate, n.TypeRune, n.Level, n.Exp, n.u12, n.u13, n.TypeCombatGear];
                var fields = read.Numbers(row.Fields, 15); var values = read.Rows(row.Values);
                // The existing DTO's Level name denotes raw column2; runtime
                // rune consumers read actual field10. Preserve both exact views.
                if (!fields.AsSpan().SequenceEqual(actual) || row.Id != fields[0] || row.Type != fields[1] || row.Level != fields[2] ||
                    values.Length != 12 || !values.Select(value => checked((uint)value)).SequenceEqual(fields.Skip(3))) return false;
            }
            foreach (var row in lotteries)
            {
                if (row.Id < 0 || !graph.Lottery.TryGetValue((uint)row.Id, out var n) || n is null) return false;
                uint[] values = [n.Id, n.Type, n.Value1, n.Value2, n.Value3, n.rate];
                if (!read.Numbers(row.Fields, 6).AsSpan().SequenceEqual(values) || row.Id != n.Id || row.Type != n.Type ||
                    row.SubType != n.Value1 || row.Value != n.Value2 || row.Unknown0 != n.Value3 || row.Weight != n.rate) return false;
            }
            foreach (var row in collections)
            {
                if (row.Id < 0 || !graph.Collection.TryGetValue((uint)row.Id, out var n) || n is null) return false;
                uint[] values = [n.Id, n.Type, n.Min, n.Mix, (uint)n.attr_type1, n.attr_value1, n.value2];
                if (!read.Numbers(row.Fields, 7).AsSpan().SequenceEqual(values) || row.Id != n.Id || row.CollectionId != n.Type ||
                    row.MinInclusive != n.Min || row.MaxInclusive != n.Mix || row.RewardType != (uint)n.attr_type1 ||
                    row.RewardValue != n.attr_value1 || row.RewardAmount != n.value2) return false;
            }
            return read.Unique(catalog.DaoqiDictTypesById, jades, row => row.Id) &&
                read.Grouped(catalog.DaoqiDictTypesByType, jades, row => row.Type) &&
                read.Grouped(catalog.DaoqiDictTypesByLevel, jades, row => row.Level) &&
                read.Unique(catalog.LotteryDictionaryById, lotteries, row => row.Id) &&
                read.Grouped(catalog.LotteryDictionaryByType, lotteries, row => row.Type) &&
                read.Unique(catalog.CollectionRewardTiersById, collections, row => row.Id) &&
                read.Unique(catalog.CollectionRewardTierByCollectionIdAndMinValue, collections, row => (row.CollectionId, row.MinInclusive)) &&
                read.Grouped(catalog.CollectionRewardTiersByCollectionId, collections.OrderBy(row => row.MinInclusive)
                    .ThenBy(row => row.MaxInclusive).ThenBy(row => row.Id).ToArray(), row => row.CollectionId);
        }
    }
}
