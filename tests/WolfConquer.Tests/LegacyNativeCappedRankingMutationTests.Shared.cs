using WolfConquer.Core.Data;
using Ninja = WolfConquer.Core.Data.LegacyNativeNinjaRank;
using Weapons = WolfConquer.Core.Data.LegacyNativeHWRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCappedRankingMutationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SharedUpdateCapturesTheGenuineUidExactlyOnceUnderTheSameOwnerGate(bool replacing)
    {
        var rank = new Ninja.Rank(Ninja.Type.Overall) { MaxItems = 3 };
        var item = new Ninja.Entry { UID = 5, TotalPoints = 200 };
        if (replacing) rank.RankingList.Add(5, new() { UID = 5, TotalPoints = 100 });
        int reads = 0;
        uint ReadUid(Ninja.Entry actual)
        {
            Assert.True(Monitor.IsEntered(rank.SynRoot)); Assert.Same(item, actual);
            reads++; return actual.UID;
        }
        LegacyNativeCappedRankingMutations.Update(rank.SynRoot, ref rank.RankingList, item,
            ref rank.MaxItems, ReadUid, static entry => entry.TotalPoints);
        Assert.Equal(1, reads); Assert.Same(item, Assert.Single(rank.RankingList).Value);
        Assert.Equal(5u, Assert.Single(rank.RankingList).Key); Assert.Equal(5u, item.UID);
    }

    [Fact]
    public void ConcurrentNinjaWritersSerializeOnTheOriginalGateWithoutLosingRows()
    {
        var rank = new Ninja.Rank(Ninja.Type.Overall) { MaxItems = 10 };
        var first = new Ninja.Entry { UID = 1, TotalPoints = 10 };
        var second = new Ninja.Entry { UID = 2, TotalPoints = 20 };
        ConcurrentUpdates(() => rank.UpdateItem(first), () => rank.UpdateItem(second));
        Assert.Equal(new uint[] { 2, 1 }, rank.RankingList.Keys);
        Assert.Same(first, rank.RankingList[1]); Assert.Same(second, rank.RankingList[2]);
    }

    [Fact]
    public void ConcurrentWeaponsWritersSerializeOnTheOriginalGateWithoutLosingRows()
    {
        var rank = new Weapons.Rank(Weapons.Type.Main) { MaxItems = 10 };
        var first = new Weapons.Entry { UID = 1, TotalPoints = 10 };
        var second = new Weapons.Entry { UID = 2, TotalPoints = 20 };
        ConcurrentUpdates(() => rank.UpdateItem(first), () => rank.UpdateItem(second));
        Assert.Equal(new uint[] { 2, 1 }, rank.RankingList.Keys);
        Assert.Same(first, rank.RankingList[1]); Assert.Same(second, rank.RankingList[2]);
    }

    private static void ConcurrentUpdates(Action first, Action second)
    {
        var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var one = Task.Run(async () => { await start.Task.WaitAsync(TimeSpan.FromSeconds(5)); first(); });
        var two = Task.Run(async () => { await start.Task.WaitAsync(TimeSpan.FromSeconds(5)); second(); });
        start.SetResult(true);
        Assert.True(Task.WaitAll([one, two], TimeSpan.FromSeconds(10)),
            "Both genuine rank updates must complete within the owned test deadline.");
    }

    private static Dictionary<uint, T> Book<T>(MutationProbe probe, T survivor, T old, bool replacing) where T : class
    {
        var entries = new Dictionary<uint, T>(probe) { [9] = survivor };
        if (replacing) entries.Add(5, old);
        return entries;
    }

    private static void VerifyUpsert<T>(Dictionary<uint, T> original, Func<Dictionary<uint, T>> current,
        Action actualUpdate, MutationProbe probe, T survivor, T old, T added) where T : class
    {
        probe.Keys.Clear(); probe.Armed = true;
        try { actualUpdate(); }
        finally { probe.Armed = false; }
        Assert.Equal(new uint[] { 5 }, probe.Keys);
        Assert.Same(added, original[5]); Assert.Same(survivor, original[9]);
        Assert.NotSame(original, current()); Assert.Equal(new uint[] { 5, 9 }, current().Keys);
        Assert.Same(added, current()[5]); Assert.Same(survivor, current()[9]); Assert.NotSame(old, current()[5]);
    }

    private static void AssertTrim<T>(Dictionary<uint, T> actual, T[] rows, int cap) where T : class
    {
        uint[] keys = [9, 7, 8, 6];
        Assert.Equal(keys.Take(cap), actual.Keys);
        for (int index = 0; index < cap; index++) Assert.Same(rows[index], actual[keys[index]]);
    }

    private sealed class MutationProbe(object gate) : IEqualityComparer<uint>
    {
        internal bool Armed;
        internal List<uint> Keys { get; } = [];
        internal Action<uint>? OnHash;
        public bool Equals(uint first, uint second) => first == second;
        public int GetHashCode(uint key)
        {
            if (Armed)
            {
                Assert.True(Monitor.IsEntered(gate)); Keys.Add(key); OnHash?.Invoke(key);
            }
            return key.GetHashCode();
        }
    }
}
