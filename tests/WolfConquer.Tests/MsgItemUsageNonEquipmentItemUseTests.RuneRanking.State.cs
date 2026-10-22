using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    [Fact]
    public void RuneRankingSeedNormalizesThresholdDescendingAndTopHundred()
    {
        var seed = Enumerable.Range(0, 101)
            .Select(index => RuneRankSeed(7_100_000 + index, 10_000 + index))
            .Prepend(RuneRankSeed(7_200_000, -1))
            .Prepend(RuneRankSeed(RuneRankPlayerA, 9_999))
            .ToArray();
        var state = new LegacyRuneRankingState(seed);

        var rows = state.Snapshot();
        Assert.Equal(100, rows.Count);
        Assert.Equal(Enumerable.Range(0, 100).Select(index => 7_100_100L - index),
            rows.Select(entry => entry.CharacterUid));
        Assert.Equal(10_100U, rows[0].Score);
        Assert.Equal(10_001U, rows[^1].Score);
        Assert.Equal<int?>(1, state.FindRank(7_100_100));
        Assert.Equal<int?>(100, state.FindRank(7_100_001));
        Assert.Null(state.FindRank(7_100_000));
        Assert.Null(state.FindRank(RuneRankPlayerA));
        Assert.Null(state.FindRank(7_200_000));
        Assert.Equal(103, seed.Length);
        Assert.Equal(9_999, seed[0].Score);
        seed[0] = RuneRankSeed(RuneRankPlayerA, 99_999);
        Assert.Null(state.FindRank(RuneRankPlayerA));
        Assert.Empty(new LegacyRuneRankingState().Snapshot());
        Assert.Throws<ArgumentException>(() => new LegacyRuneRankingState(
            [RuneRankSeed(RuneRankPlayerB, 10_000), RuneRankSeed(RuneRankPlayerB, 10_300)]));
    }

    [Fact]
    public void RuneRankingUpdateReordersLiveScoreAndNameWithoutMutatingOldSnapshot()
    {
        var state = new LegacyRuneRankingState(
            [RuneRankSeed(RuneRankPlayerB, 10_000), RuneRankSeed(RuneRankPlayerA, 9_700)]);
        var original = state.Snapshot();

        state.Update(RuneRankCharacter(RuneRankPlayerA, "Renamed"), RuneRankItems(16));

        var reordered = state.Snapshot();
        Assert.Equal(new LegacyRuneRankingEntry(RuneRankPlayerA, "Renamed", 10_300), reordered[0]);
        Assert.Equal(new LegacyRuneRankingEntry(RuneRankPlayerB, "Demonic", 10_000), reordered[1]);
        Assert.Equal<int?>(1, state.FindRank(RuneRankPlayerA));
        Assert.Equal<int?>(2, state.FindRank(RuneRankPlayerB));
        Assert.Equal(RuneRankPlayerB, Assert.Single(original).CharacterUid);

        state.Update(RuneRankCharacter(RuneRankPlayerA, "BelowThreshold"), RuneRankItems(14));

        Assert.Null(state.FindRank(RuneRankPlayerA));
        Assert.Equal<int?>(1, state.FindRank(RuneRankPlayerB));
        Assert.Equal(2, reordered.Count);
        Assert.Equal("Renamed", reordered[0].CharacterName);
        Assert.Equal(10_300U, reordered[0].Score);
        Assert.Equal(RuneRankPlayerB, Assert.Single(state.Snapshot()).CharacterUid);
    }

    [Fact]
    public void RuneRankingTiesRetainCurrentOrderingAndAppendNewEqualScore()
    {
        const long thirdUid = 1_293_602;
        var state = new LegacyRuneRankingState(
            [RuneRankSeed(RuneRankPlayerA, 10_300), RuneRankSeed(RuneRankPlayerB, 10_300)]);

        state.Update(RuneRankCharacter(RuneRankPlayerB, "Second"), RuneRankItems(16));
        state.Update(RuneRankCharacter(thirdUid, "Third"), RuneRankItems(16));
        Assert.Equal(new[] { RuneRankPlayerA, RuneRankPlayerB, thirdUid },
            state.Snapshot().Select(entry => entry.CharacterUid));

        state.Update(RuneRankCharacter(RuneRankPlayerB, "Second"), RuneRankItems(17));
        state.Update(RuneRankCharacter(RuneRankPlayerA, "First"), RuneRankItems(17));
        Assert.Equal(new[] { RuneRankPlayerB, RuneRankPlayerA, thirdUid },
            state.Snapshot().Select(entry => entry.CharacterUid));
    }

    [Fact]
    public void RuneRankingEvictionDropsHistoryUntilAnExplicitNewUpdate()
    {
        var state = new LegacyRuneRankingState(RuneRankFullSeed());
        Assert.Equal<int?>(100, state.FindRank(RuneRankPlayerB));

        state.Update(RuneRankCharacter(RuneRankPlayerA, "Newcomer"), RuneRankItems(16));
        Assert.Equal(100, state.Snapshot().Count);
        Assert.Equal<int?>(100, state.FindRank(RuneRankPlayerA));
        Assert.Null(state.FindRank(RuneRankPlayerB));

        state.Update(RuneRankCharacter(RuneRankPlayerA, "BelowThreshold"), RuneRankItems(14));
        Assert.Equal(99, state.Snapshot().Count);
        Assert.Null(state.FindRank(RuneRankPlayerA));
        Assert.Null(state.FindRank(RuneRankPlayerB));

        state.Update(RuneRankCharacter(RuneRankPlayerB, "Returned"), RuneRankItems(15));
        Assert.Equal(100, state.Snapshot().Count);
        Assert.Equal<int?>(100, state.FindRank(RuneRankPlayerB));
        Assert.Equal("Returned", state.Snapshot()[^1].CharacterName);
    }

}
