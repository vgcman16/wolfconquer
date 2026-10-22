using System.Reflection;
using WolfConquer.Core.World;
using Table = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;

namespace WolfConquer.Tests;

// Pure points methods only. Private-field substitutions below are declared MODEL states.
// No Parse/Loading/console, native call, file IO or public gameplay route is invoked.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTypePointsTests
{
    private static readonly string[] Names =
        { "purifyStabilizationPoints", "refineryStabilizationPoints", "StonePoints", "ComposePoints" };

    private static FieldInfo Field(string name) => typeof(Table).GetField(name,
        BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
    private static ushort[] Values(string name) => (ushort[])Field(name).GetValue(null)!;

    [Fact]
    public void DeclaredTablesHaveExactNativePrivateMutableUshortStorageAndOrder()
    {
        var fields = typeof(Table).GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(Names, fields.Select(field => field.Name));
        Assert.All(fields, field =>
        {
            Assert.Equal(typeof(ushort[]), field.FieldType);
            Assert.True(field.IsPrivate);
            Assert.True(field.IsStatic);
            Assert.False(field.IsInitOnly);
            Assert.False(field.IsLiteral);
        });
    }

    [Fact]
    public void DeclaredTableInitializersKeepEveryNativeValueAndArrayLength()
    {
        Assert.Equal(new ushort[] { 10, 30, 60, 100, 150, 200 }, Values(Names[0]));
        Assert.Equal(new ushort[] { 10, 30, 70, 150, 270 }, Values(Names[1]));
        Assert.Equal(new ushort[] { 1, 10, 40, 120, 360, 1080, 3240, 9720, 29160 }, Values(Names[2]));
        Assert.Equal(new ushort[] { 20, 20, 80, 240, 720, 2160, 6480, 19440, 58320, 2700, 5500, 9000, 0 },
            Values(Names[3]));
        Assert.Equal(4, Names.Select(name => Values(name)).Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    [Fact]
    public void FourPublicStaticSignaturesRetainByteInputsAndExactUnsignedReturns()
    {
        var expected = new[]
        {
            ("PurifyStabilizationPoints", typeof(ushort), "plevel"),
            ("RefineryStabilizationPoints", typeof(ushort), "elevel"),
            ("ComposePlusPoints", typeof(uint), "plus"),
            ("StonePlusPoints", typeof(uint), "plus")
        };
        foreach (var (name, result, parameter) in expected)
        {
            var method = typeof(Table).GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)!;
            Assert.NotNull(method);
            Assert.Equal(result, method.ReturnType);
            var argument = Assert.Single(method.GetParameters());
            Assert.Equal(typeof(byte), argument.ParameterType);
            Assert.Equal(parameter, argument.Name);
        }
    }

    [Fact]
    public void PurifyZeroPreservesTheNativeNegativeIndexFault()
    {
        Assert.Throws<IndexOutOfRangeException>(() => Table.PurifyStabilizationPoints(0));
    }

    [Fact]
    public void PurifyLevelsOneThroughSixKeepExactNativeSlotOrder()
    {
        ushort[] expected = { 10, 30, 60, 100, 150, 200 };
        for (byte level = 1; level <= 6; level++)
            Assert.Equal(expected[level - 1], Table.PurifyStabilizationPoints(level));
    }

    [Fact]
    public void PurifyEveryHigherByteClampsToNativeSlotFive()
    {
        for (var level = 7; level <= byte.MaxValue; level++)
            Assert.Equal((ushort)200, Table.PurifyStabilizationPoints((byte)level));
    }

    [Fact]
    public void RefineryZeroPreservesTheNativeNegativeIndexFault()
    {
        Assert.Throws<IndexOutOfRangeException>(() => Table.RefineryStabilizationPoints(0));
    }

    [Fact]
    public void RefineryLevelsOneThroughFiveKeepExactNativeSlotOrder()
    {
        ushort[] expected = { 10, 30, 70, 150, 270 };
        for (byte level = 1; level <= 5; level++)
            Assert.Equal(expected[level - 1], Table.RefineryStabilizationPoints(level));
    }

    [Fact]
    public void RefineryEveryHigherByteClampsToNativeSlotFour()
    {
        for (var level = 6; level <= byte.MaxValue; level++)
            Assert.Equal((ushort)270, Table.RefineryStabilizationPoints((byte)level));
    }

    [Fact]
    public void ComposeEveryByteUsesNativeSlotTwelveCapAndExactUshortValues()
    {
        uint[] expected = { 20, 20, 80, 240, 720, 2160, 6480, 19440, 58320, 2700, 5500, 9000, 0 };
        for (var plus = 0; plus <= byte.MaxValue; plus++)
            Assert.Equal(expected[plus < 13 ? plus : 12], Table.ComposePlusPoints((byte)plus));
    }

    [Fact]
    public void ComposeNineThroughTwelveKeepTheNativeDropAndTerminalZero()
    {
        Assert.Equal(58320u, Table.ComposePlusPoints(8));
        Assert.Equal(2700u, Table.ComposePlusPoints(9));
        Assert.Equal(5500u, Table.ComposePlusPoints(10));
        Assert.Equal(9000u, Table.ComposePlusPoints(11));
        Assert.Equal(0u, Table.ComposePlusPoints(12));
        Assert.Equal(0u, Table.ComposePlusPoints(byte.MaxValue));
    }

    [Fact]
    public void StoneEveryByteUsesNativeSlotEightCapRatherThanComposeValues()
    {
        uint[] expected = { 1, 10, 40, 120, 360, 1080, 3240, 9720, 29160 };
        for (var plus = 0; plus <= byte.MaxValue; plus++)
            Assert.Equal(expected[plus < 9 ? plus : 8], Table.StonePlusPoints((byte)plus));
    }

    [Fact]
    public void ReplacingAndMutatingOneActualArrayIsObservedWithoutCachedValues()
    {
        using var model = new TablesScope();
        ushort[] replacement = { 11, 22, 33, 44, 55, 66 };
        model.Set(Names[0], replacement);
        Assert.Same(replacement, Field(Names[0]).GetValue(null));
        Assert.Equal((ushort)11, Table.PurifyStabilizationPoints(1));
        replacement[0] = ushort.MaxValue;
        Assert.Equal(ushort.MaxValue, Table.PurifyStabilizationPoints(1));
        replacement[5] = 123;
        Assert.Equal((ushort)123, Table.PurifyStabilizationPoints(byte.MaxValue));
    }

    [Fact]
    public void AllFourFieldsMayAliasOneActualArrayAndKeepTheirOwnNativeIndexes()
    {
        using var model = new TablesScope();
        var same = new ushort[13];
        same[0] = ushort.MaxValue;
        same[4] = 456;
        same[5] = 123;
        same[8] = 789;
        same[12] = 111;
        model.SetAll(same);
        Assert.All(Names, name => Assert.Same(same, Field(name).GetValue(null)));
        Assert.Equal(ushort.MaxValue, Table.PurifyStabilizationPoints(1));
        Assert.Equal(ushort.MaxValue, Table.RefineryStabilizationPoints(1));
        Assert.Equal(65535u, Table.ComposePlusPoints(0));
        Assert.Equal(65535u, Table.StonePlusPoints(0));
        Assert.Equal((ushort)123, Table.PurifyStabilizationPoints(byte.MaxValue));
        Assert.Equal((ushort)456, Table.RefineryStabilizationPoints(byte.MaxValue));
        Assert.Equal(111u, Table.ComposePlusPoints(byte.MaxValue));
        Assert.Equal(789u, Table.StonePlusPoints(byte.MaxValue));
    }

    [Fact]
    public void NullReplacementTablesFaultWithoutDefaultsOrLazyRepair()
    {
        using var model = new TablesScope();
        model.SetAll(null);
        Assert.Throws<NullReferenceException>(() => Table.PurifyStabilizationPoints(1));
        Assert.Throws<NullReferenceException>(() => Table.RefineryStabilizationPoints(1));
        Assert.Throws<NullReferenceException>(() => Table.ComposePlusPoints(0));
        Assert.Throws<NullReferenceException>(() => Table.StonePlusPoints(0));
        Assert.All(Names, name => Assert.Null(Field(name).GetValue(null)));
    }

    [Fact]
    public void EmptyReplacementTablesKeepIndexFaultsWithoutResizing()
    {
        using var model = new TablesScope();
        var empty = Array.Empty<ushort>();
        model.SetAll(empty);
        Assert.Throws<IndexOutOfRangeException>(() => Table.PurifyStabilizationPoints(1));
        Assert.Throws<IndexOutOfRangeException>(() => Table.RefineryStabilizationPoints(1));
        Assert.Throws<IndexOutOfRangeException>(() => Table.ComposePlusPoints(0));
        Assert.Throws<IndexOutOfRangeException>(() => Table.StonePlusPoints(0));
        Assert.All(Names, name => Assert.Same(empty, Field(name).GetValue(null)));
    }

    [Fact]
    public void ShortReplacementTablesDoNotChangeTheHardCodedClampSlots()
    {
        using var model = new TablesScope();
        ushort[] one = { 99 };
        model.SetAll(one);
        Assert.Equal((ushort)99, Table.PurifyStabilizationPoints(1));
        Assert.Equal((ushort)99, Table.RefineryStabilizationPoints(1));
        Assert.Equal(99u, Table.ComposePlusPoints(0));
        Assert.Equal(99u, Table.StonePlusPoints(0));
        Assert.Throws<IndexOutOfRangeException>(() => Table.PurifyStabilizationPoints(byte.MaxValue));
        Assert.Throws<IndexOutOfRangeException>(() => Table.RefineryStabilizationPoints(byte.MaxValue));
        Assert.Throws<IndexOutOfRangeException>(() => Table.ComposePlusPoints(byte.MaxValue));
        Assert.Throws<IndexOutOfRangeException>(() => Table.StonePlusPoints(byte.MaxValue));
        Assert.All(Names, name => Assert.Same(one, Field(name).GetValue(null)));
    }

    [Fact]
    public void ExtraReplacementCapacityDoesNotNormalizeTheZeroLevelNegativeIndex()
    {
        using var model = new TablesScope();
        var larger = Enumerable.Repeat((ushort)77, 256).ToArray();
        model.Set(Names[0], larger);
        model.Set(Names[1], larger);
        Assert.Throws<IndexOutOfRangeException>(() => Table.PurifyStabilizationPoints(0));
        Assert.Throws<IndexOutOfRangeException>(() => Table.RefineryStabilizationPoints(0));
        Assert.Equal((ushort)77, Table.PurifyStabilizationPoints(1));
        Assert.Equal((ushort)77, Table.RefineryStabilizationPoints(1));
    }

    [Fact]
    public void UshortHighBitsWidenToUintWithoutSignExtensionOrNormalization()
    {
        using var model = new TablesScope();
        model.SetAll(Enumerable.Repeat(ushort.MaxValue, 13).ToArray());
        Assert.Equal(ushort.MaxValue, Table.PurifyStabilizationPoints(byte.MaxValue));
        Assert.Equal(ushort.MaxValue, Table.RefineryStabilizationPoints(byte.MaxValue));
        Assert.Equal(65535u, Table.ComposePlusPoints(byte.MaxValue));
        Assert.Equal(65535u, Table.StonePlusPoints(byte.MaxValue));
    }

    [Fact]
    public void PureCallsWriteNoTableElementOrExistingPublicCacheReference()
    {
        var arrays = Names.Select(Values).ToArray();
        var values = arrays.Select(array => array.ToArray()).ToArray();
        var caches = typeof(Table).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        var references = caches.Select(field => field.GetValue(null)).ToArray();
        Assert.Equal((ushort)10, Table.PurifyStabilizationPoints(1));
        Assert.Equal((ushort)10, Table.RefineryStabilizationPoints(1));
        Assert.Equal(20u, Table.ComposePlusPoints(0));
        Assert.Equal(1u, Table.StonePlusPoints(0));
        for (var i = 0; i < Names.Length; i++)
        {
            Assert.Same(arrays[i], Values(Names[i]));
            Assert.Equal(values[i], Values(Names[i]));
        }
        for (var i = 0; i < caches.Length; i++)
            Assert.Same(references[i], caches[i].GetValue(null));
    }

    private sealed class TablesScope : IDisposable
    {
        private readonly FieldInfo[] fields = Names.Select(Field).ToArray();
        private readonly object?[] originals;

        internal TablesScope() => originals = fields.Select(field => field.GetValue(null)).ToArray();
        internal void Set(string name, ushort[]? value) => Field(name).SetValue(null, value);
        internal void SetAll(ushort[]? value)
        {
            foreach (var field in fields) field.SetValue(null, value);
        }
        public void Dispose()
        {
            // Tests only mutate newly installed MODEL arrays; the original graphs remain untouched.
            for (var i = 0; i < fields.Length; i++)
                fields[i].SetValue(null, originals[i]);
        }
    }
}
