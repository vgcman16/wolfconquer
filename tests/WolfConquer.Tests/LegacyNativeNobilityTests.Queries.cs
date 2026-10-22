using Ranking = WolfConquer.Core.World.LegacyNativeNobility.NobilityRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNobilityTests
{
    [Fact]
    public void RankingConstructorOwnsAnEmptyMutableDictionaryAndNativeThresholdDefaults()
    {
        var ranking = new Ranking();
        var other = new Ranking();
        Assert.Empty(ranking.ClientPoll); Assert.NotSame(ranking.ClientPoll, other.ClientPoll);
        Assert.Equal(30_000_000UL, ranking.KnightDonation);
        Assert.Equal(100_000_000UL, ranking.BaronDonation);
        Assert.Equal(200_000_000UL, ranking.EarlDonation);
        Assert.Equal(0UL, ranking.DukeDonation); Assert.Equal(0UL, ranking.PrinceDonation);
        Assert.Equal(0UL, ranking.KingDonation);
        Assert.False(ranking.TryGetValue(17, out var missing)); Assert.Null(missing);
        Assert.Empty(ranking.GetArray());
    }

    [Fact]
    public void QueriesUsePhysicalDictionaryKeysAndSnapshotMembershipButKeepActualRowAliasesLive()
    {
        var ranking = new Ranking(); var row = Row();
        ranking.ClientPoll.Add(900, row); ranking.ClientPoll.Add(901, row);
        Assert.True(ranking.TryGetValue(900, out var found)); Assert.Same(row, found);
        Assert.False(ranking.TryGetValue(row.UID, out var missing)); Assert.Null(missing);
        var snapshot = ranking.GetArray();
        Assert.Equal(2, snapshot.Length); Assert.Same(row, snapshot[0]); Assert.Same(row, snapshot[1]);
        row.Donation = 12; ranking.ClientPoll.Clear(); ranking.ClientPoll.Add(1000, Row(55));
        Assert.Equal(2, snapshot.Length); Assert.Equal(12UL, snapshot[0].Donation);
        Assert.Same(row, snapshot[1]); Assert.Equal(55u, Assert.Single(ranking.GetArray()).UID);
    }

    [Fact]
    public void NullRowsAreReturnedRatherThanFilteredAndNullDictionaryFaultsAreNotRepaired()
    {
        var ranking = new Ranking(); ranking.ClientPoll.Add(17, null!);
        Assert.True(ranking.TryGetValue(17, out var found)); Assert.Null(found);
        Assert.Null(Assert.Single(ranking.GetArray()));
        ranking.ClientPoll = null!;
        Assert.Throws<NullReferenceException>(() => ranking.TryGetValue(17, out _));
        Assert.Throws<NullReferenceException>(() => ranking.GetArray());
        Assert.Null(ranking.ClientPoll);
    }
}
