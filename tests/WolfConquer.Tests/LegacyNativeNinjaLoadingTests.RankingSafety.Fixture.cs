using System.Reflection;
using WolfConquer.Core.Data;
using RankType = WolfConquer.Core.Data.LegacyNativeNinjaRank.Type;
using RankEntry = WolfConquer.Core.Data.LegacyNativeNinjaRank.Entry;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaLoadingTests
{
    private static readonly RankType[] RankingCategories = Enum.GetValues<RankType>()
        .Where(type => type != RankType.Count).ToArray();

    private static void SeedRankingRows(Scope scope)
    {
        foreach (var category in RankingCategories)
        {
            var map = scope.Rank(category).RankingList;
            map[18] = new RankEntry { UID = 18, Type = category, Name = "Other", TotalPoints = 3000 };
            map[17] = new RankEntry { UID = 17, Type = category, Name = "Former", TotalPoints = 2000 };
        }
    }

    private static void AssertNoRankingPackets(Scope scope)
    {
        Assert.Empty(scope.Queue);
        Assert.Equal(12, scope.Available);
        scope.Untaken(0);
    }

    private sealed class RankingGraphWitness
    {
        private readonly LegacyNativeDbSafeDictionary<RankType, LegacyNativeNinjaRank.Rank> registry = LegacyNativeNinjaRank.Ranks;
        private readonly bool updated;
        private readonly long revision;
        private readonly LegacyNativeNinjaRank.Rank[] cache;
        private readonly KeyValuePair<RankType, LegacyNativeNinjaRank.Rank>[] owners;
        private readonly Dictionary<LegacyNativeNinjaRank.Rank, RankState> ranks = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<RankEntry, Dictionary<FieldInfo, object?>> rows = new(ReferenceEqualityComparer.Instance);
        private sealed record RankState(RankType Category, int Limit, object? Gate,
            Dictionary<uint, RankEntry>? Map, KeyValuePair<uint, RankEntry>[] Entries);

        internal RankingGraphWitness()
        {
            updated = registry.Update;
            revision = registry.Revision;
            cache = registry.MyArray;
            owners = registry.ToArray();
            foreach (var owner in owners.Select(pair => pair.Value).Where(owner => owner is not null)
                .Distinct<LegacyNativeNinjaRank.Rank>(ReferenceEqualityComparer.Instance))
            {
                var map = owner.RankingList;
                var entries = map?.ToArray() ?? [];
                ranks.Add(owner, new(owner._Type, owner.MaxItems, owner.SynRoot, map, entries));
                foreach (var row in entries.Select(pair => pair.Value).Where(row => row is not null))
                    rows.TryAdd(row, row.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
                        .ToDictionary(field => field, field => field.GetValue(row)));
            }
        }

        internal void RequireUnchanged()
        {
            Assert.Same(registry, LegacyNativeNinjaRank.Ranks);
            Assert.Equal(updated, registry.Update);
            Assert.Equal(cache, registry.MyArray);
            Assert.Equal(revision, registry.Revision);
            var current = registry.ToArray();
            Assert.Equal(owners.Select(pair => pair.Key), current.Select(pair => pair.Key));
            for (var index = 0; index < owners.Length; index++) Assert.Same(owners[index].Value, current[index].Value);
            foreach (var pair in ranks)
            {
                Assert.Equal(pair.Value.Category, pair.Key._Type);
                Assert.Equal(pair.Value.Limit, pair.Key.MaxItems);
                Assert.Same(pair.Value.Gate, pair.Key.SynRoot);
                Assert.Same(pair.Value.Map, pair.Key.RankingList);
                if (pair.Value.Map is null) continue;
                Assert.Equal(pair.Value.Entries.Select(row => row.Key), pair.Key.RankingList.Keys);
                foreach (var row in pair.Value.Entries) Assert.Same(row.Value, pair.Key.RankingList[row.Key]);
            }
            foreach (var row in rows)
            foreach (var field in row.Value) Assert.Equal(field.Value, field.Key.GetValue(row.Key));
        }
    }
}
