using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    // Reuse the serialized collection's real owned files and global restore.
    // Each cleanup is attempted; a cleanup fault cannot hide the primary failure.
    private static void RunOwnedPrestigeLoad(Action<OwnedRankFile> body)
    {
        State? state = null;
        OwnedRankFile? files = null;
        var errors = new List<Exception>();
        try { state = new(); files = new(); body(files); }
        catch (Exception error) { errors.Add(error); }
        if (files is not null)
            try { files.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (state is not null)
            try { state.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Prestige load action and owned cleanup failed.", errors);
    }

    private static void SeedPriorPrestigeGraph()
    {
        Ranking.Create();
        var champion = Entry(7001, 75_000);
        var warrior = Entry(7002, 65_000);
        warrior.type = Ranking.Type.Warrior;
        Ranking.Ranks[Ranking.Type.Ninja].AddItem(champion);
        Ranking.Ranks[Ranking.Type.Warrior].AddItem(warrior);
        Ranking.Ranks[Ranking.Type.World].AddItem(champion);
        Ranking.Ranks[Ranking.Type.World].AddItem(warrior);
        var extra = Place(Entry(7999, 55_000), (Ranking.Type)99);
        extra.BestOfClass = extra.Items[7999];
        _ = Ranking.Ranks.GetValues();
    }

    private static void RequireRejectedPrestigeText(OwnedRankFile files, string text, bool dirtyCache = false)
    {
        SeedPriorPrestigeGraph();
        if (dirtyCache) Ranking.Ranks[Ranking.Type.World] = Ranking.Ranks[Ranking.Type.World];
        var prior = new PublishedPrestigeGraph();
        files.Seed(text);
        var bytes = File.ReadAllBytes(files.PathFor());
        Assert.Throws<FormatException>(() => Ranking.Load());
        prior.RequireUnchanged();
        Assert.Equal(bytes, File.ReadAllBytes(files.PathFor()));
    }

    private static string CompletePrestigeLoadRow(uint uid = 41, uint total = 30_004,
        uint category = 4, string name = "Hero", string guild = "Guild", uint[]? points = null)
    {
        var fields = FullRow.Split('/').Take(21).ToList();
        fields[0] = uid.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[2] = category.ToString(CultureInfo.InvariantCulture);
        fields[6] = total.ToString(CultureInfo.InvariantCulture);
        fields[20] = guild;
        points ??= [201, 202];
        fields.Add(points.Length.ToString(CultureInfo.InvariantCulture));
        fields.AddRange(points.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        fields.Add("");
        return string.Join('/', fields);
    }

    private static string ChangePrestigeLoadField(string row, int index, string value)
    {
        var fields = row.Split('/');
        fields[index] = value;
        return string.Join('/', fields);
    }

    // Capture both published graph and the genuine SafeDictionary cache. The
    // witness also checks escaped owners after a successful whole-graph swap.
    private sealed class PublishedPrestigeGraph
    {
        private readonly LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank> ranks = Ranking.Ranks;
        private readonly object gate = Ranking.SynRoot;
        private readonly object dictionaryGate = Ranking.Ranks.SyncRoot;
        private readonly Ranking.Rank[] cache = Ranking.Ranks.MyArray;
        private readonly Ranking.Rank[] cachedMembers = Ranking.Ranks.MyArray.ToArray();
        private readonly bool dirtyCache = Ranking.Ranks.Update;
        private readonly long revision = Ranking.Ranks.Revision;
        private readonly KeyValuePair<Ranking.Type, Ranking.Rank>[] pairs = Ranking.Ranks.ToArray();
        private readonly LoadBucketImage[] buckets = Ranking.Ranks.Values.Select(rank => new LoadBucketImage(rank)).ToArray();

        internal void RequireUnchanged()
        {
            Assert.Same(ranks, Ranking.Ranks);
            RequireEscapedOwnersUnchanged();
        }

        internal void RequireEscapedOwnersUnchanged()
        {
            Assert.Same(gate, Ranking.SynRoot);
            Assert.Same(dictionaryGate, ranks.SyncRoot);
            Assert.Equal(pairs.Select(pair => pair.Key), ranks.Keys);
            foreach (var pair in pairs) Assert.Same(pair.Value, ranks[pair.Key]);
            Assert.Equal(dirtyCache, ranks.Update);
            Assert.Equal(cache, ranks.MyArray);
            Assert.Equal(revision, ranks.Revision);
            Assert.Equal(cachedMembers.Length, cache.Length);
            for (var index = 0; index < cache.Length; index++) Assert.Same(cachedMembers[index], cache[index]);
            if (!dirtyCache) Assert.Equal(cache, ranks.GetValues());
            foreach (var bucket in buckets) bucket.RequireUnchanged();
        }
    }

    private sealed class LoadBucketImage(Ranking.Rank owner)
    {
        private readonly Dictionary<uint, Ranking.Entry> items = owner.Items;
        private readonly KeyValuePair<uint, Ranking.Entry>[] pairs = owner.Items.ToArray();
        private readonly Ranking.Entry? champion = owner.BestOfClass;
        private readonly Ranking.Type type = owner._Type;
        private readonly int limit = owner.MaxItems;
        private readonly LoadEntryImage[] entries = owner.Items.Values.Select(entry => new LoadEntryImage(entry)).ToArray();

        internal void RequireUnchanged()
        {
            Assert.Same(items, owner.Items);
            Assert.Equal(pairs.Select(pair => pair.Key), owner.Items.Keys);
            foreach (var pair in pairs) Assert.Same(pair.Value, owner.Items[pair.Key]);
            Assert.Same(champion, owner.BestOfClass);
            Assert.Equal(type, owner._Type);
            Assert.Equal(limit, owner.MaxItems);
            foreach (var entry in entries) entry.RequireUnchanged();
        }
    }

    private sealed class LoadEntryImage(Ranking.Entry owner)
    {
        private readonly object[] values = Values(owner);
        private readonly uint[]? points = owner.Points;
        private readonly uint[]? pointValues = owner.Points?.ToArray();

        internal void RequireUnchanged()
        {
            Assert.Equal(values, Values(owner));
            Assert.Same(points, owner.Points);
            Assert.Equal(pointValues, owner.Points);
        }
    }
}
