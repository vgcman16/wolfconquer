using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativeRuneRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLoginCatalogTests
{
    [Fact]
    public void RuneRankWholeLoadReplacesPriorCacheAndKeepsThresholdSortAndLiteralRowFields()
    {
        using var scope = new Scope();
        Ranking.RankingList!.Add(999, Row(uint.MaxValue));
        scope.Rows("RuneRanking.txt", "Low/9999/1/", "First/10000/7/", "Max/4294967295/8/", "Tie/10000/9/");
        Ranking.Load();
        Assert.Equal(new uint[] { 8, 7, 9 }, Ranking.RankingList!.Keys);
        Assert.Equal("First", Ranking.RankingList![7].Name);
        Assert.Equal(uint.MaxValue, Ranking.RankingList![8].TotalPoints);
        Assert.False(Ranking.RankingList!.ContainsKey(999));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RuneRankMissingOrZeroCountFilePublishesEmptyDictionary(bool emptyFile)
    {
        using var scope = new Scope();
        if (emptyFile) scope.Rows("RuneRanking.txt");
        Ranking.RankingList!.Add(1, Row(10000));
        Ranking.Load();
        Assert.NotNull(Ranking.RankingList);
        Assert.Empty(Ranking.RankingList);
    }

    [Fact]
    public void RuneRankDuplicateFileKeyRetainsUnfilteredParsePrefixAtOriginalFault()
    {
        using var scope = new Scope();
        scope.Rows("RuneRanking.txt", "Low/1/4/", "High/20000/2/", "Duplicate/30000/4/", "Never/40000/9/");
        Assert.Throws<ArgumentException>(() => Ranking.Load());
        Assert.Equal(new uint[] { 4, 2 }, Ranking.RankingList!.Keys);
        Assert.Equal(1u, Ranking.RankingList![4].TotalPoints);
        Assert.Equal("Low", Ranking.RankingList![4].Name);
        Assert.False(Ranking.RankingList!.ContainsKey(9));
    }

    [Fact]
    public void RuneRankNativeInvalidFieldDefaultsStillParticipateInDuplicateFaultPrefix()
    {
        using var scope = new Scope();
        scope.Rows("RuneRanking.txt", "not-delimited", "Bad/not-number/not-uid/", "Never/20000/7/");
        Assert.Throws<ArgumentException>(() => Ranking.Load());
        Assert.Single(Ranking.RankingList!);
        Assert.Equal("", Ranking.RankingList![0].Name);
        Assert.Equal(0u, Ranking.RankingList![0].TotalPoints);
    }

    [Fact]
    public void RuneRankSaveRetainsDelimiterAndNoEscapingRatherThanRepairingNativeFormat()
    {
        using var scope = new Scope();
        string path = scope.Target("RuneRanking.txt");
        Ranking.RankingList!.Add(17, Row(15000, "A/B"));
        Ranking.Save();
        Assert.Equal(new[] { "Count=1", "A/B/15000/17/" }, File.ReadAllLines(path));
        Ranking.Load();
        Assert.Empty(Ranking.RankingList!); // B parses as0, so the original threshold drops it.
    }
}
