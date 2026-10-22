using System.Globalization;
using WolfConquer.Core.World;
using Storage = WolfConquer.Core.World.LegacyNativeCollectionStorageRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCollectionStorageTests
{
    [Fact]
    public Task ConstructorStoresPhysicalUserAndKeepsEachDictionaryIndependent() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var first = new Storage(scope.Client);
        var second = new Storage(scope.Client);
        Assert.Same(scope.Client, first.user);
        Assert.Empty(first.Items);
        Assert.Empty(first.Scraps);
        Assert.NotSame(first.Items, second.Items);
        Assert.NotSame(first.Scraps, second.Scraps);
        ItemIs(new Storage.Item(), 0, 0, 0, 0, 0);
        var scrap = new Storage.Scrap();
        Assert.Equal(0u, scrap.Type);
        Assert.Equal(0u, scrap.Amount);
        Assert.Null(scope.Client.CollectionStorage); // Full host constructor is deliberately unwired.
        Assert.Equal(0u, scope.Client.Player.CollectionID);
        scope.NoSerialization();
    });

    [Fact]
    public void NullUserStillOwnsUsableEmptyRecordStorage()
    {
        using var scope = new RecordScope();
        Assert.Null(scope.Storage.user);
        Assert.Equal("0/0/", scope.Storage.ToString());
        scope.Storage.Load("1/7/9/1/11/2/3/4/5/");
        Assert.Equal(9u, scope.Storage.Scraps[7].Amount);
        ItemIs(scope.Storage.Items[11], 11, 2, 3, 4, 5);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("1")]
    [InlineData("bad/invalid/")]
    public void InvalidOrEmptyCountsLeaveExistingDictionariesAndValuesUntouched(string? line)
    {
        using var scope = new RecordScope();
        var scrap = new Storage.Scrap { Type = 7, Amount = 9 };
        var item = new Storage.Item { Type = 11, Activate = 2, Amount = 3, RemainingTime = 4, F5 = 5 };
        scope.Storage.Scraps[7] = scrap;
        scope.Storage.Items[11] = item;
        var scraps = scope.Storage.Scraps;
        var items = scope.Storage.Items;
        scope.Storage.Load(line);
        Assert.Same(scraps, scope.Storage.Scraps);
        Assert.Same(items, scope.Storage.Items);
        Assert.Same(scrap, Assert.Single(scope.Storage.Scraps).Value);
        Assert.Same(item, Assert.Single(scope.Storage.Items).Value);
        Assert.Equal("1/7/9/1/11/2/3/4/5/", scope.Storage.ToString());
    }

    [Fact]
    public void FullRecordLoadsEveryUnsignedFieldWithoutClampingOrExpirationConversion()
    {
        using var scope = new RecordScope();
        scope.Storage.Load("1/4294967295/4294967295/1/4300000/4294967295/17/4294967295/23/ignored/trailing/");
        var scrap = Assert.Single(scope.Storage.Scraps).Value;
        Assert.Equal(uint.MaxValue, scrap.Type);
        Assert.Equal(uint.MaxValue, scrap.Amount);
        ItemIs(Assert.Single(scope.Storage.Items).Value, 4300000, uint.MaxValue, 17, uint.MaxValue, 23);
        Assert.Equal("1/4294967295/4294967295/1/4300000/4294967295/17/4294967295/23/",
            scope.Storage.ToString());
    }

    [Fact]
    public void DuplicateRowsUseRecoveredIdictionaryIndexerOverwriteRatherThanTryAdd()
    {
        using var scope = new RecordScope();
        scope.Storage.Load("2/7/1/7/9/2/11/1/2/3/4/11/5/6/7/8/");
        Assert.Equal(9u, Assert.Single(scope.Storage.Scraps).Value.Amount);
        ItemIs(Assert.Single(scope.Storage.Items).Value, 11, 5, 6, 7, 8);
        Assert.Equal("1/7/9/1/11/5/6/7/8/", scope.Storage.ToString());
    }

    [Fact]
    public void RepeatedLoadReplacesMatchingRowsButRetainsUnmentionedRowsAndEscapedObjects()
    {
        using var scope = new RecordScope();
        scope.Storage.Load("1/7/9/1/11/2/3/4/5/");
        var oldScrap = scope.Storage.Scraps[7];
        var oldItem = scope.Storage.Items[11];
        var untouched = new Storage.Item { Type = 99, F5 = 77 };
        scope.Storage.Items[99] = untouched;
        var items = scope.Storage.Items;
        var scraps = scope.Storage.Scraps;
        scope.Storage.Load("1/7/10/1/11/6/7/8/9/");
        Assert.Same(items, scope.Storage.Items);
        Assert.Same(scraps, scope.Storage.Scraps);
        Assert.NotSame(oldScrap, scope.Storage.Scraps[7]);
        Assert.NotSame(oldItem, scope.Storage.Items[11]);
        Assert.Equal(9u, oldScrap.Amount);
        ItemIs(oldItem, 11, 2, 3, 4, 5);
        ItemIs(scope.Storage.Items[11], 11, 6, 7, 8, 9);
        Assert.Equal(10u, scope.Storage.Scraps[7].Amount);
        Assert.Same(untouched, scope.Storage.Items[99]);
        Assert.Equal(2, scope.Storage.Items.Count);
    }

    [Theory]
    [InlineData("1/", "1/0/0/0/")]
    [InlineData("0/1/", "0/1/0/0/0/0/0/")]
    public void TruncatedRowsStillPublishDeclaredDefaultedRows(string line, string expected)
    {
        using var scope = new RecordScope();
        scope.Storage.Load(line);
        Assert.Equal(expected, scope.Storage.ToString());
    }

    [Fact]
    public void MalformedUnsignedFieldsDefaultIndividuallyAndStillAdvanceTheSlashCursor()
    {
        using var scope = new RecordScope();
        scope.Storage.Load("1/bad/-1/1/11/bad/4294967296/-2/23/");
        Assert.Equal(0u, Assert.Single(scope.Storage.Scraps).Key);
        Assert.Equal(0u, scope.Storage.Scraps[0].Amount);
        ItemIs(Assert.Single(scope.Storage.Items).Value, 11, 0, 0, 0, 23);
        Assert.Equal("1/0/0/1/11/0/0/0/23/", scope.Storage.ToString());
    }

    [Theory]
    [InlineData("-1/1/11/2/3/4/5/", 0, 1)]
    [InlineData("1/7/9/-1/11/2/3/4/5/", 1, 0)]
    public void NegativeCountsSkipOnlyTheirOwnLoopWithoutConsumingDeclaredRows(
        string line, int scraps, int items)
    {
        using var scope = new RecordScope();
        scope.Storage.Load(line);
        Assert.Equal(scraps, scope.Storage.Scraps.Count);
        Assert.Equal(items, scope.Storage.Items.Count);
        if (items != 0) ItemIs(scope.Storage.Items[11], 11, 2, 3, 4, 5);
        if (scraps != 0) Assert.Equal(9u, scope.Storage.Scraps[7].Amount);
    }

    [Fact]
    public void SlashNumericParsingUsesCurrentCulturePositiveSignAndWhitespace()
    {
        using var scope = new RecordScope();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        scope.Storage.Load(" p1 / p7 / p9 / p1 / p11 / p2 / p3 / p4 / p5 /");
        Assert.Equal("1/7/9/1/11/2/3/4/5/", scope.Storage.ToString());
        Assert.Equal(9u, scope.Storage.Scraps[7].Amount);
        ItemIs(scope.Storage.Items[11], 11, 2, 3, 4, 5);
    }

    [Fact]
    public void RecordWritesPhysicalTypesNotKeysAndFreshLoadRekeysThoseValues()
    {
        using var scope = new RecordScope();
        scope.Storage.Scraps[100] = new Storage.Scrap { Type = 7, Amount = 9 };
        scope.Storage.Items[200] = new Storage.Item
        {
            Type = 11, Activate = 2, Amount = 3, RemainingTime = uint.MaxValue, F5 = 5,
        };
        string line = scope.Storage.ToString();
        Assert.Equal("1/7/9/1/11/2/3/4294967295/5/", line);
        var fresh = new Storage(null!);
        fresh.Load(line);
        Assert.False(fresh.Scraps.ContainsKey(100));
        Assert.False(fresh.Items.ContainsKey(200));
        Assert.NotSame(scope.Storage.Scraps[100], fresh.Scraps[7]);
        Assert.NotSame(scope.Storage.Items[200], fresh.Items[11]);
        ItemIs(fresh.Items[11], 11, 2, 3, uint.MaxValue, 5);
    }

    [Fact]
    public void PhysicalTypeCollisionsAreBothSavedThenLastEnumeratedRecordWinsFreshLoad()
    {
        using var scope = new RecordScope();
        scope.Storage.Scraps[100] = new Storage.Scrap { Type = 7, Amount = 1 };
        scope.Storage.Scraps[200] = new Storage.Scrap { Type = 7, Amount = 2 };
        scope.Storage.Items[100] = new Storage.Item { Type = 11, F5 = 3 };
        scope.Storage.Items[200] = new Storage.Item { Type = 11, F5 = 4 };
        var scraps = scope.Storage.Scraps.Values.ToArray();
        var items = scope.Storage.Items.Values.ToArray();
        string expected = $"2/7/{scraps[0].Amount}/7/{scraps[1].Amount}/" +
            $"2/11/0/0/0/{items[0].F5}/11/0/0/0/{items[1].F5}/";
        Assert.Equal(expected, scope.Storage.ToString());
        var fresh = new Storage(null!);
        fresh.Load(expected);
        Assert.Equal(scraps[1].Amount, Assert.Single(fresh.Scraps).Value.Amount);
        Assert.Equal(items[1].F5, Assert.Single(fresh.Items).Value.F5);
        Assert.Equal(2, scope.Storage.Items.Count);
        Assert.Equal(2, scope.Storage.Scraps.Count);
    }

    [Fact]
    public void NullItemsOwnerFaultsOnlyAfterScrapRowsHaveBeenPublished()
    {
        using var scope = new RecordScope();
        scope.Storage.Items = null!;
        Assert.Throws<NullReferenceException>(() => scope.Storage.Load("1/7/9/1/11/2/3/4/5/"));
        Assert.Equal(9u, Assert.Single(scope.Storage.Scraps).Value.Amount);
        Assert.Null(scope.Storage.Items);
    }

    [Fact]
    public void ZeroScrapCountDoesNotTouchNullScrapDictionaryBeforeLoadingItems()
    {
        using var scope = new RecordScope();
        scope.Storage.Scraps = null!;
        scope.Storage.Load("0/1/11/2/3/4/5/");
        ItemIs(Assert.Single(scope.Storage.Items).Value, 11, 2, 3, 4, 5);
        Assert.Throws<NullReferenceException>(() => scope.Storage.Load("1/7/9/0/"));
        ItemIs(scope.Storage.Items[11], 11, 2, 3, 4, 5);
    }

    [Theory]
    [InlineData("scraps")]
    [InlineData("items")]
    [InlineData("scrap-row")]
    [InlineData("item-row")]
    public void SaveHasNoNullOwnerOrNullRowRecovery(string fault)
    {
        using var scope = new RecordScope();
        if (fault == "scraps") scope.Storage.Scraps = null!;
        else if (fault == "items") scope.Storage.Items = null!;
        else if (fault == "scrap-row") scope.Storage.Scraps[7] = null!;
        else scope.Storage.Items[11] = null!;
        Assert.Throws<NullReferenceException>(() => scope.Storage.ToString());
    }
}
