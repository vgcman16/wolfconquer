using Ranking = WolfConquer.Core.Data.LegacyNativePrestigeRanking;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativePrestigeRankingTests
{
    [Fact]
    public void SaveAndLoadRoundTripAllFieldsPointsAndSharedClassWorldEntryIdentity()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        var original=Entry();
        Place(original);
        var world=Place(Entry(88),Ranking.Type.World);
        Ranking.Save();
        files.Expect(Text(FullRow)); // World-only88 is deliberately not persisted.
        Ranking.Load();
        Assert.Equal(12,Ranking.Ranks.Count);
        var loaded=Assert.Single(Ranking.Ranks[Ranking.Type.Ninja].Items).Value;
        Assert.NotSame(original,loaded);
        Assert.Equal(Values(original),Values(loaded));
        Assert.Equal(original.Points,loaded.Points);
        Assert.NotSame(original.Points,loaded.Points);
        Assert.Same(loaded,Ranking.Ranks[Ranking.Type.World].Items[41]);
        Assert.Same(loaded,Ranking.Ranks[Ranking.Type.Ninja].BestOfClass);
        Assert.Same(loaded,Ranking.BestOfTheWorld);
        Assert.Single(world.Items); // Escaped old bucket not cleared by Create.
    }

    [Fact]
    public void LiteralPathPrefixControlsReaderAndWriterWithoutSeparatorRepair()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        files.Seed(Text(Row(88)));
        files.Seed(Text(FullRow),prefix:true);
        files.UseLocation(prefix:true);
        Ranking.Load();
        Assert.Same(Ranking.Ranks[Ranking.Type.Ninja].Items[41],Ranking.GetInfo(41));
        Assert.Null(Ranking.GetInfo(88));
        Ranking.Save();
        files.Expect(Text(FullRow),prefix:true);
        files.Expect(Text(Row(88)));
    }

    [Fact]
    public void LoadSafetyCompleteZeroCountPublishesExactlyTwelveFreshStandardBucketsAndPreservesEscapedOwners() =>
        RunOwnedPrestigeLoad(files =>
        {
            SeedPriorPrestigeGraph();
            var prior = new PublishedPrestigeGraph();
            var ranks = Ranking.Ranks;
            files.Seed(Text());
            Ranking.Load();
            Assert.NotSame(ranks, Ranking.Ranks);
            Assert.Equal(Enumerable.Range(0, 12).Select(index => (Ranking.Type)index), Ranking.Ranks.Keys);
            foreach (var (type, bucket) in Ranking.Ranks)
            {
                Assert.NotSame(ranks[type], bucket);
                Assert.Equal(type, bucket._Type);
                Assert.Equal(type == Ranking.Type.World ? 100 : 30, bucket.MaxItems);
                Assert.Empty(bucket.Items);
                Assert.Null(bucket.BestOfClass);
            }
            Assert.Null(Ranking.BestOfTheWorld);
            Assert.False(Ranking.Ranks.ContainsKey((Ranking.Type)99));
            prior.RequireEscapedOwnersUnchanged();
            files.Expect(Text());
        });

    [Fact]
    public void LoadSafetyInvalidTypeAfterAValidPrefixRefusesWithoutPublishingAnyGraph() =>
        RunOwnedPrestigeLoad(files => RequireRejectedPrestigeText(files,
            Text(FullRow, Row(19, type: "12"), Row(23))));

    [Fact]
    public void LoadSafetyNegativePointArrayCountAfterAValidPrefixRefusesWithoutPublishingAnyGraph() =>
        RunOwnedPrestigeLoad(files => RequireRejectedPrestigeText(files,
            Text(FullRow, Row(19, count: -1), Row(23))));

    [Fact]
    public void LoadSafetyMalformedFieldsCannotDefaultToZeroUidOrPublishAnEarlierEligibleRow() =>
        RunOwnedPrestigeLoad(files =>
        {
            var fields = FullRow.Split('/');
            fields[0] = "4294967296";
            fields[2] = "256";
            fields[4] = "256";
            fields[5] = "-1";
            fields[21] = "3";
            fields[22] = "bad";
            RequireRejectedPrestigeText(files, Text(FullRow, Row(18, 19_999), string.Join('/', fields)));
        });

    [Fact]
    public void SaveNullStringRefusalDoesNotExecuteAlreadyQueuedRowsOrOverwritePreviousBytes()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        files.Seed("previous owned bytes");
        var rank=Place(Entry());
        rank.Items.Add(19,new Ranking.Entry{UID=19,Name=null!});
        Assert.Throws<InvalidOperationException>(()=>Ranking.Save());
        files.Expect("previous owned bytes");
        Assert.Equal(2,rank.Items.Count);
    }

    [Fact]
    public void SaveReadOnlySharingFaultPreservesFileAndLiveEntry()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        files.Seed("previous owned bytes");
        var row=Entry();
        var rank=Place(row);
        using(var held=new FileStream(files.PathFor(),FileMode.Open,FileAccess.Read,FileShare.Read))
            Assert.Throws<IOException>(()=>Ranking.Save());
        files.Expect("previous owned bytes");
        Assert.Same(row,rank.Items[41]);
    }
}
