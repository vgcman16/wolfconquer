using System.Globalization;
using Ranking = WolfConquer.Core.Data.LegacyNativeHWRank;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHWRankTests
{
    [Fact]
    public void SaveLoadRoundTripTwentyOneFieldsAndShareTheLoadedEntryBetweenOwnAndMainRanks()
    {
        using var state = new State(); using var files = new OwnedRankFile(); var original = Entry(); Place(original);
        Ranking.Save(); files.Expect(Text(FullRow)); Ranking.Load();
        Assert.Equal(10, Ranking.Ranks.Count);
        var loaded = Assert.Single(Ranking.Ranks[Ranking.Type.Judge].RankingList).Value;
        Assert.NotSame(original, loaded); Assert.Equal(Values(original), Values(loaded));
        Assert.Same(loaded, Assert.Single(Ranking.Ranks[Ranking.Type.Main].RankingList).Value);
        loaded.Name = "changed shared"; Assert.Equal("changed shared", Ranking.GetInfo(Ranking.Type.Main, 41).Name);
        loaded.Name = "Hero"; Ranking.Save(); files.Expect(Text(FullRow)); // Main is not saved a second time.
    }

    [Fact]
    public void LaterWeaponRowOverwritesMainEvenWhenItsScoreIsLowerWhileOwnRanksRetainDifferentAliases()
    {
        using var state = new State(); using var files = new OwnedRankFile();
        files.Seed(Text(Row(17, 900, "1"), Row(17, 1, "2"))); Ranking.Load();
        var blade = Ranking.Ranks[Ranking.Type.Dominator].RankingList[17];
        var sword = Ranking.Ranks[Ranking.Type.Judge].RankingList[17];
        Assert.NotSame(blade, sword); Assert.Equal(900u, blade.TotalPoints); Assert.Equal(1u, sword.TotalPoints);
        Assert.Same(sword, Ranking.Ranks[Ranking.Type.Main].RankingList[17]);
        Ranking.Save(); files.Expect(Text(Row(17, 900, "1"), Row(17, 1, "2")));
    }

    [Fact]
    public void RawLocationPrefixIsUsedByBothReaderAndWriterWithoutSeparatorRepair()
    {
        using var state = new State(); using var files = new OwnedRankFile();
        files.Seed(Text(Row(88))); files.Seed(Text(FullRow), prefix: true); files.UseLocation(prefix: true);
        Ranking.Load(); Assert.Equal(41u, Ranking.BestOf(Ranking.Type.Main).UID);
        Ranking.Save(); files.Expect(Text(FullRow), prefix: true); files.Expect(Text(Row(88)));
    }

    [Fact]
    public void ZeroCountStillCreatesTenRanksAndRetainsOnlyExtraCatalogOwners()
    {
        using var state = new State(); using var files = new OwnedRankFile(); files.Seed(Text());
        var old = Place(Entry(), Ranking.Type.Main, Ranking.Type.Main);
        var extra = new Ranking.Rank((Ranking.Type)99) { MaxItems = 9 }; Ranking.Ranks.Add((Ranking.Type)99, extra);
        Ranking.Load(); Assert.Equal(11, Ranking.Ranks.Count); Assert.NotSame(old, Ranking.Ranks[Ranking.Type.Main]);
        Assert.Single(old.RankingList); Assert.Same(extra, Ranking.Ranks[(Ranking.Type)99]);
        Assert.All(Enumerable.Range(0, 10).Select(i => Ranking.Ranks[(Ranking.Type)i]), rank => Assert.Empty(rank.RankingList));
    }

    [Fact]
    public void InvalidRankTypeFaultsAfterCompleteEarlierOwnAndMainPublicationBeforeLaterRows()
    {
        using var state = new State(); using var files = new OwnedRankFile();
        files.Seed(Text(FullRow, Row(19, type: "10"), Row(23)));
        Assert.Throws<NullReferenceException>(() => Ranking.Load());
        var loaded = Assert.Single(Ranking.Ranks[Ranking.Type.Judge].RankingList).Value;
        Assert.Equal(Values(Entry()), Values(loaded)); Assert.Same(loaded, Assert.Single(Ranking.Ranks[Ranking.Type.Main].RankingList).Value);
        Assert.Null(Ranking.Ranks[Ranking.Type.Count]); Assert.Equal(10, Ranking.Ranks.Count);
    }

    [Fact]
    public void ShortMalformedAndOverflowColumnsKeepNativeDefaultsAndMainAliasing()
    {
        using var state = new State(); using var files = new OwnedRankFile();
        string[] fields = FullRow.Split('/'); fields[0] = "4294967296"; fields[2] = "256";
        fields[3] = "bad"; fields[5] = "256"; fields[6] = "-1";
        files.Seed(Text("17/Short/2/", string.Join('/', fields))); Ranking.Load();
        var shortRow = Assert.Single(Ranking.Ranks[Ranking.Type.Judge].RankingList).Value;
        Assert.Equal(new object[] { 17u, "Short", 2u, 0u, 0u, (byte)0, 0u, 0u, 0u, 0u, 0u,
            0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, "" }, Values(shortRow));
        Assert.Same(shortRow, Ranking.Ranks[Ranking.Type.Main].RankingList[17]);
        var malformed = Ranking.Ranks[Ranking.Type.Main].RankingList[0];
        Assert.Equal(Ranking.Type.Main, malformed.Type); Assert.Equal(0u, malformed.TotalPoints);
        Assert.Equal((byte)0, malformed.Level); Assert.Equal(0u, malformed.Mesh); Assert.Equal(505u, malformed.Class);
        Assert.Equal("Guild", malformed.GuildName);
    }

    [Fact]
    public void DeclaredRowsPastEofCreateDefaultMainUidZeroAndDuplicatesReplaceOwnAndMainRows()
    {
        using var state = new State(); using var files = new OwnedRankFile();
        files.Seed("Count=4" + Environment.NewLine + Row(17, 10, "2") + Environment.NewLine + Row(17, 20, "2") + Environment.NewLine);
        Ranking.Load(); var own = Assert.Single(Ranking.Ranks[Ranking.Type.Judge].RankingList).Value;
        Assert.Equal(20u, own.TotalPoints); Assert.Same(own, Ranking.Ranks[Ranking.Type.Main].RankingList[17]);
        var empty = Ranking.Ranks[Ranking.Type.Main].RankingList[0]; Assert.Equal("", empty.Name);
        Assert.Equal(0u, empty.TotalPoints); Assert.Equal(new uint[] { 17, 0 }, Ranking.Ranks[Ranking.Type.Main].RankingList.Keys);
    }

    [Fact]
    public void SaveFiltersPhysicalRankTypeNotCatalogKeyAndNarrowsEntryTypeToByte()
    {
        using var state = new State(); using var files = new OwnedRankFile();
        var row = Entry(); row.Type = (Ranking.Type)258;
        Place(row, Ranking.Type.Main, Ranking.Type.Judge);
        Place(Entry(19), Ranking.Type.Judge, Ranking.Type.Main);
        Ranking.Save(); files.Expect(Text(FullRow));
        Assert.Equal((Ranking.Type)258, row.Type); Assert.Equal(2, Ranking.Ranks.Count);
    }

    [Fact]
    public void SaveUsesRetainedCachedRankArrayUntilRealAddInvalidatesIt()
    {
        using var state = new State(); using var files = new OwnedRankFile(); var old = Place(Entry());
        var cached = Ranking.Ranks.GetValues(); var replacement = new Ranking.Rank(Ranking.Type.Judge) { MaxItems = 10 };
        Ranking.Ranks[Ranking.Type.Judge] = replacement; Assert.Same(cached, Ranking.Ranks.GetValues());
        Ranking.Save(); files.Expect(Text(FullRow)); Assert.Null(Ranking.GetInfo(Ranking.Type.Judge, 41));
        Ranking.Ranks.Add(Ranking.Type.Judge, replacement); Ranking.Save(); files.Expect(Text()); Assert.Single(old.RankingList);
    }

    [Fact]
    public void SaveFaultDoesNotExecutePreparedPrefixAndReadShareFailureKeepsOriginalFile()
    {
        using var state = new State(); using var files = new OwnedRankFile();
        var rank = Place(Entry()); Ranking.Save(); files.Expect(Text(FullRow));
        rank.RankingList.Add(19, new Ranking.Entry { UID = 19, Name = null! });
        Assert.Throws<NullReferenceException>(() => Ranking.Save()); files.Expect(Text(FullRow));
        rank.RankingList.Remove(19);
        using (var held = new FileStream(files.PathFor(), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => Ranking.Save());
        files.Expect(Text(FullRow)); Assert.Equal(41u, Assert.Single(rank.RankingList).Value.UID);
    }

    [Fact]
    public void FileAndRecordParsingUseCurrentCultureIncludingCustomPositiveSign()
    {
        using var state = new State(); using var files = new OwnedRankFile();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone(); culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture; string[] fields = FullRow.Split('/');
        for (int i = 0; i < 21; i++) if (i is not 1 and not 20) fields[i] = "p" + fields[i];
        files.Seed("Count=p1" + Environment.NewLine + string.Join('/', fields) + Environment.NewLine);
        Ranking.Load(); Assert.Equal(Values(Entry()), Values(Ranking.Ranks[Ranking.Type.Judge].RankingList[41]));
        Ranking.Save(); files.Expect(Text(FullRow));
    }
}
