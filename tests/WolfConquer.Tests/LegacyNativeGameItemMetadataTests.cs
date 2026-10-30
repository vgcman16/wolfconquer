using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;
using MsgItemExtra = WolfConquer.Core.World.LegacyNativeItemExtra;

namespace WolfConquer.Tests;

// Source-component MODELs only. No native table loader/raw bridge/Rune gameplay,
// host startup/Inventory.Update/Send/client or full-goal acceptance follows.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeGameItemMetadataTests
{
    [Fact]
    public void EntireNativeConstructionFieldSurfaceHasExactStorageWidths()
    {
        var expected = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["MythSoul"] = typeof(uint),
            ["MythSoulPoints"] = typeof(uint),
            ["Purification"] = typeof(MsgItemExtra.Purification),
            ["Refinary"] = typeof(MsgItemExtra.Refinery),
            ["Deposite"] = typeof(ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>),
            ["ItemPoints"] = typeof(uint),
            ["Leng"] = typeof(ushort),
            ["PacketID"] = typeof(ushort),
            ["Uid"] = typeof(uint),
            ["_id"] = typeof(uint),
            ["Durability"] = typeof(ushort),
            ["MaximDurability"] = typeof(ushort),
            ["Mode"] = typeof(Flags.ItemMode),
            ["Position"] = typeof(ushort),
            ["SocketProgress"] = typeof(uint),
            ["_TimeLeftInMinutes"] = typeof(uint),
            ["SocketOne"] = typeof(Flags.Gem),
            ["SocketTwo"] = typeof(Flags.Gem),
            ["SpellID"] = typeof(uint),
            ["Effect"] = typeof(Flags.ItemEffect),
            ["Plus"] = typeof(byte),
            ["Bless"] = typeof(byte),
            ["Bound"] = typeof(byte),
            ["Enchant"] = typeof(byte),
            ["ProgresGreen"] = typeof(uint),
            ["Suspicious"] = typeof(byte),
            ["Locked"] = typeof(byte),
            ["Color"] = typeof(Flags.Color),
            ["PlusProgress"] = typeof(uint),
            ["Inscribed"] = typeof(ushort),
            ["Activate"] = typeof(uint),
            ["StackSize"] = typeof(ushort),
            ["UnKnow"] = typeof(ushort),
            ["WH_ID"] = typeof(uint),
            ["OwnerUID"] = typeof(uint),
            ["PerfectionLevel"] = typeof(uint),
            ["PerfectionRank"] = typeof(ushort),
            ["PerfectionProgress"] = typeof(uint),
            ["OwnerName"] = typeof(string),
            ["Signature"] = typeof(string),
            ["RuneEXP"] = typeof(uint),
            ["RelicAttributes"] = typeof(LegacyNativeRelicAttribute[]),
            ["AnimaItemID"] = typeof(uint),
            ["UnLockTimer"] = typeof(int),
            ["EndDate"] = typeof(DateTime),
        };
        var actual = typeof(LegacyNativeGameItemRuntime).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Assert.Equal(45, actual.Length);
        Assert.Equal(expected.Keys.OrderBy(x => x, StringComparer.Ordinal),
            actual.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
        foreach (var field in actual)
            Assert.Equal(expected[field.Name], field.FieldType);
    }

    [Fact]
    public void CompleteFreshStateMatchesNativeDefaultsWithoutSpecialFirstAssignment()
    {
        var item = new LegacyNativeGameItemRuntime();
        var fields = typeof(LegacyNativeGameItemRuntime).GetFields(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        foreach (var field in fields.Where(x => x.FieldType.IsValueType))
            Assert.Equal(Activator.CreateInstance(field.FieldType), field.GetValue(item));
        Assert.Equal(string.Empty, item.OwnerName);
        Assert.Equal(string.Empty, item.Signature);
        Assert.Equal(0u, item.ITEM_ID);
        Assert.Equal(0u, item.TimeLeftInMinutes);
        Assert.Equal(0u, item.RemainingTime);
        Assert.Equal(0u, item.PerfectionStage);
        Assert.Equal(0u, item.PerfectionStageStars);
        Assert.Equal(0u, item.GetPrestigeScore);
        Assert.Equal(0L, item.EndDate.ToBinary());
        Assert.Equal(DateTimeKind.Unspecified, item.EndDate.Kind);
        Assert.Equal((Flags.Color)0u, item.Color);
        Assert.NotEqual(Flags.Color.Black, item.Color);
    }

    [Fact]
    public void ConstructorAndGettersDoNotReadNullMetadataOrConsumeNullCounter()
    {
        var savedTable = LegacyNativePoolItems.ItemsBase;
        var savedCounter = LegacyNativePoolItems.ItemCounter;
        try
        {
            LegacyNativePoolItems.ItemsBase = null;
            LegacyNativePoolItems.ItemCounter = null!;
            var item = new LegacyNativeGameItemRuntime();
            Assert.Equal(0u, item.Uid);
            Assert.Equal(0u, item.ITEM_ID);
            Assert.Empty(item.Deposite);
            Assert.Equal(5, item.RelicAttributes.Length);
            Assert.Null(LegacyNativePoolItems.ItemsBase);
            Assert.Null(LegacyNativePoolItems.ItemCounter);
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = savedTable;
            LegacyNativePoolItems.ItemCounter = savedCounter;
        }
    }

    [Fact]
    public void StaticMythsoulBagHasExactWidthAndNativeBornValue()
    {
        var field = typeof(LegacyNativeGameItemRuntime).GetField(nameof(LegacyNativeGameItemRuntime.MythsoulBag));
        Assert.NotNull(field);
        Assert.True(field.IsStatic);
        Assert.False(field.IsInitOnly);
        Assert.Equal(typeof(ushort), field.FieldType);
        Assert.Equal((ushort)217, LegacyNativeGameItemRuntime.MythsoulBag);
    }

    [Fact]
    public void DepositeAndRelicArraysAreFreshPerItemAndRetainSameItemReferences()
    {
        var first = new LegacyNativeGameItemRuntime();
        var second = new LegacyNativeGameItemRuntime();
        Assert.NotSame(first.Deposite, second.Deposite);
        Assert.NotSame(first.RelicAttributes, second.RelicAttributes);
        Assert.Empty(first.Deposite);
        Assert.Empty(second.Deposite);
        first.Deposite.TryAdd(7u, second);
        Assert.Same(second, first.Deposite[7u]);
        Assert.Empty(second.Deposite);
        Assert.Equal(5, first.RelicAttributes.Length);
        Assert.Equal(5, second.RelicAttributes.Length);
        foreach (var slot in first.RelicAttributes)
            Assert.Equal(default(LegacyNativeRelicAttribute), slot);
        first.RelicAttributes[0].Value = 42;
        Assert.Equal((ushort)0, second.RelicAttributes[0].Value);
    }

    [Fact]
    public void MutableContainerFieldsAllowReplacementAndNullWithoutNormalization()
    {
        var item = new LegacyNativeGameItemRuntime();
        var prior = item.Deposite;
        var replacement = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
        item.Deposite = replacement;
        Assert.Same(replacement, item.Deposite);
        Assert.NotSame(prior, item.Deposite);
        item.RelicAttributes = new LegacyNativeRelicAttribute[0];
        Assert.Empty(item.RelicAttributes);
        item.Deposite = null!;
        item.RelicAttributes = null!;
        Assert.Null(item.Deposite);
        Assert.Null(item.RelicAttributes);
    }

    [Fact]
    public void ItemExtraStateIsZeroValuedAndMutatesInPlaceAsValueFields()
    {
        var item = new LegacyNativeGameItemRuntime();
        Assert.Equal(default(MsgItemExtra.Purification), item.Purification);
        Assert.Equal(default(MsgItemExtra.Refinery), item.Refinary);
        var copy = item.Purification;
        copy.ItemUID = 99u;
        Assert.Equal(0u, item.Purification.ItemUID);
        item.Purification.ItemUID = 3u;
        item.Refinary.EffectPercent2 = 4u;
        Assert.Equal(3u, item.Purification.ItemUID);
        Assert.Equal(4u, item.Refinary.EffectPercent2);
    }

    [Fact]
    public void PurificationPermanentAvailabilityRetainsNativeStickyState()
    {
        var item = new LegacyNativeGameItemRuntime();
        Assert.False(item.Purification.InLife);
        item.Purification.PurificationItemID = 1001u;
        Assert.True(item.Purification.InLife);
        item.Purification.PurificationItemID = 0u;
        Assert.True(item.Purification.InLife);
        item.Purification = default;
        Assert.False(item.Purification.InLife);
    }

    [Fact]
    public void RefineryPermanentAvailabilityRetainsNativeStickyState()
    {
        var item = new LegacyNativeGameItemRuntime();
        Assert.False(item.Refinary.InLife);
        item.Refinary.EffectID = 1001u;
        Assert.True(item.Refinary.InLife);
        item.Refinary.EffectID = 0u;
        Assert.True(item.Refinary.InLife);
        item.Refinary = default;
        Assert.False(item.Refinary.InLife);
    }

    [Fact]
    public void RelicValueStateUsesExactIntEnumAndNativeDeterministicEncoding()
    {
        Assert.Equal(typeof(int), Enum.GetUnderlyingType(typeof(LegacyNativeChiAttribute)));
        var value = new LegacyNativeRelicAttribute(LegacyNativeChiAttribute.Immunity, 25, true);
        Assert.Equal(25103u, (uint)value);
        var restored = new LegacyNativeRelicAttribute((uint)value);
        Assert.Equal(value.Type, restored.Type);
        Assert.Equal(value.Value, restored.Value);
        Assert.Equal(value.Epic, restored.Epic);
        Assert.Equal(default(LegacyNativeRelicAttribute), new LegacyNativeRelicAttribute(0u));
    }

    [Fact]
    public void NativeEnumUnderlyingWidthsAndNonzeroNamedColorAreNotNormalized()
    {
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(Flags.ItemMode)));
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Flags.ItemEffect)));
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Flags.Color)));
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(Flags.Gem)));
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(MsgItemExtra.Typing)));
        Assert.Equal(1u, (uint)Flags.ItemEffect.RuneEffect);
        Assert.Equal(200u, (uint)Flags.ItemEffect.Poison);
        Assert.Equal((ushort)11, (ushort)Flags.ItemMode.ItemInbox);
        Assert.Equal((byte)255, (byte)Flags.Gem.EmptySocket);
        Assert.Equal(2u, (uint)Flags.Color.Black);
        Assert.Equal(2u, (uint)MsgItemExtra.Typing.RefinaryAdding);
        Assert.Equal((MsgItemExtra.Typing)0u, default(MsgItemExtra.Purification).Typ);
    }

    [Theory]
    [InlineData(0u, false)]
    [InlineData(4009999u, false)]
    [InlineData(4010000u, true)]
    [InlineData(4019999u, true)]
    [InlineData(4020000u, true)]
    [InlineData(4029999u, true)]
    [InlineData(4030000u, true)]
    [InlineData(4039999u, true)]
    [InlineData(4040000u, false)]
    [InlineData(uint.MaxValue, false)]
    public void RunePredicateUsesExactUnsignedQuotientBand(uint id, bool expected)
    {
        Assert.Equal(expected, LegacyNativeItemTypeRuneMetadata.IsRune(id));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(4009999u)]
    [InlineData(4040000u)]
    [InlineData(uint.MaxValue)]
    public void OrdinaryItemIdCommitsWithoutTableAndPreservesSpellAndEffect(uint id)
    {
        WithTable(null, () =>
        {
            var item = new LegacyNativeGameItemRuntime { SpellID = 12345u, Effect = Flags.ItemEffect.Poison };
            item.ITEM_ID = id;
            Assert.Equal(id, item.ITEM_ID);
            Assert.Equal(12345u, item.SpellID);
            Assert.Equal(Flags.ItemEffect.Poison, item.Effect);
            Assert.Null(LegacyNativePoolItems.ItemsBase);
        });
    }

    [Theory]
    [InlineData(4010000u, 4010001u)]
    [InlineData(4020199u, 4020101u)]
    [InlineData(4039999u, 4039901u)]
    public void EqualRuneSpellMetadataNormalizesLastTwoDigitsThenSetsRuneEffect(uint id, uint expectedSpell)
    {
        WithTable(new LegacyNativeItemTypeRuneMetadata { [id] = new() { RuneSpellID = id } }, () =>
        {
            var item = new LegacyNativeGameItemRuntime { SpellID = 7u, Effect = Flags.ItemEffect.Poison };
            item.ITEM_ID = id;
            Assert.Equal(id, item.ITEM_ID);
            Assert.Equal(expectedSpell, item.SpellID);
            Assert.Equal(Flags.ItemEffect.RuneEffect, item.Effect);
        });
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(13001u)]
    [InlineData(uint.MaxValue)]
    public void DifferentRuneSpellMetadataCopiesExactUInt32ThenClearsEffect(uint spell)
    {
        WithTable(new LegacyNativeItemTypeRuneMetadata { [4010001u] = new() { RuneSpellID = spell } }, () =>
        {
            var item = new LegacyNativeGameItemRuntime { Effect = Flags.ItemEffect.Poison };
            item.ITEM_ID = 4010001u;
            Assert.Equal(4010001u, item.ITEM_ID);
            Assert.Equal(spell, item.SpellID);
            Assert.Equal(Flags.ItemEffect.None, item.Effect);
        });
    }

    [Fact]
    public void NullItemsBaseRefusesWithoutIdSpellOrEffectWrites()
    {
        WithTable(null, () =>
        {
            var item = new LegacyNativeGameItemRuntime { SpellID = 7u, Effect = Flags.ItemEffect.Horse };
            Assert.Throws<NullReferenceException>(() => item.ITEM_ID = 4010001u);
            Assert.Equal(0u, item.ITEM_ID);
            Assert.Equal(7u, item.SpellID);
            Assert.Equal(Flags.ItemEffect.Horse, item.Effect);
        });
    }

    [Fact]
    public void MissingRuneKeyKeepsPriorIdSpellAndEffect()
    {
        WithTable(new LegacyNativeItemTypeRuneMetadata(), () =>
        {
            var item = new LegacyNativeGameItemRuntime { SpellID = 8u, Effect = Flags.ItemEffect.MP };
            Assert.Throws<KeyNotFoundException>(() => item.ITEM_ID = 4020001u);
            Assert.Equal(0u, item.ITEM_ID);
            Assert.Equal(8u, item.SpellID);
            Assert.Equal(Flags.ItemEffect.MP, item.Effect);
        });
    }

    [Fact]
    public void PresentNullRowRefusesWithoutIdCommit()
    {
        WithTable(new LegacyNativeItemTypeRuneMetadata { [4030001u] = null! }, () =>
        {
            var item = new LegacyNativeGameItemRuntime { SpellID = 9u, Effect = Flags.ItemEffect.Shield };
            Assert.Throws<NullReferenceException>(() => item.ITEM_ID = 4030001u);
            Assert.Equal(0u, item.ITEM_ID);
            Assert.Equal(9u, item.SpellID);
            Assert.Equal(Flags.ItemEffect.Shield, item.Effect);
        });
    }

    [Fact]
    public void SameIdAssignmentStillPerformsLookupAfterMetadataRemoval()
    {
        var table = new LegacyNativeItemTypeRuneMetadata { [4010001u] = new() { RuneSpellID = 4010001u } };
        WithTable(table, () =>
        {
            var item = new LegacyNativeGameItemRuntime { ITEM_ID = 4010001u };
            table.Remove(4010001u);
            Assert.Throws<KeyNotFoundException>(() => item.ITEM_ID = 4010001u);
            Assert.Equal(4010001u, item.ITEM_ID);
            Assert.Equal(4010001u, item.SpellID);
            Assert.Equal(Flags.ItemEffect.RuneEffect, item.Effect);
        });
    }

    [Fact]
    public void SameIdRereadsMutableMetadataFieldRatherThanUsingCachedSpell()
    {
        var row = new LegacyNativeItemRuneMetadata { RuneSpellID = 4020001u };
        var table = new LegacyNativeItemTypeRuneMetadata { [4020001u] = row };
        WithTable(table, () =>
        {
            var item = new LegacyNativeGameItemRuntime { ITEM_ID = 4020001u };
            row.RuneSpellID = 123u;
            item.ITEM_ID = 4020001u;
            Assert.Equal(123u, item.SpellID);
            Assert.Equal(Flags.ItemEffect.None, item.Effect);
            Assert.Same(row, table[4020001u]);
        });
    }

    [Fact]
    public void EveryCallUsesCurrentReplaceableTableReference()
    {
        WithTable(new LegacyNativeItemTypeRuneMetadata { [4010001u] = new() { RuneSpellID = 41u } }, () =>
        {
            var item = new LegacyNativeGameItemRuntime { ITEM_ID = 4010001u };
            Assert.Equal(41u, item.SpellID);
            var replacement = new LegacyNativeItemTypeRuneMetadata { [4010001u] = new() { RuneSpellID = 42u } };
            LegacyNativePoolItems.ItemsBase = replacement;
            item.ITEM_ID = 4010001u;
            Assert.Equal(42u, item.SpellID);
            Assert.Same(replacement, LegacyNativePoolItems.ItemsBase);
        });
    }

    [Fact]
    public void ChangingRuneToOrdinaryIdDoesNotClearPriorRuneMetadata()
    {
        WithTable(new LegacyNativeItemTypeRuneMetadata { [4010001u] = new() { RuneSpellID = 4010001u } }, () =>
        {
            var item = new LegacyNativeGameItemRuntime { ITEM_ID = 4010001u };
            LegacyNativePoolItems.ItemsBase = null;
            item.ITEM_ID = 1u;
            Assert.Equal(1u, item.ITEM_ID);
            Assert.Equal(4010001u, item.SpellID);
            Assert.Equal(Flags.ItemEffect.RuneEffect, item.Effect);
        });
    }

    [Fact]
    public void InitializerOrderEffectBeforeItemIdIsDifferentFromEffectAfterItemId()
    {
        WithTable(new LegacyNativeItemTypeRuneMetadata { [4010001u] = new() { RuneSpellID = 4010001u } }, () =>
        {
            var before = new LegacyNativeGameItemRuntime { Effect = Flags.ItemEffect.Poison, ITEM_ID = 4010001u };
            var after = new LegacyNativeGameItemRuntime { ITEM_ID = 4010001u, Effect = Flags.ItemEffect.Poison };
            Assert.Equal(Flags.ItemEffect.RuneEffect, before.Effect);
            Assert.Equal(Flags.ItemEffect.Poison, after.Effect);
            Assert.Equal(4010001u, before.SpellID);
            Assert.Equal(4010001u, after.SpellID);
        });
    }

    [Fact]
    public void SetterRetainsPhysicalUidContainersAndEveryUnrelatedConstructionField()
    {
        WithTable(new LegacyNativeItemTypeRuneMetadata { [4010001u] = new() { RuneSpellID = 91u } }, () =>
        {
            var item = new LegacyNativeGameItemRuntime
            {
                Uid = 7u, MythSoul = 8u, OwnerName = "owner", Signature = "sig",
                Durability = 19, MaximDurability = 20, PerfectionLevel = 11u
            };
            item.RelicAttributes[0].Value = 44;
            var fields = typeof(LegacyNativeGameItemRuntime).GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(x => x.Name is not "_id" and not "SpellID" and not "Effect").ToArray();
            var prior = fields.ToDictionary(x => x.Name, x => x.GetValue(item), StringComparer.Ordinal);
            item.ITEM_ID = 4010001u;
            foreach (var field in fields)
                Assert.Equal(prior[field.Name], field.GetValue(item));
            Assert.Equal(7u, item.Uid);
            Assert.Equal((ushort)44, item.RelicAttributes[0].Value);
        });
    }

    [Fact]
    public void InventoryKeepsSameConcreteItemAfterMetadataAssignment()
    {
        WithTable(new LegacyNativeItemTypeRuneMetadata { [4010001u] = new() { RuneSpellID = 17u } }, () =>
        {
            var item = new LegacyNativeGameItemRuntime { Uid = 7u };
            var inventory = new LegacyNativeInventoryRuntime(null);
            inventory.AddDBItem(item);
            item.ITEM_ID = 4010001u;
            Assert.Same(item, inventory.ClientItems[7u]);
            Assert.Equal(17u, inventory.ClientItems[7u].SpellID);
            Assert.Equal(7u, inventory.ClientItems[7u].Uid);
        });
    }

    [Theory]
    [InlineData(0u, 55u)]
    [InlineData(1u, 0u)]
    [InlineData(2u, 0u)]
    public void TimeLeftStorageIsHiddenWheneverActivateIsNonzero(uint activate, uint expected)
    {
        var item = new LegacyNativeGameItemRuntime { Activate = activate, TimeLeftInMinutes = 55u };
        Assert.Equal(expected, item.TimeLeftInMinutes);
        item.Activate = 0u;
        Assert.Equal(55u, item.TimeLeftInMinutes);
        item.Activate = 2u;
        Assert.Equal(0u, item.RemainingTime);
    }

    [Fact]
    public void StageAndPrestigeAccessorsReadLiveMutableFields()
    {
        var item = new LegacyNativeGameItemRuntime { PerfectionLevel = 19u, ItemPoints = 17u };
        Assert.Equal(2u, item.PerfectionStage);
        Assert.Equal(1u, item.PerfectionStageStars);
        Assert.Equal(17u, item.GetPrestigeScore);
        item.PerfectionLevel = 0u;
        item.ItemPoints = uint.MaxValue;
        Assert.Equal(0u, item.PerfectionStage);
        Assert.Equal(0u, item.PerfectionStageStars);
        Assert.Equal(uint.MaxValue, item.GetPrestigeScore);
    }

    private static void WithTable(LegacyNativeItemTypeRuneMetadata? table, Action body)
    {
        var saved = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = table;
            body();
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = saved;
        }
    }
}
