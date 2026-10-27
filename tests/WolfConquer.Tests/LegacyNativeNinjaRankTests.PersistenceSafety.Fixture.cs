using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    private static void WithOwnedRankPersistence(Action<OwnedRankFile> body)
    {
        var state = new State();
        OwnedRankFile? files = null;
        var failures = new List<Exception>();
        try { files = new(); body(files); }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            try { files?.Dispose(); }
            catch (Exception error) { failures.Add(error); }
            try { state.Dispose(); }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count == 1)
        {
            if (files is not null) failures[0].Data["OwnedNinjaRankRoot"] = files.Root;
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1)
            throw new AggregateException($"Ninja rank operation/cleanup failures; owned root {files?.Root}.", failures);
    }

    private static PersistenceGraphImage SeedPersistenceGraph()
    {
        Ranking.Create();
        for (uint index = 0; index < 6; index++)
        {
            var row = Entry(500 + index, 2000 + index);
            row.Type = (Ranking.Type)index;
            Ranking.Ranks[row.Type].RankingList.Add(row.UID, row);
        }
        _ = Ranking.Ranks.GetValues(); // Retain the genuine populated cache too.
        return new();
    }

    private static void RequireRejectedRankLoad(OwnedRankFile files)
    {
        var before = SeedPersistenceGraph();
        var bytes = File.ReadAllBytes(files.PathFor());
        var error = Record.Exception(Ranking.Load);
        before.RequireCurrent();
        Assert.Equal(bytes, File.ReadAllBytes(files.PathFor()));
        Assert.IsType<InvalidDataException>(error);
    }

    private static void RequireFreshSixRanks(PersistenceGraphImage previous)
    {
        Assert.NotSame(previous.Registry, Ranking.Ranks);
        previous.RequireEscaped();
        Assert.Equal(Enumerable.Range(0, 6).Select(value => (Ranking.Type)value), Ranking.Ranks.Keys);
        Assert.Equal(6, Ranking.Ranks.Count);
        var owners = Ranking.Ranks.Values.ToArray();
        Assert.Equal(6, owners.Distinct(ReferenceEqualityComparer.Instance).Count());
        for (int index = 0; index < 6; index++)
        {
            var rank = Ranking.Ranks[(Ranking.Type)index];
            Assert.Equal((Ranking.Type)index, rank._Type);
            Assert.Equal(10, rank.MaxItems);
            Assert.NotNull(rank.SynRoot);
            Assert.NotNull(rank.RankingList);
        }
        Assert.Equal(owners, Ranking.Ranks.GetValues());
    }

    private sealed class PersistenceGraphImage
    {
        internal LegacyNativeDbSafeDictionary<Ranking.Type, Ranking.Rank>? Registry { get; } = Ranking.Ranks;
        private readonly object? gate;
        private readonly Ranking.Rank[]? cache;
        private readonly Ranking.Rank[] cacheRows;
        private readonly bool dirty;
        private readonly KeyValuePair<Ranking.Type, Ranking.Rank>[] owners;
        private readonly Dictionary<Ranking.Rank, PersistenceRankImage> ranks = new(ReferenceEqualityComparer.Instance);

        internal PersistenceGraphImage()
        {
            gate = Registry?.SyncRoot;
            cache = Registry?.MyArray;
            cacheRows = cache?.ToArray() ?? [];
            dirty = Registry?.Update ?? false;
            owners = Registry?.ToArray() ?? [];
            foreach (var owner in owners.Select(pair => pair.Value).Where(owner => owner is not null))
                ranks.TryAdd(owner, new(owner));
        }

        internal void RequireCurrent()
        {
            Assert.Same(Registry, Ranking.Ranks);
            RequireEscaped();
        }

        internal void RequireEscaped()
        {
            if (Registry is null) return;
            Assert.Same(gate, Registry.SyncRoot);
            Assert.Same(cache, Registry.MyArray);
            Assert.Equal(cacheRows, Registry.MyArray ?? []);
            Assert.Equal(dirty, Registry.Update);
            var current = Registry.ToArray();
            Assert.Equal(owners.Select(pair => pair.Key), current.Select(pair => pair.Key));
            for (int index = 0; index < owners.Length; index++) Assert.Same(owners[index].Value, current[index].Value);
            foreach (var rank in ranks.Values) rank.RequireUnchanged();
        }
    }

    private sealed class PersistenceRankImage
    {
        private readonly Ranking.Rank owner;
        private readonly Ranking.Type type;
        private readonly int limit;
        private readonly object? gate;
        private readonly Dictionary<uint, Ranking.Entry>? map;
        private readonly KeyValuePair<uint, Ranking.Entry>[] rows;
        private readonly Dictionary<Ranking.Entry, object[]> values = new(ReferenceEqualityComparer.Instance);

        internal PersistenceRankImage(Ranking.Rank owner)
        {
            this.owner = owner; type = owner._Type; limit = owner.MaxItems; gate = owner.SynRoot;
            map = owner.RankingList; rows = map?.ToArray() ?? [];
            foreach (var row in rows.Select(pair => pair.Value).Where(row => row is not null))
                values.TryAdd(row, Values(row));
        }

        internal void RequireUnchanged()
        {
            Assert.Equal(type, owner._Type); Assert.Equal(limit, owner.MaxItems); Assert.Same(gate, owner.SynRoot);
            Assert.Same(map, owner.RankingList);
            var current = map?.ToArray() ?? [];
            Assert.Equal(rows.Select(pair => pair.Key), current.Select(pair => pair.Key));
            for (int index = 0; index < rows.Length; index++) Assert.Same(rows[index].Value, current[index].Value);
            foreach (var row in values) Assert.Equal(row.Value, Values(row.Key));
        }
    }
}
