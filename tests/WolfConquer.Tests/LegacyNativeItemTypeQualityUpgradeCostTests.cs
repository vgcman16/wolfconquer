using System.Reflection;
using WolfConquer.Core.World;
using Row = WolfConquer.Core.World.LegacyNativeItemRuneMetadata;
using Table = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;

namespace WolfConquer.Tests;

// Serial source controls over the real mutable global owner. Null-row substitutions
// are declared MODEL states, not claims about native Loading publication.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTypeQualityUpgradeCostTests
{
    private static void WithPool(Action<Table> action)
    {
        var previous = LegacyNativePoolItems.ItemsBase;
        try
        {
            var items = new Table();
            LegacyNativePoolItems.ItemsBase = items;
            action(items);
        }
        finally
        {
            LegacyNativePoolItems.ItemsBase = previous;
        }
    }

    private static Row Add(Table items, uint id, byte level)
    {
        var row = new Row { ID = id, Level = level, Name = "quality-cost-control" };
        items.Add(id, row);
        return row;
    }

    [Fact]
    public void PublicStaticSignatureKeepsUintIdAndSignedIntReturn()
    {
        var method = typeof(Table).GetMethod(nameof(Table.GetUpEpQualityInfo),
            BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
        Assert.NotNull(method);
        Assert.Equal(typeof(int), method.ReturnType);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(uint), parameter.ParameterType);
        Assert.Equal("ID", parameter.Name);
    }

    [Fact]
    public void GenuineDictionaryRowLevelRemainsPublicMutableByte()
    {
        Assert.Equal(typeof(Dictionary<uint, Row>), typeof(Table).BaseType);
        var level = typeof(Row).GetField("Level",
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        Assert.NotNull(level);
        Assert.Equal(typeof(byte), level.FieldType);
        Assert.False(level.IsInitOnly);
        Assert.False(level.IsStatic);
    }

    [Fact]
    public void NullGlobalPoolFaultsEvenForDefaultQualityAndUintExtremes()
    {
        WithPool(_ =>
        {
            LegacyNativePoolItems.ItemsBase = null;
            foreach (var id in new uint[] { 0, 6, 7, 8, uint.MaxValue })
                Assert.Throws<NullReferenceException>(() => Table.GetUpEpQualityInfo(id));
        });
    }

    [Fact]
    public void MissingKeysPreserveActualDictionaryIndexerFault()
    {
        WithPool(items =>
        {
            Add(items, 125006, 0);
            foreach (var id in new uint[] { 0, 125007, 125008, uint.MaxValue })
                Assert.Throws<KeyNotFoundException>(() => Table.GetUpEpQualityInfo(id));
            Assert.Single(items);
        });
    }

    [Fact]
    public void PresentNullRowsPreserveDereferenceFaultForAllQualityArms()
    {
        WithPool(items =>
        {
            foreach (var id in new uint[] { 125000, 125006, 125007, 125008 })
            {
                items.Add(id, null!);
                Assert.Throws<NullReferenceException>(() => Table.GetUpEpQualityInfo(id));
            }
        });
    }

    [Fact]
    public void LookupUsesSuppliedKeyAndNotTheRowsEmbeddedId()
    {
        WithPool(items =>
        {
            var row = Add(items, 125006, 150);
            row.ID = 999999;
            Assert.Equal(10, Table.GetUpEpQualityInfo(125006));
            Assert.Equal(999999u, row.ID);
            Assert.Same(row, items[125006]);
            Assert.Throws<KeyNotFoundException>(() => Table.GetUpEpQualityInfo(row.ID));
        });
    }

    [Fact]
    public void AllTenUnsignedQualityDigitsKeepNativeBaselineCosts()
    {
        WithPool(items =>
        {
            int[] expected = { 1, 1, 1, 1, 1, 1, 2, 3, 5, 1 };
            for (uint quality = 0; quality < 10; quality++)
            {
                var id = 125000u + quality;
                Add(items, id, 0);
                Assert.Equal(expected[(int)quality], Table.GetUpEpQualityInfo(id));
            }
        });
    }

    [Fact]
    public void UpperUintBoundaryUsesUnsignedRemainderWithoutNarrowing()
    {
        WithPool(items =>
        {
            int[] expected = { 2, 3, 5, 1, 1, 1, 1, 1, 1, 1 };
            for (uint index = 0; index < 10; index++)
            {
                var id = uint.MaxValue - 9u + index;
                Add(items, id, 0);
                Assert.Equal(expected[(int)index], Table.GetUpEpQualityInfo(id));
            }
        });
    }

    [Fact]
    public void ZeroIdUsesItsRealStoredLevelRatherThanAnEarlyDefault()
    {
        WithPool(items =>
        {
            Add(items, 0, 150);
            Assert.Equal(5, Table.GetUpEpQualityInfo(0));
        });
    }

    [Fact]
    public void EveryLevelThroughSeventyLeavesAllFourDenominatorsUnscaled()
    {
        WithPool(items =>
        {
            uint[] ids = { 125000, 125006, 125007, 125008 };
            int[] expected = { 1, 2, 3, 5 };
            var rows = ids.Select(id => Add(items, id, 0)).ToArray();
            for (var level = 0; level <= 70; level++)
            {
                for (var index = 0; index < rows.Length; index++)
                {
                    rows[index].Level = (byte)level;
                    Assert.Equal(expected[index], Table.GetUpEpQualityInfo(ids[index]));
                }
            }
        });
    }

    [Fact]
    public void LevelSeventyOneEntersScalingBeforeIntegerDivision()
    {
        WithPool(items =>
        {
            uint[] ids = { 125000, 125006, 125007, 125008 };
            int[] expected = { 1, 2, 3, 5 };
            for (var index = 0; index < ids.Length; index++)
            {
                Add(items, ids[index], 71);
                Assert.Equal(expected[index], Table.GetUpEpQualityInfo(ids[index]));
            }
        });
    }

    [Fact]
    public void QualitySixTruncatesTheDoubleDenominatorBeforeDividing()
    {
        WithPool(items =>
        {
            var row = Add(items, 125006, 0);
            byte[] levels = { 75, 110, 150, 165, 167, 168 };
            int[] expected = { 2, 3, 10, 50, 100, 100 };
            for (var index = 0; index < levels.Length; index++)
            {
                row.Level = levels[index];
                Assert.Equal(expected[index], Table.GetUpEpQualityInfo(125006));
            }
        });
    }

    [Fact]
    public void QualitySevenTruncatesTheDoubleDenominatorBeforeDividing()
    {
        WithPool(items =>
        {
            var row = Add(items, 125007, 0);
            byte[] levels = { 80, 110, 150, 160, 166 };
            int[] expected = { 3, 5, 16, 33, 100 };
            for (var index = 0; index < levels.Length; index++)
            {
                row.Level = levels[index];
                Assert.Equal(expected[index], Table.GetUpEpQualityInfo(125007));
            }
        });
    }

    [Fact]
    public void QualityEightTruncatesTheDoubleDenominatorBeforeDividing()
    {
        WithPool(items =>
        {
            var row = Add(items, 125008, 0);
            byte[] levels = { 80, 110, 150, 160, 165 };
            int[] expected = { 5, 8, 25, 50, 100 };
            for (var index = 0; index < levels.Length; index++)
            {
                row.Level = levels[index];
                Assert.Equal(expected[index], Table.GetUpEpQualityInfo(125008));
            }
        });
    }

    [Fact]
    public void DefaultQualityKeepsHundredBasedLevelScaling()
    {
        WithPool(items =>
        {
            var row = Add(items, 125009, 0);
            byte[] levels = { 80, 110, 150, 160, 169 };
            int[] expected = { 1, 1, 5, 10, 100 };
            for (var index = 0; index < levels.Length; index++)
            {
                row.Level = levels[index];
                Assert.Equal(expected[index], Table.GetUpEpQualityInfo(125009));
            }
        });
    }

    [Fact]
    public void QualitySixZeroDenominatorFaultsAtLevels169Through171()
    {
        WithPool(items =>
        {
            var row = Add(items, 125006, 168);
            Assert.Equal(100, Table.GetUpEpQualityInfo(125006));
            for (var level = 169; level <= 171; level++)
            {
                row.Level = (byte)level;
                Assert.Throws<DivideByZeroException>(() => Table.GetUpEpQualityInfo(125006));
            }
            row.Level = 172;
            Assert.Equal(1, Table.GetUpEpQualityInfo(125006));
        });
    }

    [Fact]
    public void QualitySevenZeroDenominatorFaultsAtLevels167Through173()
    {
        WithPool(items =>
        {
            var row = Add(items, 125007, 166);
            Assert.Equal(100, Table.GetUpEpQualityInfo(125007));
            for (var level = 167; level <= 173; level++)
            {
                row.Level = (byte)level;
                Assert.Throws<DivideByZeroException>(() => Table.GetUpEpQualityInfo(125007));
            }
            row.Level = 174;
            Assert.Equal(1, Table.GetUpEpQualityInfo(125007));
        });
    }

    [Fact]
    public void QualityEightZeroDenominatorFaultsAtLevels166Through174()
    {
        WithPool(items =>
        {
            var row = Add(items, 125008, 165);
            Assert.Equal(100, Table.GetUpEpQualityInfo(125008));
            for (var level = 166; level <= 174; level++)
            {
                row.Level = (byte)level;
                Assert.Throws<DivideByZeroException>(() => Table.GetUpEpQualityInfo(125008));
            }
            row.Level = 175;
            Assert.Equal(1, Table.GetUpEpQualityInfo(125008));
        });
    }

    [Fact]
    public void DefaultQualityZeroAt170ThrowsBeforeMathMaxCanClamp()
    {
        WithPool(items =>
        {
            var row = Add(items, 125000, 169);
            Assert.Equal(100, Table.GetUpEpQualityInfo(125000));
            row.Level = 170;
            Assert.Throws<DivideByZeroException>(() => Table.GetUpEpQualityInfo(125000));
            row.Level = 171;
            Assert.Equal(1, Table.GetUpEpQualityInfo(125000));
        });
    }

    [Fact]
    public void MaximumByteLevelKeepsNativeNegativeDivisionThenOneClamp()
    {
        WithPool(items =>
        {
            for (uint quality = 0; quality < 10; quality++)
            {
                var id = 125000u + quality;
                Add(items, id, byte.MaxValue);
                Assert.Equal(1, Table.GetUpEpQualityInfo(id));
            }
        });
    }

    [Fact]
    public void MutatingTheSameRowLevelIsObservedOnTheNextCall()
    {
        WithPool(items =>
        {
            var row = Add(items, 125007, 0);
            Assert.Equal(3, Table.GetUpEpQualityInfo(125007));
            row.Level = 150;
            Assert.Equal(16, Table.GetUpEpQualityInfo(125007));
            Assert.Same(row, items[125007]);
        });
    }

    [Fact]
    public void ReplacingTheDictionaryRowIsObservedOnTheNextCall()
    {
        WithPool(items =>
        {
            var original = Add(items, 125008, 0);
            Assert.Equal(5, Table.GetUpEpQualityInfo(125008));
            var replacement = new Row { ID = 1, Level = 150 };
            items[125008] = replacement;
            Assert.Equal(25, Table.GetUpEpQualityInfo(125008));
            Assert.Same(replacement, items[125008]);
            Assert.Equal((byte)0, original.Level);
        });
    }

    [Fact]
    public void ReplacingTheActualGlobalPoolIsObservedWithoutAReceiverFacade()
    {
        WithPool(first =>
        {
            Add(first, 125006, 0);
            Assert.Equal(2, Table.GetUpEpQualityInfo(125006));
            var second = new Table();
            var replacement = Add(second, 125006, 150);
            LegacyNativePoolItems.ItemsBase = second;
            Assert.Equal(10, Table.GetUpEpQualityInfo(125006));
            Assert.Same(replacement, second[125006]);
            Assert.Equal((byte)0, first[125006].Level);
        });
    }

    [Fact]
    public void CostReadDoesNotMutateAnyStoredRowFieldOrDictionaryMembership()
    {
        WithPool(items =>
        {
            var row = Add(items, 125007, 150);
            row.Class = 777;
            row.GoldWorth = 987;
            var fields = typeof(Row).GetFields(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var before = fields.Select(field => field.GetValue(row)).ToArray();
            Assert.Equal(16, Table.GetUpEpQualityInfo(125007));
            Assert.Equal(before, fields.Select(field => field.GetValue(row)).ToArray());
            Assert.Single(items);
            Assert.Same(row, items[125007]);
        });
    }
}
