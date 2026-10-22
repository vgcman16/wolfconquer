using System.Collections.Concurrent;
using System.Reflection;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using GearTable = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using CollectionTable = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatGearCollectionTests
{
    [Fact]
    public void CollectionRowsKeepAllSevenMutableNativeFieldsAndTheirZeroDefaults()
    {
        var fields = typeof(CollectionTable.CQ).GetFields(BindingFlags.Instance | BindingFlags.Public);
        Assert.Equal(new[] { "Id", "Type", "Min", "Mix", "attr_type1", "attr_value1", "value2" },
            fields.OrderBy(field => field.MetadataToken).Select(field => field.Name));
        var row = new CollectionTable.CQ();
        foreach (var field in fields)
        {
            Assert.False(field.IsInitOnly);
            Assert.Equal(field.Name == "attr_type1" ? typeof(JadeTable.AttrType) : typeof(uint), field.FieldType);
            Assert.Equal(0u, Convert.ToUInt32(field.GetValue(row)));
        }
    }

    [Fact]
    public void LoadedEmptyStateMatchesZeroRangesForEveryFamilyAndReturnsFreshLists()
    {
        using var f = new Scope();
        f.User.MasteryPoints = f.User.TaoistReiki = f.User.PirateExp = uint.MaxValue;
        var rows = Enumerable.Range(700, 4).Select(i => Scope.Book((uint)i, (uint)i, 0, 0)).ToArray();
        var items = f.User.Items;
        var bag = f.User.BlackJadeBag;

        var first = f.User.GetNutBook();
        var second = f.User.GetNutBook();

        Assert.Equal(rows, first);
        Assert.Equal(rows, second);
        Assert.NotSame(first, second);
        first.Clear();
        Assert.Equal(4, second.Count);
        Assert.Equal(4, CollectionTable.Info.Count);
        Assert.Same(items, f.User.Items);
        Assert.Same(bag, f.User.BlackJadeBag);
        Assert.Empty(items);
        Assert.Empty(bag);
        Assert.Equal(uint.MaxValue, f.User.MasteryPoints);
        Assert.Equal(uint.MaxValue, f.User.TaoistReiki);
        Assert.Equal(uint.MaxValue, f.User.PirateExp);
    }

    [Fact]
    public void AllFamiliesUseDictionaryKeysAndLiveMetadataThenKeepEveryOverlappingRowInFamilyOrder()
    {
        using var f = new Scope();
        var weapons = new[] { f.Weapon(701, 201, 3, 10, 1), f.Weapon(702, 202, 3, 20, 2), f.Weapon(703, 203, 3, 30, 3) };
        f.User.Items[(Gear.TypeID)700] = null!; // These entries must not be read as a weapon family.
        f.User.Items[(Gear.TypeID)704] = null!;
        foreach (uint family in new uint[] { 701, 702, 703 })
        {
            Scope.JadeRow(family + 100, family, family - 697);
            f.Jade(family + 200, family + 100);
        }
        foreach (var pair in new[] { (Id: 31u, Rune: 7u, Score: 7u), (Id: 32u, Rune: 107u, Score: 8u), (Id: 33u, Rune: 207u, Score: 9u) })
        {
            Scope.JadeRow(pair.Id, 0, pair.Score, pair.Rune);
            f.Jade(pair.Id + 2000, pair.Id);
        }
        Scope.JadeRow(34, 0, 1000, 8);
        Scope.JadeRow(35, 700, 1000, 7); // Family 700 is NOT the native rune-7 condition.
        f.Jade(2034, 34);
        f.Jade(2035, 35);
        var last = Scope.Book(1, 703, 39, 39);
        var shared = Scope.Book(2, 701, 15, 20);
        var first = Scope.Book(3, 700, 24, 24);
        var middle = Scope.Book(4, 702, 27, 27);
        var exact = Scope.Book(5, 701, 15, 15);
        Scope.Book(6, 704, 0, uint.MaxValue);
        var overlap = Scope.Book(7, 700, 0, 24);
        CollectionTable.Info[8] = shared; // No row-ID or reference deduplication.
        var items = f.User.Items;
        var bag = f.User.BlackJadeBag;
        var bagBefore = bag.ToArray();

        var result = f.User.GetNutBook();

        Assert.Equal(new[] { first, overlap, shared, exact, shared, middle, last }, result);
        Assert.Same(shared, result[2]);
        Assert.Same(result[2], result[4]);
        Assert.Equal(41u, shared.attr_value1);
        result[2].attr_value1 = 99;
        Assert.Equal(99u, CollectionTable.Info[2].attr_value1); // Returned rows are aliases, not DTOs.
        Assert.Same(items, f.User.Items);
        Assert.Same(bag, f.User.BlackJadeBag);
        Assert.Equal(5, items.Count);
        Assert.Equal(bagBefore.Length, bag.Count);
        foreach (var pair in bagBefore) Assert.Same(pair.Value, bag[pair.Key]);
        Assert.Equal(new uint[] { 201, 202, 203 }, weapons.Select(item => (uint)item.ItemID));
        Assert.Equal(new uint[] { 1, 2, 3 }, weapons.Select(item => item.MasteryScore));
        Assert.All(bag.Values, item => Assert.Equal(77u, item.Data));
    }

    [Fact]
    public void WeaponLookupRereadsCurrentValueIdentityLevelRowsAndWholeDictionaryOwners()
    {
        using var f = new Scope();
        var weapon = f.Weapon(701, 201, 3, 10, 2);
        var book = Scope.Book(1, 701, 12, 12);
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
        weapon.ItemID = (Gear.TypeID)202;
        weapon.Level = 4;
        Scope.GearRow(202, 4, 20);
        book.Min = book.Mix = 22;
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
        Scope.GearRow(202, 4, 30);
        book.Min = book.Mix = 32;
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
        GearTable.Info = new() { [202] = new() { [4] = new() { Score = 40 } } };
        book.Min = book.Mix = 42;
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
        var replacement = new Gear.Weapon { ItemID = (Gear.TypeID)303, Level = 5, MasteryScore = 3 };
        Scope.GearRow(303, 5, 50);
        f.User.Items = new ConcurrentDictionary<Gear.TypeID, Gear.Weapon>();
        f.User.Items[(Gear.TypeID)701] = replacement;
        book.Min = book.Mix = 53;
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
        Assert.Equal(2u, weapon.MasteryScore);
        Assert.Same(replacement, f.User.Items[(Gear.TypeID)701]);
    }

    [Fact]
    public void BagUsesValueTypeNotBagKeyAndRereadsMetadataFamilyScoreAndCacheOwners()
    {
        using var f = new Scope();
        var metadata = Scope.JadeRow(113, 701, 5);
        var jade = f.Jade(999, 113);
        var book = Scope.Book(1, 701, 5, 5);
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
        metadata.TypeCombatGear = book.Type = 702;
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
        jade.Type = 213;
        Scope.JadeRow(213, 701, 9);
        book.Type = 701;
        book.Min = book.Mix = 9;
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
        JadeTable.Info = new() { [213] = new() { TypeCombatGear = 701, score = 11 } };
        book.Min = book.Mix = 11;
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
        Scope.JadeRow(313, 0, 13, 107);
        f.User.BlackJadeBag = new ConcurrentDictionary<uint, Gear.BlackJade>();
        f.Jade(777, 313);
        CollectionTable.Info = new();
        var current = Scope.Book(2, 700, 13, 13);
        Assert.Same(current, Assert.Single(f.User.GetNutBook()));
        Assert.Equal(213u, jade.Type);
        Assert.Equal(77u, jade.Data);
    }

    [Fact]
    public void MinAndMixAreInclusiveAndInvalidOrOutsideRangesDoNotMatch()
    {
        using var f = new Scope();
        f.Weapon(701, 201, 1, 10);
        var exact = Scope.Book(1, 701, 10, 10);
        var upper = Scope.Book(2, 701, 0, 10);
        var lower = Scope.Book(3, 701, 10, uint.MaxValue);
        Scope.Book(4, 701, 11, uint.MaxValue);
        Scope.Book(5, 701, 0, 9);
        Scope.Book(6, 701, 11, 9);
        Assert.Equal(new[] { exact, upper, lower }, f.User.GetNutBook());
    }

    [Theory]
    [InlineData(0u, 0u, 0u, 0)]
    [InlineData(4294967295u, 1u, 0u, 0)]
    [InlineData(4294967295u, 2u, 0u, 1)]
    [InlineData(4294967295u, 0u, 0u, -1)]
    [InlineData(2147483647u, 1u, 0u, -2147483648)]
    [InlineData(2147483647u, 0u, 2u, -2147483647)]
    [InlineData(2147483647u, 0u, 2147483649u, 0)]
    [InlineData(0u, 0u, 2147483648u, -2147483648)]
    [InlineData(0u, 0u, 4294967295u, -1)]
    public void ScoresWrapAsUIntThenIntAndNegativeTotalsDoNotMatchUIntRanges(uint score, uint mastery, uint jadeScore, int expected)
    {
        using var f = new Scope();
        var weapon = f.Weapon(701, 201, 1, score, mastery);
        var metadata = Scope.JadeRow(113, 701, jadeScore);
        f.Jade(999, 113);
        uint nonnegative = expected < 0 ? 0u : (uint)expected;
        var exact = Scope.Book(1, 701, nonnegative, nonnegative);
        var broad = Scope.Book(2, 701, 0, uint.MaxValue);
        if (expected < 0) Assert.Empty(f.User.GetNutBook());
        else Assert.Equal(new[] { exact, broad }, f.User.GetNutBook());
        Assert.Equal(score, weapon.DBItem.Score);
        Assert.Equal(mastery, weapon.MasteryScore);
        Assert.Equal(jadeScore, metadata.score);
    }

    [Theory]
    [InlineData(700u)]
    [InlineData(701u)]
    [InlineData(702u)]
    [InlineData(703u)]
    public void TwoSignedSignBitBagScoresWrapBackToZeroForEveryFamily(uint family)
    {
        using var f = new Scope();
        Scope.JadeRow(113, family == 700 ? 0 : family, 2147483648, 7);
        Scope.JadeRow(213, family == 700 ? 0 : family, 2147483648, 107);
        f.Jade(1, 113);
        f.Jade(2, 213);
        var book = Scope.Book(1, family, 0, 0);
        Assert.Same(book, Assert.Single(f.User.GetNutBook()));
    }

    [Fact]
    public void MissingWeaponMetadataStillCountsMasteryWhileMissingJadeMetadataIsExcluded()
    {
        using var f = new Scope();
        var weapon = new Gear.Weapon { ItemID = (Gear.TypeID)999, Level = 4, MasteryScore = 7 };
        f.User.Items[(Gear.TypeID)701] = weapon;
        f.Jade(123, 113);
        var rows = new[] { Scope.Book(1, 700, 0, 0), Scope.Book(2, 701, 7, 7),
            Scope.Book(3, 702, 0, 0), Scope.Book(4, 703, 0, 0) };
        Assert.Equal(rows, f.User.GetNutBook());
        Assert.Empty(GearTable.Info);
        Assert.Empty(JadeTable.Info);
        Assert.Same(weapon, f.User.Items[(Gear.TypeID)701]);
        Assert.Equal(7u, weapon.MasteryScore);
    }

    [Fact]
    public void FalseLoadGateReturnsNullBeforeAnyOwnerOrCacheAccessWithoutRepair()
    {
        using var f = new Scope();
        GearTable.m_Load = false;
        GearTable.Info = null!;
        JadeTable.Info = null!;
        CollectionTable.Info = null!;
        f.User.Items = null!;
        f.User.BlackJadeBag = null!;
        Assert.Null(f.User.GetNutBook());
        Assert.Null(f.User.Items);
        Assert.Null(f.User.BlackJadeBag);
        Assert.Null(GearTable.Info);
        Assert.Null(JadeTable.Info);
        Assert.Null(CollectionTable.Info);
        Assert.False(GearTable.m_Load);
    }

    [Theory]
    [InlineData("bag")]
    [InlineData("bag-value")]
    [InlineData("jade-cache")]
    [InlineData("jade-row")]
    [InlineData("items")]
    [InlineData("gear-cache")]
    [InlineData("gear-levels")]
    [InlineData("gear-row")]
    [InlineData("collection-cache")]
    public void LoadedNullInputsFaultWithoutRepairingAnyCacheOrOwner(string fault)
    {
        using var f = new Scope();
        switch (fault)
        {
            case "bag": f.User.BlackJadeBag = null!; f.User.Items = null!; break;
            case "bag-value": f.User.BlackJadeBag[1] = null!; break;
            case "jade-cache": f.Jade(1, 113); JadeTable.Info = null!; break;
            case "jade-row": f.Jade(1, 113); JadeTable.Info[113] = null!; break;
            case "items": f.User.Items = null!; break;
            case "gear-cache": f.Weapon(701, 201, 1, 10); GearTable.Info = null!; break;
            case "gear-levels": f.Weapon(701, 201, 1, 10); GearTable.Info[201] = null!; break;
            case "gear-row": f.Weapon(701, 201, 1, 10); GearTable.Info[201][1] = null!; break;
            case "collection-cache": CollectionTable.Info = null!; break;
            default: throw new InvalidOperationException("Unknown test prestate.");
        }
        var items = f.User.Items;
        var bag = f.User.BlackJadeBag;
        var gear = GearTable.Info;
        var jade = JadeTable.Info;
        var books = CollectionTable.Info;
        Assert.Throws<NullReferenceException>(() => f.User.GetNutBook());
        Assert.Same(items, f.User.Items);
        Assert.Same(bag, f.User.BlackJadeBag);
        Assert.Same(gear, GearTable.Info);
        Assert.Same(jade, JadeTable.Info);
        Assert.Same(books, CollectionTable.Info);
        Assert.True(GearTable.m_Load);
    }

    [Fact]
    public void NullCollectionRowAfterAnEarlierMatchDoesNotPublishAPartialResultOrMutateMetadata()
    {
        using var f = new Scope();
        var match = Scope.Book(1, 700, 0, 0);
        CollectionTable.Info[2] = null!;
        var sentinel = new List<CollectionTable.CQ> { new() { Id = 987 } };
        var result = sentinel;
        Assert.Throws<NullReferenceException>(() => result = f.User.GetNutBook());
        Assert.Same(sentinel, result);
        Assert.Equal(987u, Assert.Single(result).Id);
        Assert.Same(match, CollectionTable.Info[1]);
        Assert.Null(CollectionTable.Info[2]);
        Assert.Equal(41u, match.attr_value1);
        Assert.Empty(f.User.Items);
        Assert.Empty(f.User.BlackJadeBag);
    }
}
