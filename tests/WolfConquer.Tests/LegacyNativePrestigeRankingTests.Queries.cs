using WolfConquer.Core.World;
using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    public static IEnumerable<object[]> ClassBands() => PrestigeTargetClassFixture.FamilyEdges();

    public static IEnumerable<object[]> NonPrestigeClasses() => PrestigeTargetClassFixture.StrictRefusals();

    [Theory]
    [MemberData(nameof(ClassBands))]
    public void GetIndexResolvesKnownD13FamilyEdgesThroughSuffix57(uint job, uint expected) =>
        Assert.Equal((Ranking.Type)expected, Ranking.GetIndex(job));

    [Theory]
    [MemberData(nameof(NonPrestigeClasses))]
    public void GetIndexRefusesUnsupportedAndWorldOnlyInputsWithoutDefaultWaterAuthority(uint job) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Ranking.GetIndex(job));

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
    public void CheckRebornRemovesOnlyPriorClassAndRefreshesItsChampionWithoutChangingOtherOwners(byte reborn)
    {
        using var state = new State();
        Ranking.Create();
        var user = new LegacyNativeGameClientRuntime();
        user.Player.UID = 41;
        user.Player.FirstClass = 1000;
        user.Player.SecoundeClass = 2000;
        user.Player.Reborn = reborn;
        SetRebornCurrentClass(user, 5000);
        var same = Entry();
        foreach (var rank in Ranking.Ranks.Values) rank.AddItem(same);
        var escaped = Ranking.Ranks.ToDictionary(pair => pair.Key, pair => pair.Value.Items);
        var before = new PrestigeRebornGraphImage();
        Ranking.CheckReborn(user);
        foreach (var rank in Ranking.Ranks.Values)
        {
            bool removed = (reborn == 1 && rank._Type == Ranking.Type.Trojan) ||
                (reborn == 2 && rank._Type == Ranking.Type.Warrior);
            Assert.Equal(!removed,rank.Items.ContainsKey(41));
            Assert.Same(removed ? null : same,rank.BestOfClass);
            Assert.Same(same,escaped[rank._Type][41]);
            if (removed) Assert.NotSame(escaped[rank._Type],rank.Items);
            else Assert.Same(escaped[rank._Type],rank.Items);
        }
        if (reborn is 1 or 2)
            before.RequireOnlySelectedChanged(Ranking.Ranks[reborn == 1 ? Ranking.Type.Trojan : Ranking.Type.Warrior]);
        else before.RequireUnchanged();
    }

    [Fact]
    public void CheckRebornMalformedSelectedTailRefusesWithoutDeletionOrChampionMutation()
    {
        using var state = new State();
        Ranking.Create();
        var user = new LegacyNativeGameClientRuntime();
        user.Player.UID = 41;
        user.Player.FirstClass = 1000;
        user.Player.Reborn = 1;
        SetRebornCurrentClass(user, 5000);
        var rank = Ranking.Ranks[Ranking.Type.Trojan];
        var best = Entry();
        rank.AddItem(best);
        rank.Items.Add(77,null!);
        var escaped = rank.Items;
        var before = new PrestigeRebornGraphImage();
        var error = Record.Exception(() => Ranking.CheckReborn(user));
        before.RequireUnchanged();
        Assert.Same(escaped,rank.Items);
        Assert.Same(best,escaped[41]);
        Assert.Same(best,rank.BestOfClass);
        Assert.IsType<InvalidOperationException>(error);
    }
}
