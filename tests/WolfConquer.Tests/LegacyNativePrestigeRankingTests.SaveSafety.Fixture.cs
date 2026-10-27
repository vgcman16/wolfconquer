using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    private static void AssertOwnedPrestigeSaveRefusal(OwnedRankFile files)
    {
        var graph = new PrestigeSaveGraph();
        var error = Record.Exception(Ranking.Save);
        files.Expect(Text(FullRow));
        graph.AssertUnchanged();
        Assert.IsType<InvalidOperationException>(error);
    }

    private static void RunOwnedPrestigeSave(Action<OwnedRankFile> action)
    {
        State? state = null;
        OwnedRankFile? files = null;
        var failures = new List<Exception>();
        try
        {
            state = new State();
            files = new OwnedRankFile();
            action(files);
        }
        catch (Exception error)
        {
            failures.Add(error);
        }
        finally
        {
            try { files?.Dispose(); }
            catch (Exception error) { failures.Add(error); }
            try { state?.Dispose(); }
            catch (Exception error) { failures.Add(error); }
        }

        if (failures.Count == 1)
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Owned prestige Save assertion and cleanup failed; guarded unexpected files remain preserved.", failures);
    }

    private sealed class PrestigeSaveGraph
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> graph = Ranking.Ranks;
        private readonly (Ranking.Type Key, Ranking.Rank? Rank,
            Dictionary<uint, Ranking.Entry>? Items, KeyValuePair<uint, Ranking.Entry>[] Rows,
            Ranking.Entry? Champion)[] buckets;
        private readonly (Ranking.Entry Row, object[] Values, uint[]? Points, uint[]? PointValues)[] rows;

        internal PrestigeSaveGraph()
        {
            buckets = graph.Select(pair => (pair.Key, pair.Value, pair.Value?.Items,
                pair.Value?.Items?.ToArray() ?? [], pair.Value?.BestOfClass)).ToArray();
            rows = buckets.SelectMany(bucket => bucket.Rows).Select(pair => pair.Value)
                .Where(row => row is not null).Distinct()
                .Select(row => (row, Values(row), row.Points, row.Points?.ToArray())).ToArray();
        }

        internal void AssertUnchanged()
        {
            Assert.Same(graph, Ranking.Ranks);
            Assert.Equal(buckets.Select(bucket => bucket.Key), graph.Keys);
            foreach (var bucket in buckets)
            {
                Assert.Same(bucket.Rank, graph[bucket.Key]);
                if (bucket.Rank is null) continue;
                Assert.Same(bucket.Items, bucket.Rank.Items);
                Assert.Same(bucket.Champion, bucket.Rank.BestOfClass);
                if (bucket.Items is null) continue;
                Assert.Equal(bucket.Rows.Select(pair => pair.Key), bucket.Rank.Items.Keys);
                foreach (var pair in bucket.Rows)
                    Assert.Same(pair.Value, bucket.Rank.Items[pair.Key]);
            }
            foreach (var saved in rows)
            {
                Assert.Equal(saved.Values, Values(saved.Row));
                Assert.Same(saved.Points, saved.Row.Points);
                Assert.Equal(saved.PointValues, saved.Row.Points);
            }
        }
    }
}
