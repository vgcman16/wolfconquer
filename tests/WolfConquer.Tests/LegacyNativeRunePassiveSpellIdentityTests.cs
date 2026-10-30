using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

// Standalone native query/data coverage. Actual admitted Frame/WAL behavior is
// covered separately; this scope grants no client, saver or runtime authority.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeRunePassiveSpellIdentityTests
{
    [Theory]
    [InlineData(4_020_201U, 2000U, 4_020_201U)]
    [InlineData(4_020_202U, 2000U, 4_020_201U)]
    [InlineData(4_030_101U, 0U, 4_030_101U)]
    [InlineData(4_030_102U, 0U, 4_030_101U)]
    [InlineData(4_011_501U, 9000U, 4_011_501U)]
    public void ActualRawSelfRowsKeepNativeDisplayAndEffectWithoutALearnedSpell(uint itemId, uint requiredClass, uint display)
    {
        using var scope = new MetadataScope();
        var row = Metadata(itemId, itemId, requiredClass); scope.Items[itemId] = row;
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = itemId, Uid = 123, Position = 212, RuneEXP = 456, StackSize = 1 };
        Assert.Equal(display, item.SpellID); Assert.Equal(Flags.ItemEffect.RuneEffect, item.Effect);
        var before = (item.Uid, item.ITEM_ID, item.SpellID, item.Effect, item.Position, item.RuneEXP, item.StackSize);
        Assert.Null(LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.Null(LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.Equal(before, (item.Uid, item.ITEM_ID, item.SpellID, item.Effect, item.Position, item.RuneEXP, item.StackSize));
        Assert.Same(row, scope.Items[itemId]); Assert.Equal(itemId, row.RuneSpellID); Assert.Equal(requiredClass, row.Class);

        // Independent fixed byte offsets from the existing2116 item contract.
        // This semantic correction must not change the display override/Effect1.
        var wire = LegacyGameItemPacket.FromSnapshotItem(new PlayerItemSnapshotItem
            { Uid = 123, ItemId = itemId, Position = 212, StackSize = 1 },
            LegacyGameItemPacket.CreateRuneSpellOverride(itemId, itemId)).ToFrameBytes();
        Assert.Equal((ushort)2116, BinaryPrimitives.ReadUInt16LittleEndian(wire.AsSpan(2)));
        Assert.Equal(display, BinaryPrimitives.ReadUInt32LittleEndian(wire.AsSpan(25)));
        Assert.Equal((byte)1, wire[29]);
    }

    [Theory]
    [InlineData(4_020_201U, 2000U)] [InlineData(4_020_202U, 2000U)]
    [InlineData(4_030_101U, 0U)] [InlineData(4_030_102U, 0U)] [InlineData(4_011_501U, 9000U)]
    public void ActualRawSelfRowsAreNotReverseLearnedSkillMappings(uint itemId, uint requiredClass)
    {
        using var scope = new MetadataScope();
        // The query's preserved contract reads the row.ID, not the dictionary
        // key. A key alias cannot disguise a passive row as a learned mapping.
        var row = Metadata(itemId, itemId, requiredClass); scope.Items[7] = row;
        var spell = new LegacyNativeMsgSpell { ID = checked((ushort)(itemId / 100)), Level = checked((ushort)(itemId % 100)) };
        Assert.Equal(0U, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        Assert.Same(row, scope.Items[7]); Assert.Equal(itemId, row.ID); Assert.Equal(itemId, row.RuneSpellID);
        Assert.Equal(checked((ushort)(itemId / 100)), spell.ID); Assert.Equal(checked((ushort)(itemId % 100)), spell.Level);
    }

    [Fact]
    public void PassiveRowsBeforeAnActualMappedAbilityDoNotHideTheValidNativeMapping()
    {
        using var scope = new MetadataScope();
        scope.Items[1] = Metadata(4_030_102, 4_030_102, 0);
        var active = Metadata(4_020_517, 1_416_016, 6000); scope.Items[active.ID] = active;
        var spell = new LegacyNativeMsgSpell { ID = 14160, Level = 16 };
        Assert.Equal(4_020_517U, LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = active.ID };
        var mapped = Assert.IsType<LegacyNativeMsgSpell>(LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.Equal((ushort)14160, mapped.ID); Assert.Equal((ushort)16, mapped.Level);
        Assert.Equal(1_416_016U, item.SpellID); Assert.Equal(Flags.ItemEffect.None, item.Effect);
    }

    [Fact]
    public void APassiveMatchDoesNotTurnAMalformedPoolTailIntoKnownAbsence()
    {
        using var scope = new MetadataScope();
        scope.Items[1] = Metadata(4_030_102, 4_030_102, 0); scope.Items[2] = null!;
        var spell = new LegacyNativeMsgSpell { ID = 40301, Level = 2 };
        Assert.Throws<InvalidOperationException>(() => LegacyNativeRuneItemStorage.GetRuneFromSkill(spell));
        Assert.Equal((ushort)40301, spell.ID); Assert.Equal((ushort)2, spell.Level);
    }

    [Fact]
    public void CurrentRawMetadataOwnerDeterminesTheForwardQueryWithoutRewritingOldItemDisplay()
    {
        using var scope = new MetadataScope();
        const uint itemId = 4_020_202;
        scope.Items[itemId] = Metadata(itemId, itemId, 2000);
        var item = new LegacyNativeGameItemRuntime { ITEM_ID = itemId };
        Assert.Null(LegacyNativeRuneItemStorage.GetSkillForRune(item));
        // Deliberate replacement data tests current-pool lookup only. It is not
        // an admitted catalog, a producer permission or a new ability owner.
        LegacyNativePoolItems.ItemsBase = new() { [itemId] = Metadata(itemId, 1_416_016, 2000) };
        var spell = Assert.IsType<LegacyNativeMsgSpell>(LegacyNativeRuneItemStorage.GetSkillForRune(item));
        Assert.Equal((ushort)14160, spell.ID); Assert.Equal((ushort)16, spell.Level);
        Assert.Equal(4_020_201U, item.SpellID); Assert.Equal(Flags.ItemEffect.RuneEffect, item.Effect);
        Assert.Equal(itemId, scope.Items[itemId].RuneSpellID);
    }

    private static LegacyNativeItemRuneMetadata Metadata(uint id, uint encoded, uint requiredClass) =>
        new() { ID = id, Class = requiredClass, Level = 0, RuneSpellID = encoded };

    private sealed class MetadataScope : IDisposable
    {
        private readonly LegacyNativeItemTypeRuneMetadata? before = LegacyNativePoolItems.ItemsBase;
        internal LegacyNativeItemTypeRuneMetadata Items { get; } = new();
        internal MetadataScope() => LegacyNativePoolItems.ItemsBase = Items;
        public void Dispose() => LegacyNativePoolItems.ItemsBase = before;
    }
}
