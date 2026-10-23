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
        Assert.NotSame(scope.Shop.Items, other.Items); Assert.Empty(other.Items);
        Assert.Same(scope.Client, typeof(Shop).GetField("user", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(other));
        ItemIs(new Shop.Item(), 0, 0, 0);
        Assert.Same(scope.Shop, scope.Client.MyExchangeShop); Assert.Equal("0/", scope.Shop.ToString()); scope.Unwritten();
    });

    [Fact]
    public void SlashRecordUsesCurrentCultureAndPersistsEveryValidPhysicalUnsignedField()
    {
        using var state = new State();
        var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone(); culture.NumberFormat.PositiveSign = "p";
        CultureInfo.CurrentCulture = culture;
        Assert.True(state.Shop.TryLoad(" p1 / p4294967295 / p17 / p4294967295 /", out var reason), reason);
        ItemIs(Assert.Single(state.Shop.Items).Value, uint.MaxValue, 17, uint.MaxValue);
        Assert.Equal("1/4294967295/17/4294967295/", state.Shop.ToString());
        Assert.Empty(state.GoodsRows); Assert.Empty(state.ExRows);
    }

    [Fact]
    public void DuplicateInputRecordRejectsWithoutPublishingSuccessfullyParsedPrefix()
    {
        using var state = new State(); var dictionary = state.Shop.Items;
        Assert.False(state.Shop.TryLoad("3/7/17/1/8/19/2/9/17/3/", out var reason));
        Assert.StartsWith("exchangeShop", reason); Assert.Same(dictionary, state.Shop.Items); Assert.Empty(dictionary);
        Assert.Throws<FormatException>(() => state.Shop.Load("3/7/17/1/8/19/2/9/17/3/"));
        Assert.Same(dictionary, state.Shop.Items); Assert.Empty(dictionary);
    }

    [Fact]
    public void RepeatedLoadRejectsExistingCollisionWithoutPublishingNewPrefix()
    {
        using var state = new State(); var old = state.Record(17, 7, 17, 9); var dictionary = state.Shop.Items;
        Assert.False(state.Shop.TryLoad("2/8/19/2/9/17/3/", out var reason));
        Assert.StartsWith("exchangeShop", reason); Assert.Same(dictionary, state.Shop.Items);
        Assert.Same(old, Assert.Single(dictionary).Value); ItemIs(old, 7, 17, 9);
    }

    [Fact]
    public void MalformedAndTruncatedFieldsRejectRatherThanMaterializingDefaultZeroRecords()
    {
        using var state = new State(); var dictionary = state.Shop.Items;
        Assert.False(state.Shop.TryLoad("2/bad/-1/4294967296/", out var reason));
        Assert.StartsWith("exchangeShop", reason); Assert.Same(dictionary, state.Shop.Items); Assert.Empty(dictionary);
    }

    [Fact]
    public void ValidRecordRoundTripPreservesPhysicalOrderAndAppendDoesNotMutateEscapedBook()
    {
        using var state = new State();
        var old = state.Record(17, 7, 17, 9); var oldBook = state.Shop.Items;
        Assert.True(state.Shop.TryLoad("1/8/19/10/", out var reason), reason);
        Assert.NotSame(oldBook, state.Shop.Items); Assert.Same(old, state.Shop.Items[17]);
        Assert.Same(old, Assert.Single(oldBook).Value); ItemIs(old, 7, 17, 9);
        Assert.Equal("2/7/17/9/8/19/10/", state.Shop.ToString());
        var fresh = new Shop(null!);
        Assert.True(fresh.TryLoad(state.Shop.ToString(), out reason), reason);
        Assert.Equal(new uint[] { 17, 19 }, fresh.Items.Keys);
        Assert.Equal(state.Shop.ToString(), fresh.ToString());
        Assert.NotSame(old, fresh.Items[17]);
    }

    [Fact]
    public void NullOrAliasedBookCannotBeSerializedOrSilentlyRepairedByLoad()
    {
        using var state = new State(); var row = state.Record(17, 7, 17, 9);
        state.Shop.Items.Add(101, row);
        Assert.Throws<InvalidDataException>(() => state.Shop.ToString());
        Assert.False(state.Shop.TryLoad("0/", out _)); Assert.Same(row, state.Shop.Items[17]);
        state.Shop.Items = null!;
        Assert.False(state.Shop.TryLoad("1/7/17/9/", out _)); Assert.Null(state.Shop.Items);
        Assert.Throws<InvalidDataException>(() => state.Shop.ToString());
    }
}
