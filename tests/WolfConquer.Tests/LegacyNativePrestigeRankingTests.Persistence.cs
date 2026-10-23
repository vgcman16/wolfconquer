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
    public void ZeroCountStillCreatesFreshStandardBucketsAndPreservesExtraBuckets()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        files.Seed(Text());
        var old=Place(Entry());
        var extra=Place(Entry(99),(Ranking.Type)99);
        Ranking.Load();
        Assert.Equal(13,Ranking.Ranks.Count);
        Assert.NotSame(old,Ranking.Ranks[Ranking.Type.Ninja]);
        Assert.Single(old.Items);
        Assert.Same(extra,Ranking.Ranks[(Ranking.Type)99]);
        Assert.All(Enumerable.Range(0,12),i=>Assert.Empty(Ranking.Ranks[(Ranking.Type)i].Items));
    }

    [Fact]
    public void InvalidTypeFaultKeepsEarlierClassAndWorldRowsWithoutPublishingLaterRows()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        files.Seed(Text(FullRow,Row(19,type:"12"),Row(23)));
        Assert.Throws<NullReferenceException>(()=>Ranking.Load());
        var row=Assert.Single(Ranking.Ranks[Ranking.Type.Ninja].Items).Value;
        Assert.Equal(41u,row.UID);
        Assert.Same(row,Assert.Single(Ranking.Ranks[Ranking.Type.World].Items).Value);
        Assert.Null(Ranking.Ranks[Ranking.Type.Count]);
    }

    [Fact]
    public void NegativePointArrayCountFaultRetainsEarlierRowsBeforeAddingFaultingRowToEitherRank()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        files.Seed(Text(FullRow,Row(19,count:-1),Row(23)));
        Assert.Throws<OverflowException>(()=>Ranking.Load());
        Assert.Equal(new uint[]{41},Ranking.Ranks[Ranking.Type.Ninja].Items.Keys);
        Assert.Equal(new uint[]{41},Ranking.Ranks[Ranking.Type.World].Items.Keys);
    }

    [Fact]
    public void ShortMalformedAndLowPointRowsUseLiteralDefaultsAndThresholdWhileMissingPointsDefaultZero()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        var fields=FullRow.Split('/');
        fields[0]="4294967296";
        fields[2]="256"; // Byte parse default0 becomes World, not an UInt32 type256.
        fields[4]="256";
        fields[5]="-1";
        fields[21]="3";
        fields[22]="bad";
        files.Seed(Text("17/Short/4/",Row(18,19999),string.Join('/',fields)));
        Ranking.Load();
        Assert.Empty(Ranking.Ranks[Ranking.Type.Ninja].Items);
        var malformed=Assert.Single(Ranking.Ranks[Ranking.Type.World].Items).Value;
        Assert.Equal(0u,malformed.UID);
        Assert.Equal((byte)0,malformed.Level);
        Assert.Equal(0u,malformed.Mesh);
        Assert.Equal(30004u,malformed.TotalPoints);
        Assert.Equal(new uint[]{0,202,0},malformed.Points);
    }

    [Fact]
    public void SaveWritesNullPointsAsZeroCountAndNarrowsEntryTypeWithoutChangingItsBucket()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        var row=Entry();
        row.type=(Ranking.Type)260;
        row.Points=null!;
        Place(row);
        Ranking.Save();
        files.Expect(Text(FullRow[..FullRow.IndexOf("2/201/202/",StringComparison.Ordinal)]+"0/"));
        Assert.Equal((Ranking.Type)260,row.type);
    }

    [Fact]
    public void SaveUsesStaleSafeDictionaryArrayUntilNativeAddInvalidatesIt()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        var old=Place(Entry());
        var cached=Ranking.Ranks.GetValues();
        var replacement=new Ranking.Rank(Ranking.Type.Ninja){MaxItems=30};
        Ranking.Ranks[Ranking.Type.Ninja]=replacement;
        Assert.Same(cached,Ranking.Ranks.GetValues());
        Ranking.Save();
        files.Expect(Text(FullRow));
        Ranking.Ranks.Add(Ranking.Type.Ninja,replacement);
        Ranking.Save();
        files.Expect(Text());
        Assert.Single(old.Items);
    }

    [Fact]
    public void SaveNullStringFaultDoesNotExecuteAlreadyQueuedRowsOrOverwritePreviousBytes()
    {
        using var state=new State();
        using var files=new OwnedRankFile();
        files.Seed("previous owned bytes");
        var rank=Place(Entry());
        rank.Items.Add(19,new Ranking.Entry{UID=19,Name=null!});
        Assert.Throws<NullReferenceException>(()=>Ranking.Save());
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
