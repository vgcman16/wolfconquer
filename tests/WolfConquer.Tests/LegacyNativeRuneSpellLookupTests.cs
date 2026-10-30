using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeRuneSpellLookupTests
{
    [Fact]
    public void GetSkillEqualityGatePrecedesMetadataAndKeepsTheItemUnchanged()
    {
        using var scope = new MetadataScope();
        LegacyNativePoolItems.ItemsBase = null;
        var item = new LegacyNativeGameItemRuntime { SpellID = 0 };
        Assert.Null(LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.Equal(0u, item.ITEM_ID);
        Assert.Equal(0u, item.SpellID);
    }

    [Theory]
    [InlineData(1234599u, (ushort)12345, (ushort)99)]
    [InlineData(6553599u, ushort.MaxValue, (ushort)99)]
    public void GetSkillUsesTheLiveRuneMetadataWithoutNarrowing(uint encoded, ushort id, ushort level)
    {
        using var scope = new MetadataScope();
        scope.Items[1_000_000] = new() { ID = 42, RuneSpellID = encoded };
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 1_000_000 };
        var first = Assert.IsType<LegacyNativeMsgSpell>(LegacyNativeRuneItemStorage.GetSkillForRune(item));
        var second = Assert.IsType<LegacyNativeMsgSpell>(LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.NotSame(first, second);
        Assert.Equal(id, first.ID);
        Assert.Equal(level, first.Level);
        Assert.Equal(0, first.Experience);
        Assert.Equal((byte)0, first.SoulLevel);
        Assert.Equal(0u, item.SpellID);
        scope.Items[1_000_000].RuneSpellID = 800123;
        Assert.Equal((ushort)8001, Assert.IsType<LegacyNativeMsgSpell>(LegacyNativeRuneItemStorage.GetSkillForRune(item)).ID);
    }

    [Fact]
    public void ZeroMetadataIsExplicitlyNoSkillRatherThanAPhysicalZeroSpell()
    {
        using var scope = new MetadataScope();
        scope.Items[1_000_000] = new() { ID = 42, RuneSpellID = 0 };
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 1_000_000 };
        Assert.Null(LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.Equal(0u, item.SpellID);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(99u)]
    [InlineData(6553600u)]
    [InlineData(6553700u)]
    [InlineData(uint.MaxValue)]
    public void UnrepresentableMetadataNeverWrapsOntoAnotherNativeSpell(uint encoded)
    {
        using var scope = new MetadataScope();
        var metadata = new LegacyNativeItemRuneMetadata { ID = 42, RuneSpellID = encoded };
        scope.Items[1_000_000] = metadata;
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 1_000_000 };
        Assert.Throws<InvalidOperationException>(() => LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.Equal(encoded, metadata.RuneSpellID);
        Assert.Equal(0u, item.SpellID);
    }

    [Fact]
    public void GetSkillMissingMetadataAndNullArgumentsFailExplicitly()
    {
        using var scope = new MetadataScope();
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 1_000_000 };
        Assert.Throws<InvalidOperationException>(() => LegacyNativeRuneItemStorage.GetSkillForRune(item));
        scope.Items[1_000_000] = null!;
        Assert.Throws<InvalidOperationException>(() => LegacyNativeRuneItemStorage.GetSkillForRune(item));
        LegacyNativePoolItems.ItemsBase = null;
        Assert.Throws<InvalidOperationException>(() => LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.Throws<ArgumentNullException>(() => LegacyNativeRuneItemStorage.GetSkillForRune(null!));
    }

    [Fact]
    public void ReverseLookupUsesFirstLiveRowIdRatherThanTheDictionaryKey()
    {
        using var scope = new MetadataScope();
        var first = new LegacyNativeItemRuneMetadata { ID = uint.MaxValue, RuneSpellID = 1234599 };
        scope.Items[7] = first;
        scope.Items[8] = new() { ID = 33, RuneSpellID = 1234599 };
        var spell = new LegacyNativeMsgSpell { ID = 12345, Level = 99 };
        Assert.Equal(uint.MaxValue, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        first.ID = 42;
        Assert.Equal(42u, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        first.RuneSpellID = 1;
        Assert.Equal(33u, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        spell.Level = 100;
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
    }

    [Fact]
    public void ReverseLookupSupportsMaximumPackedIdentityAndReadsTheCurrentPoolOwner()
    {
        using var scope = new MetadataScope();
        var spell = new LegacyNativeMsgSpell { ID = ushort.MaxValue, Level = 99 };
        scope.Items[9] = new() { ID = 99, RuneSpellID = 6_553_599 };
        Assert.Equal(99u, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        LegacyNativePoolItems.ItemsBase = new() { [10] = new() { ID = 101, RuneSpellID = 6_553_599 } };
        Assert.Equal(101u, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        Assert.Equal(99u, scope.Items[9].ID);
    }

    [Fact]
    public void ReverseLookupRejectsMissingRequiredStateWithoutIncidentalNullTraps()
    {
        using var scope = new MetadataScope();
        Assert.Throws<ArgumentNullException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(null!));
        var spell = new LegacyNativeMsgSpell { ID = 1, Level = 0 };
        Assert.Equal(0u, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        scope.Items[1] = null!;
        Assert.Throws<InvalidOperationException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        scope.Items[1] = new();
        Assert.Throws<ArgumentNullException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(null!));
        LegacyNativePoolItems.ItemsBase = null;
        Assert.Throws<InvalidOperationException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
    }

    [Theory]
    [InlineData((ushort)0, (ushort)0)]
    [InlineData((ushort)1, (ushort)100)]
    [InlineData(ushort.MaxValue, ushort.MaxValue)]
    public void ReverseLookupNeverCarriesAnInvalidLevelIntoAnotherRuneIdentity(ushort id, ushort level)
    {
        using var scope = new MetadataScope();
        scope.Items[9] = new() { ID = 99, RuneSpellID = (uint)id * 100 + level };
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(new() { ID = id, Level = level }));
    }

    [Fact]
    public void ReverseLookupDoesNotAcceptAPartialMatchBeforeANullTail()
    {
        using var scope = new MetadataScope();
        scope.Items[1] = new() { ID = 42, RuneSpellID = 100 };
        scope.Items[2] = null!;
        Assert.Throws<InvalidOperationException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(new() { ID = 1, Level = 0 }));
    }

    private sealed class MetadataScope : IDisposable
    {
        private readonly LegacyNativeItemTypeRuneMetadata? prior = LegacyNativePoolItems.ItemsBase;
        internal LegacyNativeItemTypeRuneMetadata Items { get; } = new();
        internal MetadataScope() => LegacyNativePoolItems.ItemsBase = Items;
        public void Dispose() => LegacyNativePoolItems.ItemsBase = prior;
    }
}
