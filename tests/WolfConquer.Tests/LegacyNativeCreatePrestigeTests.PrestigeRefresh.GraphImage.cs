using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    // Unlike the valid-owner RankPublicationGraph fixture, this image also retains
    // null/mistyped owners so assertions can inspect deliberately corrupt graphs.
    private sealed class PrestigeRefreshGraphImage
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> graph = Ranking.Ranks;
        private readonly object gate = Ranking.SynRoot;
        private readonly object cacheGate;
        private readonly Ranking.Rank[] cache;
        private readonly Ranking.Rank[] cacheMembers;
        private readonly bool update;
        private readonly BucketImage[] buckets;
        private readonly EntryImage[] entries;

        internal PrestigeRefreshGraphImage()
        {
            cacheGate = graph.SyncRoot;
            cache = graph.MyArray;
            cacheMembers = cache.ToArray();
            update = graph.Update;
            buckets = graph.Select(pair => new BucketImage(pair.Key, pair.Value)).ToArray();
            entries = buckets.SelectMany(bucket => bucket.Rows.Select(pair => pair.Value)
                    .Append(bucket.Champion!)).Where(row => row is not null).Distinct()
                .Select(row => new EntryImage(row)).ToArray();
        }

        internal void RequireUnchanged()
        {
            RequireOwnersAndCacheUnchanged();
            foreach (var bucket in buckets)
            {
                if (bucket.Owner is null) continue;
                Assert.Same(bucket.Items, bucket.Owner.Items);
                Assert.Same(bucket.Champion, bucket.Owner.BestOfClass);
            }
            RequireEscapedContentsUnchanged();
        }

        internal void RequireOwnersAndCacheUnchanged()
        {
            Assert.Same(graph, Ranking.Ranks);
            Assert.Same(gate, Ranking.SynRoot);
            Assert.Same(cacheGate, graph.SyncRoot);
            Assert.Equal(update, graph.Update);
            Assert.Same(cache, graph.MyArray);
            Assert.Equal(cacheMembers.Length, cache.Length);
            for (var index = 0; index < cache.Length; index++) Assert.Same(cacheMembers[index], cache[index]);
            Assert.Equal(buckets.Select(bucket => bucket.Key), graph.Keys);
            foreach (var bucket in buckets)
            {
                Assert.Same(bucket.Owner, graph[bucket.Key]);
                if (bucket.Owner is null) continue;
                Assert.Equal(bucket.Category, bucket.Owner._Type);
                Assert.Equal(bucket.Limit, bucket.Owner.MaxItems);
            }
        }

        internal void RequireEscapedContentsUnchanged()
        {
            foreach (var bucket in buckets)
            {
                if (bucket.Items is null) continue;
                Assert.Equal(bucket.Rows.Select(pair => pair.Key), bucket.Items.Keys);
                foreach (var pair in bucket.Rows) Assert.Same(pair.Value, bucket.Items[pair.Key]);
            }
            foreach (var entry in entries) entry.RequireUnchanged();
        }

        private sealed class BucketImage
        {
            internal readonly Ranking.Type Key;
            internal readonly Ranking.Rank? Owner;
            internal readonly Ranking.Type? Category;
            internal readonly int? Limit;
            internal readonly Dictionary<uint, Ranking.Entry>? Items;
            internal readonly KeyValuePair<uint, Ranking.Entry>[] Rows;
            internal readonly Ranking.Entry? Champion;

            internal BucketImage(Ranking.Type key, Ranking.Rank? owner)
            {
                Key = key;
                Owner = owner;
                Category = owner?._Type;
                Limit = owner?.MaxItems;
                Items = owner?.Items;
                Rows = Items?.ToArray() ?? [];
                Champion = owner?.BestOfClass;
            }
        }

        private sealed class EntryImage
        {
            private readonly Ranking.Entry entry;
            private readonly object?[] values;
            private readonly uint[]? points;
            private readonly uint[]? pointValues;

            internal EntryImage(Ranking.Entry entry)
            {
                this.entry = entry;
                values = ClientPreparationEntryValues(entry);
                points = entry.Points;
                pointValues = points?.ToArray();
            }

            internal void RequireUnchanged()
            {
                Assert.Equal(values, ClientPreparationEntryValues(entry));
                Assert.Same(points, entry.Points);
                Assert.Equal(pointValues, entry.Points);
            }
        }
    }
}
