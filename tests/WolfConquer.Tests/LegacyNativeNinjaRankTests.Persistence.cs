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
    public void ZeroCountReaderFalseStillRunsCreateAndPreservesExtraBuckets()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        files.Seed(Text());
        var old = Place(Entry(), Ranking.Type.Overall);
        var extra = new Ranking.Rank((Ranking.Type)99) { MaxItems = 9 };
        Ranking.Ranks.Add((Ranking.Type)99, extra);
        Ranking.Load();
        Assert.Equal(7, Ranking.Ranks.Count);
        Assert.NotSame(old, Ranking.Ranks[Ranking.Type.Overall]);
        Assert.Single(old.RankingList);
        Assert.Same(extra, Ranking.Ranks[(Ranking.Type)99]);
        Assert.All(Enumerable.Range(0, 6).Select(value => Ranking.Ranks[(Ranking.Type)value]),
            rank => Assert.Empty(rank.RankingList));
    }

    [Fact]
    public void InvalidPhysicalTypeFaultsAfterEarlierRowsAndBeforeLaterRows()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        files.Seed(Text(FullRow, Row(19, type: "6"), Row(23)));
        Assert.Throws<NullReferenceException>(() => Ranking.Load());
        Assert.Equal(6, Ranking.Ranks.Count);
        Assert.Single(Ranking.Ranks[Ranking.Type.Water].RankingList);
        Assert.Equal(21, Values(Ranking.Ranks[Ranking.Type.Water].RankingList[41]).Length);
        Assert.Empty(Ranking.Ranks[Ranking.Type.Overall].RankingList);
        Assert.Null(Ranking.Ranks[Ranking.Type.Count]);
    }

    [Fact]
    public void ShortRowsOverflowAndMalformedFieldsUseLiteralReadLineDefaults()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        string[] fields = FullRow.Split('/');
        fields[0] = "4294967296";
        fields[2] = "256"; // Type is read as byte then widened, not uint.
        fields[3] = "bad";
        fields[5] = "256";
        fields[6] = "-1";
        files.Seed(Text("17/Short/1/", string.Join('/', fields)));
        Ranking.Load();
        var shortRow = Assert.Single(Ranking.Ranks[Ranking.Type.Fire].RankingList).Value;
        Assert.Equal(new object[] { 17u, "Short", 1u, 0u, 0u, (byte)0, 0u, 0u, 0u, 0u, 0u,
            0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, 0u, "" }, Values(shortRow));
        var malformed = Assert.Single(Ranking.Ranks[Ranking.Type.Overall].RankingList).Value;
        Assert.Equal(0u, malformed.UID);
        Assert.Equal(0u, malformed.TotalPoints);
        Assert.Equal((byte)0, malformed.Level);
        Assert.Equal(0u, malformed.Mesh);
        Assert.Equal(505u, malformed.Class);
        Assert.Equal("Guild", malformed.GuildName);
    }

    [Fact]
    public void DeclaredRowsPastEofCreateDefaultUidZeroRecordAndDuplicateRowsReplaceEarlierAliases()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        files.Seed("Count=4" + Environment.NewLine + Row(17, 10) + Environment.NewLine + Row(17, 20) + Environment.NewLine);
        Ranking.Load();
        var rank = Ranking.Ranks[Ranking.Type.Overall];
        Assert.Equal(2, rank.RankingList.Count);
        Assert.Equal(20u, rank.RankingList[17].TotalPoints);
        Assert.Equal(0u, rank.RankingList[0].TotalPoints);
        Assert.Equal("", rank.RankingList[0].Name);
        Assert.Equal(new uint[] { 17, 0 }, rank.RankingList.Keys);
    }

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
    public void SaveUsesStaleSafeDictionaryArrayUntilARealAddInvalidatesIt()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        var old = Place(Entry());
        var cached = Ranking.Ranks.GetValues();
        var replacement = new Ranking.Rank(Ranking.Type.Water) { MaxItems = 10 };
        Ranking.Ranks[Ranking.Type.Water] = replacement;
        Assert.Same(cached, Ranking.Ranks.GetValues());
        Assert.Null(Ranking.GetInfo(Ranking.Type.Water, 41));
        Ranking.Save();
        files.Expect(Text(FullRow));
        Ranking.Ranks.Add(Ranking.Type.Water, replacement);
        Ranking.Save();
        files.Expect(Text());
        Assert.Single(old.RankingList);
    }

    [Fact]
    public void SaveNarrowsTypeToByteAndNullStringFaultNeverExecutesQueuedPrefix()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        var row = Entry();
        row.Type = (Ranking.Type)258;
        var rank = Place(row);
        Ranking.Save();
        files.Expect(Text(FullRow)); // 258 narrows to native byte2.
        rank.RankingList.Add(19, new Ranking.Entry { UID = 19, Name = null! });
        Assert.Throws<NullReferenceException>(() => Ranking.Save());
        files.Expect(Text(FullRow));
    }

    [Fact]
    public void SaveReadOnlyShareFaultPreservesExistingFileAndLiveRows()
    {
        using var state = new State();
        using var files = new OwnedRankFile();
        string before = "owned previous bytes";
        files.Seed(before);
        var row = Entry();
        Place(row);
        using (var held = new FileStream(files.PathFor(), FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.Throws<IOException>(() => Ranking.Save());
        files.Expect(before);
        Assert.Same(row, Ranking.Ranks[Ranking.Type.Water].RankingList[41]);
    }
}
