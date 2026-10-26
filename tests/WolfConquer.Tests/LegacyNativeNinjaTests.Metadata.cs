using System.Reflection;
using WolfConquer.Core.World;
using ItemType = WolfConquer.Core.World.LegacyNativeNinja.ItemType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaTests
{
    [Fact]
    public void ActualItemAndBothMetadataRowTypesKeepCompleteMutableNativeFieldShapes()
    {
        RowFields<LegacyNativeNinja.Item>("ItemID Level Position Exp UK");
        RowFields<LegacyNativeNinjaFile.NinjaSprintItem>("ID ItemID Level Exp UK1 UK2 magic1 magic2 Power Damage Score UK5");
        RowFields<LegacyNativeNinjaFile.Item>("Level U1 MaxHP PAttack PDefense MDefense Chance Seconds Rate MovementSpeed U13 U14 P");
        Assert.Null(typeof(LegacyNativeNinja.Item).GetProperty("DBItem")!.SetMethod);
        Assert.Equal(typeof(Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>),
            typeof(LegacyNativeNinjaFile).GetProperty("gouyu_type")!.PropertyType);
        Assert.Equal(typeof(Dictionary<uint, LegacyNativeNinjaFile.Item>),
            typeof(LegacyNativeNinjaFile).GetProperty("gouyu_immortals")!.PropertyType);
        foreach (var name in new[] { "gouyu_type", "gouyu_immortals" })
        {
            var property = typeof(LegacyNativeNinjaFile).GetProperty(name)!;
            Assert.True(property.GetMethod!.IsPublic);
            Assert.True(property.SetMethod!.IsPublic);
        }
    }

    [Fact]
    public void DbItemMatchesBothValueFieldsNotDictionaryKeyAndReturnsFirstExactReference()
    {
        using var tables = new Tables();
        var item = new LegacyNativeNinja.Item { ItemID = 100, Level = 7 };
        var wrongLevel = new LegacyNativeNinjaFile.NinjaSprintItem { ItemID = 100, Level = 6 };
        var wrongId = new LegacyNativeNinjaFile.NinjaSprintItem { ItemID = 200, Level = 7 };
        var first = new LegacyNativeNinjaFile.NinjaSprintItem { ItemID = 100, Level = 7, Power = -4, Score = 90 };
        var second = new LegacyNativeNinjaFile.NinjaSprintItem { ItemID = 100, Level = 7, Power = 8, Score = 91 };
        LegacyNativeNinjaFile.gouyu_type = new() { [100] = wrongLevel, [7] = wrongId, [900] = first, [1] = second };

        Assert.Same(first, item.DBItem);
        first.UK5 = 123;
        Assert.Equal(123u, item.DBItem.UK5);
        Assert.Equal(-4, item.DBItem.Power);
        first.Level = 8;
        Assert.Same(second, item.DBItem);
        item.Level = 8;
        Assert.Same(first, item.DBItem);
        item.ItemID = 200;
        item.Level = 7;
        Assert.Same(wrongId, item.DBItem);
        var replacement = new LegacyNativeNinjaFile.NinjaSprintItem { ItemID = 200, Level = 7, Score = 999 };
        LegacyNativeNinjaFile.gouyu_type = new() { [uint.MaxValue] = replacement };
        Assert.Same(replacement, item.DBItem); // Table reference and item fields are read anew, not cached.
    }

    [Fact]
    public void MissingDbItemReturnsFreshZeroRowsWithoutInsertingOrCachingThem()
    {
        using var tables = new Tables();
        var item = new LegacyNativeNinja.Item { ItemID = 100, Level = 7 };
        var first = item.DBItem;
        first.Level = 123;
        first.Score = 456;
        var second = item.DBItem;
        Assert.NotSame(first, second);
        foreach (var field in typeof(LegacyNativeNinjaFile.NinjaSprintItem).GetFields(BindingFlags.Public | BindingFlags.Instance))
            Assert.Equal(0L, Convert.ToInt64(field.GetValue(second)));
        Assert.Empty(LegacyNativeNinjaFile.gouyu_type);
        Assert.Empty(LegacyNativeNinjaFile.gouyu_immortals);
    }

    [Fact]
    public void DbItemPreservesNullTableAndEarlyNullRowFaultsButStopsBeforeLaterNullRows()
    {
        using var tables = new Tables();
        var item = new LegacyNativeNinja.Item { ItemID = 100, Level = 7 };
        LegacyNativeNinjaFile.gouyu_type = null!;
        Assert.Throws<NullReferenceException>(() => item.DBItem);
        Assert.Null(LegacyNativeNinjaFile.gouyu_type);
        var match = new LegacyNativeNinjaFile.NinjaSprintItem { ItemID = 100, Level = 7 };
        LegacyNativeNinjaFile.gouyu_type = new() { [1] = null!, [2] = match };
        Assert.Throws<NullReferenceException>(() => item.DBItem);
        LegacyNativeNinjaFile.gouyu_type = new() { [1] = match, [2] = null! };
        Assert.Same(match, item.DBItem);
        match.Level = 8;
        Assert.Throws<NullReferenceException>(() => item.DBItem); // Level must also match before the early return.
    }

    [Fact]
    public void SimpleLookupIgnoresClassUserPositionAndDictionaryKeysAndKeepsFirstEnumerationMatch()
    {
        var ninja = new LegacyNativeNinja(); // user remains null; no class/equipment access.
        Put(ninja, 71, 0);
        Put(ninja, 72, 999);
        Put(ninja, 73, uint.MaxValue);
        var expected = ninja.Items.Values.First();
        Assert.True(ninja.TryGetValue(100, out var result));
        Assert.Same(expected, result);
        result.Exp = 123;
        Assert.Equal(123u, ninja.Items.Single(pair => ReferenceEquals(pair.Value, expected)).Value.Exp);
        result.ItemID = 200;
        var next = ninja.Items.Values.First(row => row.ItemID == 100);
        Assert.True(ninja.TryGetValue(100, out result));
        Assert.Same(next, result);
        Assert.False(ninja.TryGetValue(999, out result));
        Assert.Null(result);
        Assert.Null(ninja.user);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SimpleQueryFaultRetainsPriorOutWhileEquippedQueryClearsItFirst(bool nullRow)
    {
        var ninja = new LegacyNativeNinja(Client());
        if (nullRow) Assert.True(ninja.Items.TryAdd(1, null!));
        else ninja.Items = null!;
        var sentinel = new LegacyNativeNinja.Item();
        var result = sentinel;
        Assert.Throws<NullReferenceException>(() => ninja.TryGetValue(100, out result));
        Assert.Same(sentinel, result);
        Assert.Throws<NullReferenceException>(() => ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Null(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EquippedQueryClearsOutBeforeNullCurrentUserOrPlayerDereference(bool nullPlayer)
    {
        var ninja = new LegacyNativeNinja();
        if (nullPlayer) { ninja.user = Client(); ninja.user.Player = null!; }
        LegacyNativeNinja.Item result = new();
        Assert.Throws<NullReferenceException>(() => ninja.TryGetValueEquip(ItemType.WildSigil, out result));
        Assert.Null(result);
        if (nullPlayer) Assert.Null(ninja.user.Player);
        else Assert.Null(ninja.user);
    }

    [Fact]
    public void ALaterSlotFaultKeepsTheAlreadyPublishedEarlierSlotReference()
    {
        var ninja = new LegacyNativeNinja(Client());
        Put(ninja, 11, 1);
        Put(ninja, 22, 2);
        var keys = ninja.Items.ToArray().Select(pair => pair.Key).ToArray();
        var first = new LegacyNativeNinja.Item { ItemID = 100, Position = 1 };
        ninja.Items[keys[0]] = first;
        ninja.Items[keys[1]] = null!;
        Assert.Same(first, ninja.Items.Values.First()); // Explicit enumeration prestate, no insertion-order assumption.
        LegacyNativeNinja.Item result = new();

        Assert.Throws<NullReferenceException>(() => ninja.TryGetValueEquip(ItemType.WildSigil, out result));

        Assert.Same(first, result); // Slot1 publishes before slot2 predicate reaches the null row.
        Assert.True(ninja.TryGetValue(100, out result)); // Simple lookup stops at the first matching row.
        Assert.Same(first, result);
    }

    private static void RowFields<T>(string names) where T : new()
    {
        var row = new T();
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(names.Split(' '), fields.Select(field => field.Name));
        foreach (var field in fields)
        {
            Assert.Equal(field.Name == "Power" ? typeof(int) : typeof(uint), field.FieldType);
            Assert.False(field.IsInitOnly);
            Assert.Equal(0L, Convert.ToInt64(field.GetValue(row)));
            object value = field.Name == "Power" ? (object)(-4) : 77u;
            field.SetValue(row, value);
            Assert.Equal(value, field.GetValue(row));
        }
    }
}
