using Inner = WolfConquer.Core.World.LegacyNativeInnerPowerRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInnerPowerTests
{
    [Fact]
    public void RankingStartsNullThenFiltersZeroScoresAndReturnsFreshArrayOfLiveReferences()
    {
        using var scope = new Scope();
        Assert.Throws<ArgumentNullException>(() => Inner.InnerPowerRank.GetRankingList());
        var lower = WithScore(1, 3); var higher = WithScore(2, 9); var zero = WithScore(3, 0);
        Inner.InnerPowerRank.UpdateRank(lower); Inner.InnerPowerRank.UpdateRank(higher); Inner.InnerPowerRank.UpdateRank(zero);
        var first = Inner.InnerPowerRank.GetRankingList(); var second = Inner.InnerPowerRank.GetRankingList();
        Assert.Equal(new[] { higher, lower }, first); Assert.NotSame(first, second); Assert.Same(higher, second[0]);
        first[0] = zero; Assert.Same(higher, Inner.InnerPowerRank.GetRankingList()[0]);
        lower.Stages[0].NeiGongs[0].Score = 20;
        Assert.Same(higher, Inner.InnerPowerRank.GetRankingList()[0]);
        Inner.InnerPowerRank.CreateRanks(); Assert.Same(lower, Inner.InnerPowerRank.GetRankingList()[0]);
    }

    [Fact]
    public void BetterHundredAndFirstEntrantIsAddedWithoutRemovingLowestAndEqualEntrantIsRejected()
    {
        using var scope = new Scope();
        for (uint uid = 1; uid <= 100; uid++) Inner.InnerPowerRank.UpdateRank(WithScore(uid, (byte)uid));
        var rejected = WithScore(101, 1); Inner.InnerPowerRank.UpdateRank(rejected);
        Assert.Equal(100, Inner.InnerPowerRank.GetRankingList().Length);
        var added = WithScore(102, 200); Inner.InnerPowerRank.UpdateRank(added);
        var ranks = Inner.InnerPowerRank.GetRankingList();
        Assert.Equal(101, ranks.Length); Assert.Same(added, ranks[0]);
        Assert.Contains(ranks, value => value.UID == 1); Assert.DoesNotContain(rejected, ranks);
    }

    [Fact]
    public void HundredZeroEntriesLeaveEmptyRankingsAndNextUpdateFaultsOnLast()
    {
        using var scope = new Scope();
        for (uint uid = 1; uid <= 100; uid++) Inner.InnerPowerRank.UpdateRank(WithScore(uid, 0));
        Assert.Empty(Inner.InnerPowerRank.GetRankingList());
        Assert.Throws<InvalidOperationException>(() => Inner.InnerPowerRank.UpdateRank(WithScore(101, 2)));
        Assert.Empty(Inner.InnerPowerRank.GetRankingList());
    }

    [Fact]
    public void DuplicateUidDoesNotReplaceRankingObjectAndExistingEntryCanReorderAtCapacity()
    {
        using var scope = new Scope(); var first = WithScore(1, 10);
        Inner.InnerPowerRank.UpdateRank(first);
        var duplicate = WithScore(1, 250); Inner.InnerPowerRank.UpdateRank(duplicate);
        Assert.Same(first, Assert.Single(Inner.InnerPowerRank.GetRankingList()));
        for (uint uid = 2; uid <= 100; uid++) Inner.InnerPowerRank.UpdateRank(WithScore(uid, 20));
        first.Stages[0].NeiGongs[0].Score = 255;
        Inner.InnerPowerRank.UpdateRank(first);
        Assert.Same(first, Inner.InnerPowerRank.GetRankingList()[0]);
    }
}
