using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeSpellRuneTests
{
    [Fact]
    public void GetterReturnsFreshArraysRetainingActualSpellReferencesAndDuplicateAliases()
    {
        using var scope = new MagicScope();
        var storage = CreateStorage();
        var shared = new LegacyNativeMsgSpell { ID = 1000, Level = 9 };
        var other = new LegacyNativeMsgSpell { ID = 1001 };
        storage.ClientSpells[1] = shared;
        storage.ClientSpells[2] = shared;
        storage.ClientSpells[3] = other;
        scope.Cache[1000] = Levels(true);
        scope.Cache[1001] = Levels(true);

        var first = storage.RuneSpells;
        var second = storage.RuneSpells;

        Assert.NotSame(first, second);
        Assert.Equal(3, first.Length);
        Assert.Equal(2, first.Count(x => ReferenceEquals(shared, x)));
        Assert.Single(first.Where(x => ReferenceEquals(other, x)));
        first[0] = new LegacyNativeMsgSpell();
        Assert.DoesNotContain(second, x => x.ID == 0);
        Assert.Same(shared, storage.ClientSpells[1]);
        shared.Experience = 73;
        Assert.All(second.Where(x => ReferenceEquals(shared, x)), x => Assert.Equal(73, x.Experience));
    }

    [Fact]
    public void MetadataLookupUsesTheMutableSpellIdRatherThanItsDictionaryKey()
    {
        using var scope = new MagicScope();
        var storage = CreateStorage();
        var spell = new LegacyNativeMsgSpell { ID = 1000 };
        storage.ClientSpells[777] = spell;
        scope.Cache[777] = Levels(false);
        scope.Cache[1000] = Levels(true);
        Assert.Same(spell, Assert.Single(storage.RuneSpells));
        spell.ID = 777;
        Assert.Empty(storage.RuneSpells);
        Assert.Same(spell, storage.ClientSpells[777]);
    }

    [Fact]
    public void OnlyMetadataLevelZeroControlsRuneSelectionNotTheLearnedLevelOrRowId()
    {
        using var scope = new MagicScope();
        var storage = CreateStorage();
        var spell = new LegacyNativeMsgSpell { ID = 1000, Level = 9 };
        storage.ClientSpells[1000] = spell;
        scope.Cache[1000] = new Dictionary<ushort, LegacyNativeMagicType.Magic>
        {
            [0] = new() { ID = 2000, isRuneSkill = false },
            [9] = new() { ID = 1000, isRuneSkill = true }
        };
        Assert.Empty(storage.RuneSpells);
        scope.Cache[1000][0].isRuneSkill = true;
        scope.Cache[1000][9].isRuneSkill = false;
        Assert.Same(spell, Assert.Single(storage.RuneSpells));
    }

    [Fact]
    public void UnknownIdsAreSkippedWithoutInsertingMetadataOrRewritingStorage()
    {
        using var scope = new MagicScope();
        var storage = CreateStorage();
        var spell = new LegacyNativeMsgSpell { ID = 1000 };
        storage.ClientSpells[1] = spell;
        Assert.Empty(storage.RuneSpells);
        Assert.Empty(scope.Cache);
        Assert.Same(spell, storage.ClientSpells[1]);
    }

    [Fact]
    public void EachQueryReadsCurrentRowsLevelDictionariesAndTheActualCacheReference()
    {
        using var scope = new MagicScope();
        var storage = CreateStorage();
        var spell = new LegacyNativeMsgSpell { ID = 1000 };
        storage.ClientSpells[1] = spell;
        var levels = Levels(true);
        scope.Cache[1000] = levels;
        var retained = storage.RuneSpells;
        levels[0].isRuneSkill = false;
        Assert.Empty(storage.RuneSpells);
        levels[0] = new LegacyNativeMagicType.Magic { isRuneSkill = true };
        Assert.Same(spell, Assert.Single(storage.RuneSpells));
        scope.Cache[1000] = Levels(false);
        Assert.Empty(storage.RuneSpells);
        LegacyNativePoolMagic.Magic = new LegacyNativeMagicType { [1000] = Levels(true) };
        Assert.Same(spell, Assert.Single(storage.RuneSpells));
        LegacyNativePoolMagic.Magic = new LegacyNativeMagicType();
        Assert.Empty(storage.RuneSpells);
        Assert.Same(spell, Assert.Single(retained));
    }

    [Fact]
    public void KnownIdWithoutLevelZeroThrowsEvenWhenTheLearnedLevelExists()
    {
        using var scope = new MagicScope();
        var storage = CreateStorage();
        storage.ClientSpells[1] = new LegacyNativeMsgSpell { ID = 1000, Level = 9 };
        scope.Cache[1000] = new Dictionary<ushort, LegacyNativeMagicType.Magic>
            { [9] = new() { isRuneSkill = true } };
        Assert.Throws<KeyNotFoundException>(() => storage.RuneSpells);
        Assert.False(scope.Cache[1000].ContainsKey(0));
        Assert.Single(storage.ClientSpells);
    }

    [Fact]
    public void NullRootCacheIsUntouchedAndFaultsOnlyWhenThePredicateRuns()
    {
        using var scope = new MagicScope();
        var storage = CreateStorage();
        LegacyNativePoolMagic.Magic = null!;
        Assert.Empty(storage.RuneSpells); // No spell means no predicate evaluation.
        storage.ClientSpells[1] = new LegacyNativeMsgSpell { ID = 1000 };
        Assert.Throws<NullReferenceException>(() => storage.RuneSpells);
        Assert.Null(LegacyNativePoolMagic.Magic);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NullLevelDictionaryOrLevelZeroRowRetainsItsNativeFault(bool nullRow)
    {
        using var scope = new MagicScope();
        var storage = CreateStorage();
        storage.ClientSpells[1] = new LegacyNativeMsgSpell { ID = 1000 };
        scope.Cache[1000] = nullRow
            ? new Dictionary<ushort, LegacyNativeMagicType.Magic> { [0] = null! }
            : null!;
        Assert.Throws<NullReferenceException>(() => storage.RuneSpells);
        if (nullRow) Assert.Null(scope.Cache[1000][0]);
        else Assert.Null(scope.Cache[1000]);
    }

    [Fact]
    public void NullStoredSpellIsNotFilteredOrRepaired()
    {
        using var scope = new MagicScope();
        var storage = CreateStorage();
        storage.ClientSpells[1] = null!;
        Assert.Throws<NullReferenceException>(() => storage.RuneSpells);
        Assert.Null(storage.ClientSpells[1]);
    }

    private static LegacyNativeSpellStorage CreateStorage() => new(new LegacyNativeGameClientRuntime());

    private static Dictionary<ushort, LegacyNativeMagicType.Magic> Levels(bool rune) =>
        new() { [0] = new LegacyNativeMagicType.Magic { isRuneSkill = rune } };

    private sealed class MagicScope : IDisposable
    {
        private readonly LegacyNativeMagicType? prior = LegacyNativePoolMagic.Magic;
        internal LegacyNativeMagicType Cache { get; } = new();
        internal MagicScope() => LegacyNativePoolMagic.Magic = Cache;
        public void Dispose() => LegacyNativePoolMagic.Magic = prior!;
    }
}
