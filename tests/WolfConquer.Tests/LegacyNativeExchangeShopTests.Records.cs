using System.Globalization;
using System.Reflection;
using Shop = WolfConquer.Core.World.LegacyNativeExchangeShopRuntime;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExchangeShopTests
{
    [Fact]
    public Task ConstructorKeepsPhysicalUserAndFreshDictionaryWithoutAutomaticClientBirth() => NativePacketTestThread.Run(() =>
    {
        using var scope = new PacketScope();
        var other = new Shop(scope.Client);
        Assert.NotSame(scope.Shop.Items, other.Items);
        Assert.Empty(scope.Shop.Items);
        Assert.Same(scope.Client, typeof(Shop).GetField("user", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(scope.Shop));
        ItemIs(new Shop.Item(), 0, 0, 0);
        Assert.Null(scope.Client.MyExchangeShop);
        Assert.Equal("0/", scope.Shop.ToString());
        scope.Unwritten();
    });

    [Fact]
    public void InvalidOrNonPositiveCountsNeverClearExistingRowsOrRequireMetadata()
    {
        using var state = new State();
        var row = state.Record(17, 7, 17, 9);
        var dictionary = state.Shop.Items;
        foreach (string? line in new string?[] { null, "", "1", "bad/", "-1/", "0/extra/" })
        {
            state.Shop.Load(line);
            Assert.Same(dictionary, state.Shop.Items);
            Assert.Same(row, Assert.Single(dictionary).Value);
            ItemIs(row, 7, 17, 9);
        }
    }

    [Fact]
    public void SlashRecordUsesCurrentCultureAndPersistsEveryPhysicalUnsignedField()
    {
        using var state = new State();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        state.Shop.Load(" p1 / p4294967295 / p17 / p4294967295 /ignored/");
        ItemIs(Assert.Single(state.Shop.Items).Value, uint.MaxValue, 17, uint.MaxValue);
        Assert.Equal("1/4294967295/17/4294967295/", state.Shop.ToString());
        Assert.Empty(state.GoodsRows);
        Assert.Empty(state.ExRows);
    }

    [Fact]
    public void DuplicateRecordUsesConcreteDictionaryAddAndRetainsSuccessfullyLoadedPrefix()
    {
        using var state = new State();
        Assert.Throws<ArgumentException>(() => state.Shop.Load("3/7/17/1/8/19/2/9/17/3/"));
        Assert.Equal(2, state.Shop.Items.Count);
        ItemIs(state.Shop.Items[17], 7, 17, 1);
        ItemIs(state.Shop.Items[19], 8, 19, 2);
        Assert.Equal("2/7/17/1/8/19/2/", state.Shop.ToString());
    }

    [Fact]
    public void RepeatedLoadKeepsOldEscapedRowsAndPublishesNewPrefixBeforeDuplicateFault()
    {
        using var state = new State();
        var old = state.Record(17, 7, 17, 9);
        var dictionary = state.Shop.Items;
        Assert.Throws<ArgumentException>(() => state.Shop.Load("2/8/19/2/9/17/3/"));
        Assert.Same(dictionary, state.Shop.Items);
        Assert.Same(old, state.Shop.Items[17]);
        ItemIs(old, 7, 17, 9);
        ItemIs(state.Shop.Items[19], 8, 19, 2);
        Assert.Equal(2, dictionary.Count);
    }

    [Fact]
    public void MalformedAndTruncatedFieldsDefaultAndDefaultKeyDuplicateStillFaults()
    {
        using var state = new State();
        Assert.Throws<ArgumentException>(() => state.Shop.Load("2/bad/-1/4294967296/"));
        ItemIs(Assert.Single(state.Shop.Items).Value, 0, 0, 0);
        Assert.Equal("1/0/0/0/", state.Shop.ToString());
    }

    [Fact]
    public void RecordRekeysPhysicalGoodsAndPhysicalCollisionThrowsOnFreshLoad()
    {
        using var state = new State();
        state.Record(100, 7, 17, 9);
        string line = state.Shop.ToString();
        Assert.Equal("1/7/17/9/", line);
        var fresh = new Shop(null!);
        fresh.Load(line);
        Assert.False(fresh.Items.ContainsKey(100));
        ItemIs(fresh.Items[17], 7, 17, 9);
        Assert.NotSame(state.Shop.Items[100], fresh.Items[17]);
        state.Record(200, 8, 17, 10);
        string collision = state.Shop.ToString();
        Assert.Equal("2/7/17/9/8/17/10/", collision);
        var partial = new Shop(null!);
        Assert.Throws<ArgumentException>(() => partial.Load(collision));
        ItemIs(Assert.Single(partial.Items).Value, 7, 17, 9);
        Assert.Equal(2, state.Shop.Items.Count);
    }

    [Fact]
    public void NullDictionaryAndRowAreNotRepairedBySaveOrLoad()
    {
        using var state = new State();
        state.Shop.Items[17] = null!;
        Assert.Throws<NullReferenceException>(() => state.Shop.ToString());
        state.Shop.Items = null!;
        state.Shop.Load("0/"); // No loop means no dictionary access.
        Assert.Throws<NullReferenceException>(() => state.Shop.Load("1/7/17/9/"));
        Assert.Throws<NullReferenceException>(() => state.Shop.ToString());
    }
}
