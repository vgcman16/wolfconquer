using System.Globalization;
using Sort = WolfConquer.Core.Data.LegacyNativeDbLevExp.Sort;
using SubClass = WolfConquer.Core.World.LegacyNativeSubClassRuntime;
using Row = WolfConquer.Core.World.LegacyNativeMsgSubClass.SubClases;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeSubClassTests
{
    [Fact]
    public Task ConstructorOwnsFreshTypedDictionaryAndCreatePreservesUnknownIdWithNativeNineDefaults() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var other = new SubClass();
        Assert.NotSame(scope.Runtime.src, other.src);
        Assert.Empty(scope.Runtime.src);
        Assert.Equal(Sort.User, scope.Runtime.ActiveSubclass);
        Assert.Equal((ushort)0, scope.Runtime.StudyPoints);
        Assert.Equal("0#0#0#", scope.Runtime.ToString());
        RowIs(new Row(), Sort.User, 0, 0);
        RowIs(Row.Create((Sort)255), (Sort)255, 9, 9);
        Assert.Null(scope.Client.Player.SubClass);
        Assert.Equal(Sort.User, scope.Client.Player.ActiveSublass);
        Assert.Equal(0u, scope.Client.Player.SubClassHasPoints);
        scope.Unwritten();
    });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullAndEmptyLoadDoNotChangeHeadersOrRetainedRows(string? line)
    {
        using var state = new State();
        state.Runtime.StudyPoints = 17;
        state.Runtime.ActiveSubclass = Sort.Sage;
        var row = state.Row(Sort.Warlock, Sort.Wrangler, 7, 5);
        var dictionary = state.Runtime.src;
        state.Runtime.Load(line);
        Assert.Equal((ushort)17, state.Runtime.StudyPoints);
        Assert.Equal(Sort.Sage, state.Runtime.ActiveSubclass);
        Assert.Same(dictionary, state.Runtime.src);
        Assert.Same(row, Assert.Single(dictionary).Value);
        RowIs(row, Sort.Wrangler, 7, 5);
    }

    [Fact]
    public void NonemptyInvalidLineResetsHeaderDefaultsButDoesNotClearExistingClasses()
    {
        using var state = new State();
        var row = state.Row(Sort.Warlock, Sort.Warlock);
        state.Runtime.StudyPoints = 17;
        state.Runtime.ActiveSubclass = Sort.Sage;
        state.Runtime.Load("not-separated");
        Assert.Equal((ushort)0, state.Runtime.StudyPoints);
        Assert.Equal(Sort.User, state.Runtime.ActiveSubclass);
        Assert.Same(row, Assert.Single(state.Runtime.src).Value);
    }

    [Fact]
    public void RecordUsesCurrentCultureAndFullByteRangesWithFirstDuplicateWinning()
    {
        using var state = new State();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        state.Runtime.Load(" p65535 # p255 # p2 # p1 # p3 # p4 # p1 # p255 # p255 #ignored#");
        Assert.Equal(ushort.MaxValue, state.Runtime.StudyPoints);
        Assert.Equal((Sort)255, state.Runtime.ActiveSubclass);
        RowIs(Assert.Single(state.Runtime.src).Value, Sort.MartialArtist, 3, 4);
        Assert.Equal("65535#255#1#1#3#4#", state.Runtime.ToString());
    }

    [Fact]
    public void RepeatedLoadUpdatesHeadersAndAddsMissingKeysWithoutReplacingExistingRows()
    {
        using var state = new State();
        var old = state.Row(Sort.MartialArtist, Sort.Wrangler, 7, 5);
        var dictionary = state.Runtime.src;
        state.Runtime.Load("19#4#2#1#255#255#2#8#6#");
        Assert.Equal((ushort)19, state.Runtime.StudyPoints);
        Assert.Equal(Sort.Sage, state.Runtime.ActiveSubclass);
        Assert.Same(dictionary, state.Runtime.src);
        Assert.Same(old, dictionary[Sort.MartialArtist]);
        RowIs(old, Sort.Wrangler, 7, 5);
        RowIs(dictionary[Sort.Warlock], Sort.Warlock, 8, 6);
        Assert.Equal(2, dictionary.Count);
    }

    [Fact]
    public void MalformedAndTruncatedRowsDefaultIndividuallyWithoutDiscardingDeclaredRows()
    {
        using var state = new State();
        state.Runtime.Load("65536#256#2#-1#999#-2#9#255#256#");
        Assert.Equal((ushort)0, state.Runtime.StudyPoints);
        Assert.Equal(Sort.User, state.Runtime.ActiveSubclass);
        RowIs(state.Runtime.src[Sort.User], Sort.User, 0, 0);
        RowIs(state.Runtime.src[Sort.Wrangler], Sort.Wrangler, 255, 0);
        state.Runtime.Load("0#0#1#2#");
        RowIs(state.Runtime.src[Sort.Warlock], Sort.Warlock, 0, 0);
        Assert.Equal(3, state.Runtime.src.Count);
    }

    [Fact]
    public void LargestSafeByteLoopCountFinishesWithOneFirstWinsDefaultKey()
    {
        using var state = new State();
        state.Runtime.Load("17#9#255#"); // Never execute the native >=256 counter-wrap horizon.
        Assert.Equal((ushort)17, state.Runtime.StudyPoints);
        Assert.Equal(Sort.Wrangler, state.Runtime.ActiveSubclass);
        RowIs(Assert.Single(state.Runtime.src).Value, Sort.User, 0, 0);
    }

    [Fact]
    public void SaveUsesPhysicalIdsAndFreshLoadRekeysCollisionsWithFirstRecordWinning()
    {
        using var state = new State();
        state.Runtime.StudyPoints = 17;
        state.Runtime.ActiveSubclass = Sort.Sage;
        state.Row(Sort.MartialArtist, Sort.Wrangler, 3, 4);
        state.Row(Sort.Warlock, Sort.Wrangler, 7, 8);
        Row[] rows = state.Runtime.src.Values.ToArray();
        string expected = "17#4#2#" + string.Concat(rows.Select(row => $"9#{row.Level}#{row.Phrase}#"));
        Assert.Equal(expected, state.Runtime.ToString());
        var fresh = new SubClass();
        fresh.Load(expected);
        Assert.Equal((ushort)17, fresh.StudyPoints);
        Assert.Equal(Sort.Sage, fresh.ActiveSubclass);
        RowIs(Assert.Single(fresh.src).Value, Sort.Wrangler, rows[0].Level, rows[0].Phrase);
        Assert.NotSame(rows[0], fresh.src[Sort.Wrangler]);
        Assert.False(fresh.src.ContainsKey(Sort.MartialArtist));
        Assert.Equal(2, state.Runtime.src.Count);
    }

    [Fact]
    public void ClassGettersUseDictionaryKeyAndRetainedFieldsRatherThanPhysicalId()
    {
        using var state = new State();
        var row = state.Row(Sort.MartialArtist, Sort.Wrangler, 7, 5);
        Assert.Equal((byte)7, state.Runtime.GetClassLevel(Sort.MartialArtist));
        Assert.Equal((byte)5, state.Runtime.GetClassPhrase(Sort.MartialArtist));
        Assert.Equal((byte)0, state.Runtime.GetClassLevel(Sort.Wrangler));
        Assert.Equal((byte)0, state.Runtime.GetClassPhrase(Sort.Wrangler));
        row.Level = 255; row.Phrase = 254;
        Assert.Equal((byte)255, state.Runtime.GetClassLevel(Sort.MartialArtist));
        Assert.Equal((byte)254, state.Runtime.GetClassPhrase(Sort.MartialArtist));
        state.Runtime.src[Sort.Warlock] = null!;
        Assert.Throws<NullReferenceException>(() => state.Runtime.GetClassLevel(Sort.Warlock));
        Assert.Throws<NullReferenceException>(() => state.Runtime.GetClassPhrase(Sort.Warlock));
    }

    [Fact]
    public void HashReadsPhysicalIdsAndPhraseAndUnsignedAdditionWrapsWithoutNormalizingRows()
    {
        using var state = new State();
        var first = state.Row(Sort.MartialArtist, Sort.MartialArtist, phrase: 2);
        var second = state.Row(Sort.Warlock, Sort.ChiMaster, phrase: 4);
        Assert.Equal(402u, state.Runtime.GetHashPoint());
        first.ID = Sort.Wrangler; first.Phrase = 40;
        second.ID = Sort.Wrangler; second.Phrase = 3;
        // Each floating term remains in uint range; only the native sum wraps.
        Assert.Equal(5032704u, state.Runtime.GetHashPoint());
    }

    [Fact]
    public Task CreateSpawnWritesActualPlayerActiveClassAndComputedPhysicalHashWithoutSending() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Runtime.ActiveSubclass = Sort.Sage;
        scope.State.Row(Sort.MartialArtist, Sort.Warlock, phrase: 7);
        scope.Runtime.CreateSpawn(scope.Client);
        Assert.Equal(Sort.Sage, scope.Client.Player.ActiveSublass);
        Assert.Equal(70u, scope.Client.Player.SubClassHasPoints);
        Assert.Null(scope.Client.Player.SubClass);
        scope.Unwritten();
    });

    [Fact]
    public Task CreateSpawnPublishesActiveClassBeforeHashFaultAndRetainsOldHash() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Client.Player.SubClassHasPoints = 91;
        scope.Runtime.ActiveSubclass = Sort.Wrangler;
        scope.Runtime.src[Sort.MartialArtist] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Runtime.CreateSpawn(scope.Client));
        Assert.Equal(Sort.Wrangler, scope.Client.Player.ActiveSublass);
        Assert.Equal(91u, scope.Client.Player.SubClassHasPoints);
        scope.Unwritten();
    });

    [Fact]
    public void NullDictionaryFaultsAfterLoadHeaderPublicationAndIsNotRepaired()
    {
        using var state = new State();
        state.Runtime.src = null!;
        Assert.Throws<NullReferenceException>(() => state.Runtime.Load("17#9#1#1#2#3#"));
        Assert.Equal((ushort)17, state.Runtime.StudyPoints);
        Assert.Equal(Sort.Wrangler, state.Runtime.ActiveSubclass);
        Assert.Null(state.Runtime.src);
        Assert.Throws<NullReferenceException>(() => state.Runtime.ToString());
    }
}
