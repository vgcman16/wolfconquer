using WolfConquer.Core.Data;
using Ranking = WolfConquer.Core.Data.LegacyNativeRuneRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLoginCatalogTests
{
    [Fact]
    public void RuneRankWholeUpdateMutatesExistingAliasThenPublishesThresholdFilteredDictionary()
    {
        using var scope = new Scope();
        var client = scope.Client(runeCount: 19);
        var retained = Row(50000, "Before");
        var previous = Ranking.RankingList!;
        previous.Add(17, retained);
        previous.Add(18, Row(12000));
        Ranking.Update(client);
        Assert.Equal(9500u, retained.TotalPoints);
        Assert.Equal("Hero", retained.Name);
        Assert.Same(retained, previous[17]);
        Assert.NotSame(previous, Ranking.RankingList);
        Assert.Equal(new uint[] { 18 }, Ranking.RankingList!.Keys);
    }

    [Fact]
    public void RuneRankWholeUpdateAddsThresholdEntryAndRetainsSharedEntryAliases()
    {
        using var scope = new Scope();
        var client = scope.Client(runeCount: 22, name: "Shared");
        var entry = Row(10001, "Before");
        Ranking.RankingList!.Add(99, entry);
        Ranking.RankingList!.Add(17, entry);
        Ranking.Update(client);
        Assert.Equal(new uint[] { 99, 17 }, Ranking.RankingList!.Keys);
        Assert.Same(entry, Ranking.RankingList![99]);
        Assert.Same(entry, Ranking.RankingList![17]);
        Assert.Equal(11000u, entry.TotalPoints);
        Assert.Equal("Shared", entry.Name);
        client.Player.UID = 42;
        Ranking.Update(client);
        Assert.Equal(new uint[] { 99, 17, 42 }, Ranking.RankingList!.Keys);
        Assert.NotSame(entry, Ranking.RankingList![42]);
        Assert.Equal(11000u, Ranking.RankingList![42].TotalPoints);
    }

    [Theory]
    [InlineData(1)] [InlineData(100)] [InlineData(101)] [InlineData(105)]
    public void RuneRankSaveKeepsStableEqualScoreBoundaryAndOriginalEntryReferences(int count)
    {
        using var scope = new Scope();
        string path = scope.Target("RuneRanking.txt");
        var previous = Ranking.RankingList!;
        for (uint i = 0; i < count; i++) previous.Add(1000 - i, Row(10000, "T" + i));
        Ranking.Save();
        uint[] expected = Enumerable.Range(0, Math.Min(100, count)).Select(i => 1000u - (uint)i).ToArray();
        Assert.Equal(expected, Ranking.RankingList!.Keys);
        foreach (uint key in expected) Assert.Same(previous[key], Ranking.RankingList![key]);
        string[] lines = File.ReadAllLines(path);
        Assert.Equal("Count=" + expected.Length, lines[0]);
        Assert.Equal(expected.Select((key, i) => "T" + i + "/10000/" + key + "/"), lines.Skip(1));
    }

    [Fact]
    public void RuneRankSaveSortsUnsignedExtremesAfterFilteringAndDoesNotMutateDroppedEntries()
    {
        using var scope = new Scope();
        string path = scope.Target("RuneRanking.txt");
        var drop = Row(9999, "Drop");
        Ranking.RankingList = new() { [1] = Row(10000), [2] = Row(uint.MaxValue), [3] = drop, [4] = Row(0x80000000) };
        Ranking.Save();
        Assert.Equal(new uint[] { 2, 4, 1 }, Ranking.RankingList!.Keys);
        Assert.Equal(9999u, drop.TotalPoints);
        Assert.Equal(4, File.ReadAllLines(path).Length);
    }

    [Fact]
    public void RuneRankUnpublishedCacheFaultsWithoutClientOrFileFallback()
    {
        using var scope = new Scope();
        var client = scope.Client();
        string path = scope.Target("RuneRanking.txt");
        File.WriteAllText(path, "preserved");
        Ranking.RankingList = null;
        Assert.Throws<NullReferenceException>(() => Ranking.Update(client));
        Assert.Throws<ArgumentNullException>(() => Ranking.Save());
        Assert.Null(Ranking.RankingList);
        Assert.Equal("preserved", File.ReadAllText(path));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void RuneRankSingletonNullAndValidPrefixFaultBeforeReplacingCacheOrOpeningWriter(bool prefix)
    {
        using var scope = new Scope();
        string path = scope.Target("RuneRanking.txt");
        File.WriteAllText(path, "preserved");
        var previous = Ranking.RankingList!;
        if (prefix) previous.Add(1, Row(20000));
        previous.Add(2, null!);
        Assert.Throws<NullReferenceException>(() => Ranking.Save());
        Assert.Same(previous, Ranking.RankingList);
        Assert.Equal(prefix ? 2 : 1, previous.Count);
        Assert.Equal("preserved", File.ReadAllText(path));
    }

    [Fact]
    public void RuneRankUpdateFaultKeepsEntryMutationBeforeLaterNullEnumeration()
    {
        using var scope = new Scope();
        var client = scope.Client();
        var previous = Ranking.RankingList!;
        var entry = Row(50000, "Before");
        previous.Add(17, entry);
        previous.Add(18, null!);
        Assert.Throws<NullReferenceException>(() => Ranking.Update(client));
        Assert.Same(previous, Ranking.RankingList);
        Assert.Equal(10000u, entry.TotalPoints);
        Assert.Equal("Hero", entry.Name);
    }
}
