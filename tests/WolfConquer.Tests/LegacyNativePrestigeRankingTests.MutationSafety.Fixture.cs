using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    private static void RunPrestigeMutationSafety(Action body)
    {
        State? state = null;
        var errors = new List<Exception>();
        try { state = new(); body(); } catch (Exception error) { errors.Add(error); }
        try { state?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1)
            throw new AggregateException("Prestige mutation assertion and serialized state restore failed.", errors);
    }

    private sealed class PrestigeMutationRankImage(Ranking.Rank rank)
    {
        private readonly Ranking.Type category = rank._Type;
        private readonly int limit = rank.MaxItems;
        private readonly Ranking.Entry? champion = rank.BestOfClass;
        private readonly Dictionary<uint, Ranking.Entry>? items = rank.Items;
        private readonly KeyValuePair<uint, Ranking.Entry>[] rows = rank.Items?.ToArray() ?? [];
        private readonly PrestigeMutationEntryImage[] entries = (rank.Items?.ToArray() ?? []).Select(pair => pair.Value)
            .Append(rank.BestOfClass!).Where(entry => entry is not null).Distinct()
            .Select(entry => new PrestigeMutationEntryImage(entry)).ToArray();

        internal void RequireUnchanged()
        {
            Assert.Equal(category, rank._Type);
            Assert.Equal(limit, rank.MaxItems);
            Assert.Same(champion, rank.BestOfClass);
            Assert.Same(items, rank.Items);
            RequireEscapedContentsUnchanged();
        }

        internal void RequireEscapedContentsUnchanged()
        {
            if (items is not null)
            {
                Assert.Equal(rows.Select(pair => pair.Key), items.Keys);
                foreach (var pair in rows) Assert.Same(pair.Value, items[pair.Key]);
            }
            foreach (var entry in entries) entry.RequireUnchanged();
        }
    }

    private sealed class PrestigeMutationEntryImage(Ranking.Entry entry)
    {
        private readonly object[] values = Values(entry);
        private readonly uint[]? points = entry.Points;
        private readonly uint[]? pointValues = entry.Points?.ToArray();

        internal void RequireUnchanged()
        {
            Assert.Equal(values, Values(entry));
            Assert.Same(points, entry.Points);
            Assert.Equal(pointValues, entry.Points);
        }
    }

    private sealed class PrestigeMutationRegistryImage
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> graph = Ranking.Ranks;
        private readonly object gate = Ranking.SynRoot;
        private readonly object cacheGate = Ranking.Ranks.SyncRoot;
        private readonly Ranking.Rank[] cache = Ranking.Ranks.MyArray;
        private readonly Ranking.Rank[] cacheMembers = Ranking.Ranks.MyArray.ToArray();
        private readonly bool update = Ranking.Ranks.Update;
        private readonly KeyValuePair<Ranking.Type, Ranking.Rank>[] buckets = Ranking.Ranks.ToArray();
        private readonly PrestigeMutationRankImage[] owners = Ranking.Ranks.Values.Where(rank => rank is not null)
            .Distinct().Select(rank => new PrestigeMutationRankImage(rank)).ToArray();

        internal void RequireUnchanged()
        {
            RequireRegistryAndCacheUnchanged();
            foreach (var owner in owners) owner.RequireUnchanged();
        }

        internal void RequireRegistryAndCacheUnchanged()
        {
            Assert.Same(graph, Ranking.Ranks);
            Assert.Same(gate, Ranking.SynRoot);
            Assert.Same(cacheGate, graph.SyncRoot);
            Assert.Equal(update, graph.Update);
            Assert.Same(cache, graph.MyArray);
            Assert.Equal(cacheMembers.Length, cache.Length);
            for (var index = 0; index < cache.Length; index++) Assert.Same(cacheMembers[index], cache[index]);
            Assert.Equal(buckets.Select(pair => pair.Key), graph.Keys);
            foreach (var pair in buckets) Assert.Same(pair.Value, graph[pair.Key]);
        }

        internal void RequireEscapedContentsUnchanged()
        {
            foreach (var owner in owners) owner.RequireEscapedContentsUnchanged();
        }
    }
}
