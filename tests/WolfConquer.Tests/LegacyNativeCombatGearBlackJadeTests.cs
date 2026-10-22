using System.Collections.Concurrent;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;
using JadeTable = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeCombatGearBlackJadeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddUsesTheCacheKeyAndCallerExpAndPublishesBeforeTheWireLoadGate(bool loaded)
    {
        using var f = new NativeCombatGearSupportFixture(loaded);
        var metadata = new JadeTable.CQ { ID = 999, TypeRune = 7, Exp = 123 };
        f.Metadata[41] = metadata;
        var bag = f.Gear.BlackJadeBag;

        var packet = f.Gear.AddBlackJade(41, uint.MaxValue);

        Assert.Same(bag, f.Gear.BlackJadeBag);
        var entry = Assert.Single(bag);
        Assert.Equal(41u, entry.Key);
        Assert.Equal(41u, entry.Value.Type);
        Assert.Equal(uint.MaxValue, entry.Value.Data);
        Assert.Same(metadata, entry.Value.DBItem);
        Assert.Same(f.Metadata, JadeTable.Info);
        metadata.TypeRune = 9;
        Assert.Equal(9u, entry.Value.DBItem.TypeRune);
        if (loaded) Assert.Equal(NativeCombatGearSupportFixture.Frame(7, item: 41), packet);
        else Assert.Null(packet);
        f.AssertContinuation();
    }

    [Fact]
    public void ExistingDifferentIdWithSameTypeRuneDeduplicatesByValueNotBagKey()
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Metadata[41] = new JadeTable.CQ { TypeRune = 7 };
        f.Metadata[42] = new JadeTable.CQ { TypeRune = 7 };
        var retained = new Gear.BlackJade(41, 17);
        f.Gear.BlackJadeBag[999] = retained;

        Assert.Null(f.Gear.AddBlackJade(42, 99));

        Assert.Same(retained, Assert.Single(f.Gear.BlackJadeBag).Value);
        Assert.Equal(41u, retained.Type);
        Assert.Equal(17u, retained.Data);
        Assert.False(f.Gear.BlackJadeBag.ContainsKey(42));
    }

    [Fact]
    public void IgnoredTryAddFailureStillReturnsTheAddPacketAndLeavesTheExistingAliasUnchanged()
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Metadata[41] = new JadeTable.CQ { TypeRune = 7 };
        f.Metadata[42] = new JadeTable.CQ { TypeRune = 8 };
        var retained = new Gear.BlackJade(42, 17);
        f.Gear.BlackJadeBag[41] = retained; // Bag key and the physical jade Type deliberately differ.

        var packet = f.Gear.AddBlackJade(41, 99);

        Assert.Equal(NativeCombatGearSupportFixture.Frame(7, item: 41), packet);
        Assert.Same(retained, Assert.Single(f.Gear.BlackJadeBag).Value);
        Assert.Equal(42u, retained.Type);
        Assert.Equal(17u, retained.Data);
    }

    [Fact]
    public void DedupUsesTheCurrentCacheOwnerAndLiveMutableTypeRuneRows()
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Metadata[41] = new JadeTable.CQ { TypeRune = 7 };
        f.Metadata[42] = new JadeTable.CQ { TypeRune = 8 };
        var retained = new Gear.BlackJade(41, 17);
        f.Gear.BlackJadeBag[999] = retained;
        var current = new Dictionary<uint, JadeTable.CQ>
        {
            [41] = new() { TypeRune = 9 }, [42] = new() { TypeRune = 9 }
        };
        JadeTable.Info = current;

        Assert.Null(f.Gear.AddBlackJade(42, 99));
        Assert.Same(current[41], retained.DBItem);
        current[41].TypeRune = 10;
        var packet = f.Gear.AddBlackJade(42, 99);

        Assert.Equal(NativeCombatGearSupportFixture.Frame(7, item: 42), packet);
        Assert.Same(current, JadeTable.Info);
        Assert.Same(retained, f.Gear.BlackJadeBag[999]);
        Assert.Same(current[42], f.Gear.BlackJadeBag[42].DBItem);
        Assert.Equal(7u, f.Metadata[41].TypeRune);
        Assert.Equal(8u, f.Metadata[42].TypeRune);
    }

    [Fact]
    public void MissingExistingMetadataUsesFreshZeroFallbackRowsForTypeRuneDedupWithoutCachingThem()
    {
        using var f = new NativeCombatGearSupportFixture();
        var candidate = new JadeTable.CQ { TypeRune = 0 };
        f.Metadata[41] = candidate;
        var retained = new Gear.BlackJade(123, 17);
        f.Gear.BlackJadeBag[999] = retained;
        Assert.NotSame(retained.DBItem, retained.DBItem);

        Assert.Null(f.Gear.AddBlackJade(41, 99));
        Assert.False(f.Metadata.ContainsKey(123));
        candidate.TypeRune = 1;
        var packet = f.Gear.AddBlackJade(41, 99);

        Assert.Equal(NativeCombatGearSupportFixture.Frame(7, item: 41), packet);
        Assert.Same(retained, f.Gear.BlackJadeBag[999]);
        Assert.Equal(2, f.Gear.BlackJadeBag.Count);
        Assert.False(f.Metadata.ContainsKey(123));
    }

    [Fact]
    public void MissingCandidateMetadataReturnsBeforeReadingTheNullBag()
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Gear.BlackJadeBag = null!;

        Assert.Null(f.Gear.AddBlackJade(41, 99));

        Assert.Null(f.Gear.BlackJadeBag);
        Assert.Empty(f.Metadata);
    }

    [Fact]
    public void CachedNullCandidateIsNotDereferencedWhenTheActualBagIsEmpty()
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Metadata[41] = null!;

        var packet = f.Gear.AddBlackJade(41, 99);

        Assert.Equal(NativeCombatGearSupportFixture.Frame(7, item: 41), packet);
        var jade = Assert.Single(f.Gear.BlackJadeBag).Value;
        Assert.Equal(41u, jade.Type);
        Assert.Equal(99u, jade.Data);
        Assert.Null(jade.DBItem);
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("existing-metadata")]
    [InlineData("bag-value")]
    public void NullsInsideTheLiveDedupPredicateFaultWithoutAddingOrRepairing(string kind)
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Metadata[41] = kind == "candidate" ? null! : new JadeTable.CQ { TypeRune = 7 };
        f.Metadata[42] = kind == "existing-metadata" ? null! : new JadeTable.CQ { TypeRune = 8 };
        var retained = kind == "bag-value" ? null! : new Gear.BlackJade(42, 17);
        f.Gear.BlackJadeBag[999] = retained;

        Assert.Throws<NullReferenceException>(() => f.Gear.AddBlackJade(41, 99));

        Assert.Same(retained, Assert.Single(f.Gear.BlackJadeBag).Value);
        Assert.False(f.Gear.BlackJadeBag.ContainsKey(41));
        if (kind == "candidate") Assert.Null(f.Metadata[41]);
        if (kind == "existing-metadata") Assert.Null(f.Metadata[42]);
    }

    [Fact]
    public void NullCacheFaultIsNotHiddenByUnloadedStateOrNullBag()
    {
        using var f = new NativeCombatGearSupportFixture(false);
        JadeTable.Info = null!;
        f.Gear.BlackJadeBag = null!;

        Assert.Throws<NullReferenceException>(() => f.Gear.AddBlackJade(41, 99));

        Assert.Null(JadeTable.Info);
        Assert.Null(f.Gear.BlackJadeBag);
    }

    [Fact]
    public void PresentCandidateAndNullBagFaultWithoutAllocatingAReplacement()
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Metadata[41] = new JadeTable.CQ { TypeRune = 7 };
        f.Gear.BlackJadeBag = null!;

        Assert.Throws<NullReferenceException>(() => f.Gear.AddBlackJade(41, 99));

        Assert.Null(f.Gear.BlackJadeBag);
        Assert.Single(f.Metadata);
    }

    [Fact]
    public void AddUsesTheCurrentBagOwnerRatherThanACachedOriginalDictionary()
    {
        using var f = new NativeCombatGearSupportFixture();
        f.Metadata[41] = new JadeTable.CQ { TypeRune = 7 };
        f.Metadata[42] = new JadeTable.CQ { TypeRune = 7 };
        var old = f.Gear.BlackJadeBag;
        var retained = new Gear.BlackJade(41, 17);
        old[999] = retained;
        var current = new ConcurrentDictionary<uint, Gear.BlackJade>();
        f.Gear.BlackJadeBag = current;

        var packet = f.Gear.AddBlackJade(42, 99);

        Assert.Equal(NativeCombatGearSupportFixture.Frame(7, item: 42), packet);
        Assert.Same(current, f.Gear.BlackJadeBag);
        Assert.Equal(42u, Assert.Single(current).Key);
        Assert.Same(retained, Assert.Single(old).Value);
        Assert.Equal(17u, retained.Data);
    }
}
