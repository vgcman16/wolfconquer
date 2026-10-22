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
    [InlineData(0u, (ushort)0, (ushort)0)]
    [InlineData(1234599u, (ushort)12345, (ushort)99)]
    [InlineData(uint.MaxValue, (ushort)23592, (ushort)95)]
    public void GetSkillUsesTheLiveRuneMetadataWithNativeNarrowing(uint encoded, ushort id, ushort level)
    {
        using var scope = new MetadataScope();
        scope.Items[1_000_000] = new() { ID = 42, RuneSpellID = encoded };
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 1_000_000 };
        var first = LegacyNativeRuneItemStorage.GetSkillForRune(item);
        var second = LegacyNativeRuneItemStorage.GetSkillForRune(item);
        Assert.NotSame(first, second);
        Assert.Equal(id, first.ID);
        Assert.Equal(level, first.Level);
        Assert.Equal(0, first.Experience);
        Assert.Equal((byte)0, first.SoulLevel);
        Assert.Equal(0u, item.SpellID);
        scope.Items[1_000_000].RuneSpellID = 800123;
        Assert.Equal((ushort)8001, LegacyNativeRuneItemStorage.GetSkillForRune(item).ID);
    }

    [Fact]
    public void GetSkillMissingMetadataAndNullArgumentsKeepNativeFailures()
    {
        using var scope = new MetadataScope();
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = 1_000_000 };
        Assert.Throws<KeyNotFoundException>(() => LegacyNativeRuneItemStorage.GetSkillForRune(item));
        scope.Items[1_000_000] = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.Throws<NullReferenceException>(() => LegacyNativeRuneItemStorage.GetSkillForRune(null!));
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
        Assert.Equal(0u, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
    }

    [Fact]
    public void ReverseLookupRetainsFullUshortLevelAndReReadsThePoolOwner()
    {
        using var scope = new MetadataScope();
        var spell = new LegacyNativeMsgSpell { ID = ushort.MaxValue, Level = ushort.MaxValue };
        scope.Items[9] = new() { ID = 99, RuneSpellID = 6_619_035 };
        Assert.Equal(99u, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        LegacyNativePoolItems.ItemsBase = new() { [10] = new() { ID = 101, RuneSpellID = 6_619_035 } };
        Assert.Equal(101u, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        Assert.Equal(99u, scope.Items[9].ID);
    }

    [Fact]
    public void ReverseLookupEmptyTableDoesNotDereferenceSkillButNullRowsAndOwnersFail()
    {
        using var scope = new MetadataScope();
        Assert.Equal(0u, LegacyNativeRuneItemStorage.GetRuneFromSkill(null!));
        scope.Items[1] = null!;
        Assert.Throws<NullReferenceException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(new()));
        scope.Items[1] = new();
        Assert.Throws<NullReferenceException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(null!));
        LegacyNativePoolItems.ItemsBase = null;
        Assert.Throws<NullReferenceException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(new()));
    }

    private sealed class MetadataScope : IDisposable
    {
        private readonly LegacyNativeItemTypeRuneMetadata? prior = LegacyNativePoolItems.ItemsBase;
        internal LegacyNativeItemTypeRuneMetadata Items { get; } = new();
        internal MetadataScope() => LegacyNativePoolItems.ItemsBase = Items;
        public void Dispose() => LegacyNativePoolItems.ItemsBase = prior;
    }
}
