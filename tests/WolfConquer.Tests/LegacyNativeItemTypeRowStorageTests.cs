using System.Reflection;
using WolfConquer.Core.World;
using Flags = WolfConquer.Core.World.LegacyNativeGameItemFlags;

namespace WolfConquer.Tests;

// Declared DBItem storage only; no parser/table loader/Class policy/client proof.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTypeRowStorageTests
{
    [Fact]
    public void CompleteNativeDeclaredFieldNamesWidthsOrderAndMutabilityAreExact()
    {
        var expected = new (string Name, Type Type)[]
        {
            ("ID", typeof(uint)),
            ("Plus", typeof(LegacyNativeItemTypePlus[])),
            ("AllowUpgradePlus", typeof(bool)),
            ("Name", typeof(string)),
            ("Class", typeof(uint)),
            ("Proficiency", typeof(byte)),
            ("Level", typeof(byte)),
            ("Gender", typeof(byte)),
            ("Strength", typeof(ushort)),
            ("Agility", typeof(ushort)),
            ("GoldWorth", typeof(uint)),
            ("MinAttack", typeof(ushort)),
            ("MaxAttack", typeof(ushort)),
            ("PhysicalDefence", typeof(ushort)),
            ("PhysicalDamageIncrease", typeof(ushort)),
            ("PhysicalDamageDecrease", typeof(ushort)),
            ("MagicDefence", typeof(ushort)),
            ("MagicAttack", typeof(ushort)),
            ("MagicDamageIncrease", typeof(ushort)),
            ("MagicDamageDecrease", typeof(ushort)),
            ("RuneSpellID", typeof(uint)),
            ("Sort", typeof(uint)),
            ("BonusAttribute", typeof(byte)),
            ("Dodge", typeof(byte)),
            ("Frequency", typeof(ushort)),
            ("ConquerPointsWorth", typeof(uint)),
            ("Durability", typeof(ushort)),
            ("StackSize", typeof(ushort)),
            ("ItemHP", typeof(ushort)),
            ("ItemMP", typeof(ushort)),
            ("AttackRange", typeof(ushort)),
            ("Type", typeof(LegacyNativeItemRuneMetadata.ItemType)),
            ("Description", typeof(string)),
            ("GradeKey", typeof(int)),
            ("Crytical", typeof(uint)),
            ("SCrytical", typeof(uint)),
            ("Imunity", typeof(uint)),
            ("Penetration", typeof(uint)),
            ("Block", typeof(uint)),
            ("BreackTrough", typeof(uint)),
            ("ConterAction", typeof(uint)),
            ("Detoxication", typeof(uint)),
            ("MetalResistance", typeof(uint)),
            ("WoodResistance", typeof(uint)),
            ("WaterResistance", typeof(uint)),
            ("FireResistance", typeof(uint)),
            ("EarthResistance", typeof(uint)),
            ("PurificationLevel", typeof(ushort)),
            ("PurificationMeteorNeed", typeof(ushort)),
            ("Time", typeof(uint))
        };
        var fields = Fields();
        Assert.Equal(50, fields.Length);
        Assert.Equal(expected.Select(pair => pair.Name), fields.Select(field => field.Name));
        for (var index = 0; index < fields.Length; index++)
        {
            Assert.Equal(expected[index].Type, fields[index].FieldType);
            Assert.True(fields[index].IsPublic);
            Assert.False(fields[index].IsStatic);
            Assert.False(fields[index].IsInitOnly);
            Assert.False(fields[index].IsLiteral);
        }
        Assert.False(typeof(LegacyNativeItemRuneMetadata).IsSealed);
        Assert.Equal(typeof(object), typeof(LegacyNativeItemRuneMetadata).BaseType);
    }

    [Fact]
    public void FreshStorageHasNativeZeroFalseEnumAndNullDefaultsWithoutOwners()
    {
        var savedTable = LegacyNativePoolItems.ItemsBase;
        var savedCounter = LegacyNativePoolItems.ItemCounter;
        try
        {
            LegacyNativePoolItems.ItemsBase = null;
            LegacyNativePoolItems.ItemCounter = null!;
            var row = new LegacyNativeItemRuneMetadata();
            foreach (var field in Fields())
            {
                if (field.FieldType.IsValueType)
                    Assert.Equal(Activator.CreateInstance(field.FieldType), field.GetValue(row));
                else
                    Assert.Null(field.GetValue(row));
            }
            Assert.Null(row.Plus);
            Assert.Null(row.Name);
            Assert.Null(row.Description);
            Assert.False(row.AllowUpgradePlus);
            Assert.Equal(LegacyNativeItemRuneMetadata.ItemType.Dropable, row.Type);
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
    public void NativeNestedItemTypeRetainsByteMembersAndUndefinedValues()
    {
        Assert.Equal(typeof(byte), Enum.GetUnderlyingType(typeof(LegacyNativeItemRuneMetadata.ItemType)));
        Assert.Equal(new[] { "Dropable", "Others" }, Enum.GetNames<LegacyNativeItemRuneMetadata.ItemType>());
        Assert.Equal((byte)0, (byte)LegacyNativeItemRuneMetadata.ItemType.Dropable);
        Assert.Equal((byte)1, (byte)LegacyNativeItemRuneMetadata.ItemType.Others);
        var row = new LegacyNativeItemRuneMetadata { Type = (LegacyNativeItemRuneMetadata.ItemType)byte.MaxValue };
        Assert.Equal(byte.MaxValue, (byte)row.Type);
    }

    [Fact]
    public void ClassIsDirectFullUInt32RowStorageNotAPlayerClassSetter()
    {
        var row = new LegacyNativeItemRuneMetadata { Class = uint.MaxValue };
        var alias = row;
        Assert.Equal(uint.MaxValue, row.Class);
        alias.Class = 19000;
        Assert.Equal(19000U, row.Class);
        row.Class = 0;
        Assert.Equal(0U, alias.Class);
        Assert.Null(typeof(LegacyNativeItemRuneMetadata).GetProperty(nameof(LegacyNativeItemRuneMetadata.Class)));
        Assert.Equal(typeof(uint), typeof(LegacyNativeItemRuneMetadata).GetField(nameof(LegacyNativeItemRuneMetadata.Class))!.FieldType);
    }

    [Fact]
    public void NativeReferenceFieldsRemainNullOrExplicitlyReplaceableWithoutDefaults()
    {
        var row = new LegacyNativeItemRuneMetadata();
        var array = new LegacyNativeItemTypePlus[13];
        row.Plus = array;
        row.Name = "name";
        row.Description = "description";
        Assert.Same(array, row.Plus);
        row.Plus = null;
        row.Name = null;
        row.Description = null;
        Assert.Null(row.Plus);
        Assert.Null(row.Name);
        Assert.Null(row.Description);
    }

    [Fact]
    public void RealPlusArraysAndConcreteElementsStaySharedLiveAliasesWithNullHoles()
    {
        var element = new LegacyNativeItemTypePlus();
        element.Parse("42@@2@@3@@4@@5@@6@@7@@8@@9@@10");
        var slots = new LegacyNativeItemTypePlus[13];
        slots[2] = element;
        var first = new LegacyNativeItemRuneMetadata { Plus = slots };
        var second = new LegacyNativeItemRuneMetadata { Plus = slots };
        Assert.Same(slots, first.Plus);
        Assert.Same(first.Plus, second.Plus);
        Assert.Same(element, first.Plus![2]);
        Assert.Null(first.Plus[0]);
        Assert.Null(first.Plus[12]);
        element.ItemHP = 77;
        second.Plus![2].Agility = 88;
        Assert.Equal((ushort)77, first.Plus[2].ItemHP);
        Assert.Equal((ushort)88, element.Vigor);
    }

    [Fact]
    public void PlusReplacementRetainsTheSameRowAndDictionaryIdentity()
    {
        var row = new LegacyNativeItemRuneMetadata { ID = 42 };
        var original = new LegacyNativeItemTypePlus[13];
        var replacement = new LegacyNativeItemTypePlus[1];
        row.Plus = original;
        var table = new LegacyNativeItemTypeRuneMetadata { [42] = row };
        table[42].Plus = replacement;
        Assert.Same(row, table[42]);
        Assert.Same(replacement, row.Plus);
        Assert.NotSame(original, row.Plus);
        Assert.Single(row.Plus!);
        row.Plus = null;
        Assert.Same(row, table[42]);
        Assert.Null(table[42].Plus);
        Assert.Equal(typeof(Dictionary<uint, LegacyNativeItemRuneMetadata>), table.GetType().BaseType);
    }

    [Fact]
    public void EveryNumericAndEnumFieldRetainsItsEntireNativeBoundaryBits()
    {
        var row = new LegacyNativeItemRuneMetadata();
        foreach (var field in Fields())
        {
            object value;
            if (field.FieldType == typeof(uint)) value = uint.MaxValue;
            else if (field.FieldType == typeof(ushort)) value = ushort.MaxValue;
            else if (field.FieldType == typeof(byte)) value = byte.MaxValue;
            else if (field.FieldType == typeof(int)) value = int.MinValue;
            else if (field.FieldType == typeof(bool)) value = true;
            else if (field.FieldType == typeof(LegacyNativeItemRuneMetadata.ItemType))
                value = (LegacyNativeItemRuneMetadata.ItemType)byte.MaxValue;
            else continue;
            field.SetValue(row, value);
            Assert.Equal(value, field.GetValue(row));
        }
        Assert.Equal(uint.MaxValue, row.Class);
        Assert.Equal(uint.MaxValue, row.RuneSpellID);
        Assert.Equal(int.MinValue, row.GradeKey);
        Assert.Equal(uint.MaxValue, row.EarthResistance);
        Assert.Equal(ushort.MaxValue, row.PurificationMeteorNeed);
    }

    [Fact]
    public void NameAndDescriptionKeepTheirExactRawReferencesAndNulls()
    {
        var name = new string(new[] { ' ', 'n', ' ', '\t' });
        var description = new string(new[] { '\t', 'd', '\r', '\n', ' ' });
        var row = new LegacyNativeItemRuneMetadata { Name = name, Description = description };
        Assert.Same(name, row.Name);
        Assert.Same(description, row.Description);
        Assert.Equal(" n \t", row.Name);
        Assert.Equal("\td\r\n ", row.Description);
        row.Name = null;
        row.Description = null;
        Assert.Null(row.Name);
        Assert.Null(row.Description);
    }

    [Fact]
    public void SingleMutableRuneSpellFieldStillDrivesTheExistingItemSetterBySameRow()
    {
        var fields = Fields();
        var runeField = Assert.Single(fields, field => field.Name == "RuneSpellID");
        Assert.Same(fields[20], runeField);
        Assert.Equal(typeof(uint), runeField.FieldType);
        var row = new LegacyNativeItemRuneMetadata { ID = 4020199, Class = uint.MaxValue, RuneSpellID = 4020199 };
        var table = new LegacyNativeItemTypeRuneMetadata { [4020199] = row };
        WithTable(table, () =>
        {
            var item = new LegacyNativeGameItemRuntime { ITEM_ID = 4020199 };
            Assert.Equal(4020101U, item.SpellID);
            Assert.Equal(Flags.ItemEffect.RuneEffect, item.Effect);
            row.RuneSpellID = 17;
            item.ITEM_ID = 4020199;
            Assert.Equal(17U, item.SpellID);
            Assert.Equal(Flags.ItemEffect.None, item.Effect);
            Assert.Equal(uint.MaxValue, row.Class);
            Assert.Same(row, LegacyNativePoolItems.ItemsBase![4020199]);
        });
    }

    [Fact]
    public void ExistingItemSetterDoesNotMutateAnyExpandedMetadataStorage()
    {
        var plus = new LegacyNativeItemTypePlus[13];
        plus[1] = new LegacyNativeItemTypePlus { ID = 42, Plus = 1, Agility = 77 };
        var row = new LegacyNativeItemRuneMetadata
        {
            ID = uint.MaxValue, Class = 19000, RuneSpellID = 4010001,
            Plus = plus, Name = "owner-row", Description = "raw-description",
            GradeKey = int.MinValue, Time = uint.MaxValue, AllowUpgradePlus = true,
            Type = LegacyNativeItemRuneMetadata.ItemType.Others
        };
        var fields = Fields();
        var before = fields.ToDictionary(field => field.Name, field => field.GetValue(row), StringComparer.Ordinal);
        var table = new LegacyNativeItemTypeRuneMetadata { [4010001] = row };
        WithTable(table, () =>
        {
            var item = new LegacyNativeGameItemRuntime { ITEM_ID = 4010001 };
            Assert.Equal(4010001U, item.SpellID);
            Assert.Equal(Flags.ItemEffect.RuneEffect, item.Effect);
            Assert.Same(row, table[4010001]);
            foreach (var field in fields)
            {
                if (field.FieldType.IsValueType) Assert.Equal(before[field.Name], field.GetValue(row));
                else Assert.Same(before[field.Name], field.GetValue(row));
            }
            Assert.Same(plus, row.Plus);
            Assert.Equal((ushort)77, row.Plus![1].Vigor);
        });
    }

    [Fact]
    public void SeparateRowsKeepIndependentScalarAndReferenceDefaultsWithoutCloningTableEntries()
    {
        var first = new LegacyNativeItemRuneMetadata();
        var second = new LegacyNativeItemRuneMetadata();
        var table = new LegacyNativeItemTypeRuneMetadata { [1] = first, [2] = second };
        Assert.NotSame(first, second);
        first.Class = uint.MaxValue;
        first.RuneSpellID = 123;
        first.Name = "first";
        first.Plus = new LegacyNativeItemTypePlus[13];
        Assert.Equal(0U, second.Class);
        Assert.Equal(0U, second.RuneSpellID);
        Assert.Null(second.Name);
        Assert.Null(second.Plus);
        Assert.Same(first, table[1]);
        Assert.Same(second, table[2]);
    }

    private static FieldInfo[] Fields() => typeof(LegacyNativeItemRuneMetadata)
        .GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
        .OrderBy(field => field.MetadataToken).ToArray();

    private static void WithTable(LegacyNativeItemTypeRuneMetadata table, Action body)
    {
        var prior = LegacyNativePoolItems.ItemsBase;
        try
        {
            LegacyNativePoolItems.ItemsBase = table;
            body();
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = prior;
        }
    }
}
