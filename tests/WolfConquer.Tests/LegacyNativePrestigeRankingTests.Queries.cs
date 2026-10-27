using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    public static IEnumerable<object[]> ClassBands()
    {
        foreach (var band in new (uint Min,uint Max,Ranking.Type Type)[]
        {
            (1000,1051,Ranking.Type.Trojan),(2000,2051,Ranking.Type.Warrior),
            (4000,4051,Ranking.Type.Archer),(5000,5051,Ranking.Type.Ninja),
            (6000,6051,Ranking.Type.Monk),(7000,7051,Ranking.Type.Pirate),
            (8000,8051,Ranking.Type.DragonWarrior),(9000,9051,Ranking.Type.Thunderstriker),
            (13002,13051,Ranking.Type.WaterTao),(14002,14051,Ranking.Type.FireTao),
            (16000,16051,Ranking.Type.WindWalker),
        })
        {
            yield return [band.Min, (uint)band.Type];
            yield return [band.Max, (uint)band.Type];
            yield return [band.Min-1, (uint)Ranking.Type.WaterTao];
            yield return [band.Max+1, (uint)Ranking.Type.WaterTao];
        }
        yield return [0u,(uint)Ranking.Type.WaterTao];
        yield return [uint.MaxValue,(uint)Ranking.Type.WaterTao];
    }

    [Theory]
    [MemberData(nameof(ClassBands))]
    public void GetIndexRetainsAllElevenInclusiveJobRangesAndUnknownWaterFallback(uint job, uint expected) =>
        Assert.Equal((Ranking.Type)expected, Ranking.GetIndex(job));

    [Fact]
    public void QueriesUsePhysicalUidAndInsertionOrderWithoutResortingOrUsingBestOfCache()
    {
        using var state = new State();
        Ranking.Create();
        var rank = Ranking.Ranks[Ranking.Type.Ninja];
        var first = Entry(17,20000);
        var second = Entry(19,uint.MaxValue);
        rank.Items.Add(700,first);
        rank.Items.Add(701,second);
        rank.BestOfClass = second;
        Assert.Same(first, Ranking.GetInfo(17));
        Assert.Equal(2u, Ranking.GetMyRank(Ranking.Type.Ninja,19));
        Assert.Null(Ranking.GetInfo(700));
        Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.Ninja,700));
        Assert.Equal(0u, Ranking.GetMyRank(Ranking.Type.Count,19));
        first.UID = 23;
        Assert.Null(Ranking.GetInfo(17));
        Assert.Same(first, Ranking.GetInfo(23));
        Assert.Equal(1u, Ranking.GetMyRank(Ranking.Type.Ninja,23));
        Assert.Same(second, rank.BestOfClass);
        Assert.Null(Ranking.BestOfTheWorld); // Present World bucket, no diagnostic branch.
        Ranking.Ranks[Ranking.Type.World].BestOfClass = first;
        Assert.Same(first, Ranking.BestOfTheWorld);
    }

    [Fact]
    public void GlobalRemoveUsesActualRankValuesRefreshesChampionAndPreservesTheEscapedCacheAndItems()
    {
        using var state = new State();
        Ranking.Create();
        var old = Ranking.Ranks[Ranking.Type.Ninja];
        old.AddItem(Entry());
        var cached = Ranking.Ranks.GetValues();
        var replacement = new Ranking.Rank(Ranking.Type.Ninja) { MaxItems = 30 };
        var live = Entry();
        replacement.AddItem(live);
        var escaped = replacement.Items;
        Ranking.Ranks[Ranking.Type.Ninja] = replacement;
        Assert.Same(cached, Ranking.Ranks.GetValues());
        Ranking.Remove(41);
        Assert.Empty(replacement.Items);
        Assert.Single(old.Items);
        Assert.Null(replacement.BestOfClass);
        Assert.Same(live, Assert.Single(escaped).Value);
        Assert.Same(cached,Ranking.Ranks.GetValues());
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)3)]
    public void CheckRebornRemovesOnlyPriorClassAndNeverWorldOrCurrentBestOf(byte reborn)
    {
        using var state = new State();
        Ranking.Create();
        var user = new LegacyNativeGameClientRuntime();
        user.Player.UID = 41;
        user.Player.FirstClass = 1000;
        user.Player.SecoundeClass = 2000;
        user.Player.Reborn = reborn;
        var same = Entry();
        foreach (var rank in Ranking.Ranks.Values) rank.AddItem(same);
        Ranking.CheckReborn(user);
        foreach (var rank in Ranking.Ranks.Values)
        {
            bool removed = (reborn == 1 && rank._Type == Ranking.Type.Trojan) ||
                (reborn == 2 && rank._Type == Ranking.Type.Warrior);
            Assert.Equal(!removed,rank.Items.ContainsKey(41));
            Assert.Same(same,rank.BestOfClass);
        }
    }

    [Fact]
    public void CheckRebornSortFaultRetainsRemovalAndUnchangedDictionaryAndCache()
    {
        using var state = new State();
        Ranking.Create();
        var user = new LegacyNativeGameClientRuntime();
        user.Player.UID = 41;
        user.Player.FirstClass = 1000;
        user.Player.Reborn = 1;
        var rank = Ranking.Ranks[Ranking.Type.Trojan];
        var best = Entry();
        rank.AddItem(best);
        rank.Items.Add(77,null!);
        var escaped = rank.Items;
        Assert.Throws<NullReferenceException>(() => Ranking.CheckReborn(user));
        Assert.Same(escaped,rank.Items);
        Assert.False(escaped.ContainsKey(41));
        Assert.Same(best,rank.BestOfClass);
    }
}
