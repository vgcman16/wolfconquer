using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    private static void RunOwnedPrestigeSaveFile(Action<OwnedRankFile> action)
    {
        State? state = null;
        OwnedRankFile? files = null;
        var failures = new List<Exception>();
        try { state = new(); files = new(); action(files); }
        catch (Exception error) { failures.Add(error); }
        try { files?.Dispose(); } catch (Exception error) { failures.Add(error); }
        try { state?.Dispose(); } catch (Exception error) { failures.Add(error); }
        ThrowSaveFileFailures(failures);
    }

    private static void ThrowSaveFileFailures(List<Exception> failures)
    {
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException("Prestige Save file action and owned cleanup failed; unexpected outputs remain preserved.", failures);
    }

    private static void WithOwnedPrestigeSaveTargetDirectory(OwnedRankFile files, Action action)
    {
        var path = files.PathFor();
        GuardPrestigeSaveTargetDirectory(files, path);
        Assert.False(File.Exists(path) || Directory.Exists(path));
        Directory.CreateDirectory(path);
        var failures = new List<Exception>();
        try { action(); } catch (Exception error) { failures.Add(error); }
        try
        {
            GuardPrestigeSaveTargetDirectory(files, path);
            Assert.True(Directory.Exists(path));
            Assert.Empty(Directory.GetFileSystemEntries(path));
            Directory.Delete(path, recursive: false);
        }
        catch (Exception error)
        {
            failures.Add(new InvalidOperationException($"Owned Save target directory retained at '{path}'.", error));
        }
        ThrowSaveFileFailures(failures);
    }

    private static void GuardPrestigeSaveTargetDirectory(OwnedRankFile files, string path)
    {
        var root = Path.GetFullPath(files.Root);
        Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(), Path.GetDirectoryName(root));
        Assert.Matches("^native-prestige-rank-[0-9a-f]{32}$", Path.GetFileName(root));
        Assert.Equal(Path.Combine(root, "PrestigeRanking.txt"), Path.GetFullPath(path));
        LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
    }

    private static Ranking.Entry SeedPrestigeSaveFileGraph(string name = "Hero", string guild = "Guild")
    {
        var row = Entry();
        row.Name = name;
        row.GuildName = guild;
        var rank = Place(row);
        rank.BestOfClass = row;
        var world = Place(Entry(88), Ranking.Type.World);
        world.BestOfClass = world.Items[88]; // A world-only row is not persisted.
        _ = Ranking.Ranks.GetValues();
        return row;
    }

    private static string[] PrestigeSaveFileEntries(OwnedRankFile files) =>
        Directory.GetFileSystemEntries(files.Root).Select(path => Path.GetFileName(path)!)
            .Order(StringComparer.Ordinal).ToArray();

    private static void RequireActualSaveFileSystemRefusal(Exception? failure)
    {
        Assert.NotNull(failure);
        Assert.True(failure is IOException or UnauthorizedAccessException,
            "The actual filesystem refusal must remain an I/O or access exception: " + failure);
    }

    private sealed class PrestigeSaveFileGraph
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> graph = Ranking.Ranks;
        private readonly object gate = Ranking.SynRoot;
        private readonly object cacheGate = Ranking.Ranks.SyncRoot;
        private readonly bool update = Ranking.Ranks.Update;
        private readonly long revision = Ranking.Ranks.Revision;
        private readonly Ranking.Rank[] cache = Ranking.Ranks.MyArray;
        private readonly Ranking.Rank[] cachedMembers = Ranking.Ranks.MyArray.ToArray();
        private readonly (Ranking.Type Key, Ranking.Rank Rank, Ranking.Type Type, int Limit,
            Dictionary<uint, Ranking.Entry> Items, KeyValuePair<uint, Ranking.Entry>[] Rows,
            Ranking.Entry? Champion)[] buckets = Ranking.Ranks.Select(pair =>
                (pair.Key, pair.Value, pair.Value._Type, pair.Value.MaxItems, pair.Value.Items,
                    pair.Value.Items.ToArray(), pair.Value.BestOfClass)).ToArray();
        private readonly (Ranking.Entry Row, object[] Values, uint[]? Points, uint[]? PointValues)[] rows =
            Ranking.Ranks.Values.SelectMany(rank => rank.Items.Values).Distinct()
                .Select(row => (row, Values(row), row.Points, row.Points?.ToArray())).ToArray();

        internal void RequireUnchanged()
        {
            Assert.Same(graph, Ranking.Ranks);
            Assert.Same(gate, Ranking.SynRoot);
            Assert.Same(cacheGate, graph.SyncRoot);
            Assert.Equal(update, graph.Update);
            Assert.Equal(cache, graph.MyArray);
            Assert.Equal(revision, graph.Revision);
            Assert.Equal(cachedMembers.Length, cache.Length);
            for (var index = 0; index < cache.Length; index++) Assert.Same(cachedMembers[index], cache[index]);
            Assert.Equal(buckets.Select(bucket => bucket.Key), graph.Keys);
            foreach (var bucket in buckets)
            {
                Assert.Same(bucket.Rank, graph[bucket.Key]);
                Assert.Equal(bucket.Type, bucket.Rank._Type);
                Assert.Equal(bucket.Limit, bucket.Rank.MaxItems);
                Assert.Same(bucket.Items, bucket.Rank.Items);
                Assert.Same(bucket.Champion, bucket.Rank.BestOfClass);
                Assert.Equal(bucket.Rows.Select(pair => pair.Key), bucket.Rank.Items.Keys);
                foreach (var pair in bucket.Rows) Assert.Same(pair.Value, bucket.Rank.Items[pair.Key]);
            }
            foreach (var row in rows)
            {
                Assert.Equal(row.Values, Values(row.Row));
                Assert.Same(row.Points, row.Row.Points);
                Assert.Equal(row.PointValues, row.Row.Points);
            }
        }
    }

    private sealed class WindowsPrestigeSaveFileFactAttribute : FactAttribute
    {
        public WindowsPrestigeSaveFileFactAttribute()
        {
            if (!OperatingSystem.IsWindows())
                Skip = "This case requires the target Windows deny-delete file sharing contract.";
        }
    }
}
