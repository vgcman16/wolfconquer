using System.Globalization;
using Ranking = WolfConquer.Core.Data.LegacyNativeNinjaRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaRankTests
{
    [Fact]
    public void SaveAndLoadRoundTripAllTwentyOneFieldsThroughGenuineRecordAndFileOwners()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        Ranking.Create();
        var original = Entry();
        Place(original);
        Ranking.Save();
        files.Expect(Text(FullRow));
        Ranking.Load();
        Assert.Equal(6, Ranking.Ranks.Count);
        var loaded = Assert.Single(Ranking.Ranks[Ranking.Type.Water].RankingList).Value;
        Assert.NotSame(original, loaded);
        Assert.Equal(Values(original), Values(loaded));
    }

    [Fact]
    public void LiteralLocationPrefixControlsBothReaderAndWriterWithoutSeparatorRepair()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        files.Seed(Text(Row(88)));
        files.Seed(Text(FullRow), prefix: true);
        files.UseLocation(prefix: true);
        Ranking.Load();
        Assert.Same(Ranking.Ranks[Ranking.Type.Water].RankingList[41], Ranking.GetInfo(Ranking.Type.Water, 41));
        Assert.Empty(Ranking.Ranks[Ranking.Type.Overall].RankingList);
        Ranking.Save();
        files.Expect(Text(FullRow), prefix: true);
        files.Expect(Text(Row(88)));
    }

    [Fact]
    public void ExplicitZeroCountPublishesExactlySixFreshEmptyOwnersAndLeavesEscapedGraphIntact() =>
        WithOwnedRankPersistence(files =>
    {
        files.Seed(Text());
        _ = SeedPersistenceGraph();
        var extra = new Ranking.Rank((Ranking.Type)99) { MaxItems = 9 };
        Ranking.Ranks.Add((Ranking.Type)99, extra);
        _ = Ranking.Ranks.GetValues();
        var before = new PersistenceGraphImage();
        Ranking.Load();
        RequireFreshSixRanks(before);
        Assert.All(Ranking.Ranks.Values, rank => Assert.Empty(rank.RankingList));
        Assert.False(Ranking.Ranks.ContainsKey((Ranking.Type)99));
        files.Expect(Text());
    });

    [Fact]
    public void InvalidPhysicalTypeRefusesWithoutPublishingAnyEarlierRows() => WithOwnedRankPersistence(files =>
    {
        files.Seed(Text(FullRow, Row(19, type: "6"), Row(23)));
        RequireRejectedRankLoad(files);
    });

    [Theory]
    [InlineData("short")]
    [InlineData("uid-overflow")]
    [InlineData("type-byte-overflow")]
    [InlineData("points-format")]
    [InlineData("level-overflow")]
    [InlineData("mesh-negative")]
    public void ShortOrMalformedSavedFieldsCannotPublishDefaultsOrAnEarlierPrefix(string invalid) =>
        WithOwnedRankPersistence(files =>
    {
        string[] fields = FullRow.Split('/');
        fields[0] = "19";
        switch (invalid)
        {
            case "uid-overflow": fields[0] = "4294967296"; break;
            case "type-byte-overflow": fields[2] = "256"; break;
            case "points-format": fields[3] = "bad"; break;
            case "level-overflow": fields[5] = "256"; break;
            case "mesh-negative": fields[6] = "-1"; break;
        }
        files.Seed(Text(FullRow, invalid == "short" ? "17/Short/1/" : string.Join('/', fields), Row(23)));
        RequireRejectedRankLoad(files);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingDeclaredRowsAndDuplicateUidRefuseTheWholeSnapshot(bool duplicate) => WithOwnedRankPersistence(files =>
    {
        files.Seed(duplicate ? Text(FullRow, Row(17, 10), Row(17, 20)) :
            "Count=3" + Environment.NewLine + FullRow + Environment.NewLine + Row(17, 10) + Environment.NewLine);
        RequireRejectedRankLoad(files);
    });

    [Fact]
    public void ReaderAndWriterUseCurrentCultureIncludingCustomPositiveSign()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        string[] fields = FullRow.Split('/');
        for (int index = 0; index < 21; index++)
            if (index is not 1 and not 20) fields[index] = "p" + fields[index];
        files.Seed("Count=p1" + Environment.NewLine + string.Join('/', fields) + Environment.NewLine);
        Ranking.Load();
        Assert.Equal(Values(Entry()), Values(Ranking.Ranks[Ranking.Type.Water].RankingList[41]));
        Ranking.Save();
        files.Expect(Text(FullRow));
    }

    [Fact]
    public void SaveUsesTheCurrentCompleteRegistryRatherThanItsStaleCachedArray() => WithOwnedRankPersistence(files =>
    {
        Ranking.Create();
        var old = Place(Entry());
        var cached = Ranking.Ranks.GetValues();
        var replacement = new Ranking.Rank(Ranking.Type.Water) { MaxItems = 10 };
        Ranking.Ranks[Ranking.Type.Water] = replacement;
        long revision = Ranking.Ranks.Revision;
        Assert.True(Ranking.Ranks.Update); Assert.Contains(old, cached);
        Assert.Null(Ranking.GetInfo(Ranking.Type.Water, 41));
        Ranking.Save();
        files.Expect(Text());
        Assert.Equal(revision, Ranking.Ranks.Revision); Assert.True(Ranking.Ranks.Update);
        Assert.Same(replacement, Ranking.Ranks[Ranking.Type.Water]);
        var current = Ranking.Ranks.GetValues();
        Assert.Contains(replacement, current); Assert.DoesNotContain(old, current);
        Ranking.Ranks.Add(Ranking.Type.Water, replacement);
        Ranking.Save();
        files.Expect(Text());
        Assert.Single(old.RankingList);
    });

    [Fact]
    public void SaveRefusesAnInvalidPhysicalTypeBeforeNarrowingOrTouchingTheFile() => WithOwnedRankPersistence(files =>
    {
        Ranking.Create();
        files.Seed("owned prior bytes");
        var row = Entry();
        row.Type = (Ranking.Type)258;
        Place(row);
        var before = new PersistenceGraphImage();
        var error = Record.Exception(Ranking.Save);
        before.RequireCurrent();
        files.Expect("owned prior bytes");
        Assert.IsType<InvalidOperationException>(error);
    });

    [Fact]
    public void SaveReadOnlyShareFaultPreservesExistingFileAndLiveRows()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        string before = "owned previous bytes";
        files.Seed(before);
        Ranking.Create();
        var row = Entry();
        Place(row);
        _ = Ranking.Ranks.GetValues();
        var graph = new PersistenceGraphImage();
        using (var held = new FileStream(files.PathFor(), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => Ranking.Save());
        graph.RequireCurrent();
        files.Expect(before);
        Assert.Same(row, Ranking.Ranks[Ranking.Type.Water].RankingList[41]);
    }
}
