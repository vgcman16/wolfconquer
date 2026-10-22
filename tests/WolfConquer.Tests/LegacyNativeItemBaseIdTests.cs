using System.Reflection;
using System.Reflection.Emit;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Managed arithmetic/metadata only. Never invokes Parse, console, files, Loading or native APIs.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemBaseIdTests
{
    [Fact]
    public void MethodKeepsNativeStaticUintSignatureAndUncheckedReferenceFreeBody()
    {
        var method = typeof(LegacyNativeItemTypeRuneMetadata).GetMethod("GetBaseID")!;
        Assert.True(method.IsPublic && method.IsStatic);
        Assert.Equal(typeof(uint), method.ReturnType);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal(typeof(uint), parameter.ParameterType);
        Assert.Equal("ID", parameter.Name);
        Assert.Empty(method.GetMethodBody()!.ExceptionHandlingClauses);
        var codes = Instructions(method);
        Assert.Contains(OpCodes.Conv_U1, codes);
        Assert.DoesNotContain(codes, code => code.Name!.Contains(".ovf", StringComparison.Ordinal));
        Assert.DoesNotContain(codes, code => code.FlowControl == FlowControl.Call ||
            code.OperandType is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineType ||
            code == OpCodes.Newarr || code == OpCodes.Newobj || code == OpCodes.Ldstr);
    }

    [Fact]
    public void ExactThreeHundredThousandReturnsBeforeGenericRebuild()
    {
        Assert.Equal(300000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(300000u));
        Assert.Equal(333000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(300001u));
        Assert.Equal(222990u, LegacyNativeItemTypeRuneMetadata.GetBaseID(299999u));
    }

    [Fact]
    public void ElevenThroughSixteenFamiliesRemoveOnlyQualityDigit()
    {
        foreach (var pair in new[] { (110009u, 110000u), (119999u, 119990u),
            (120009u, 120000u), (130009u, 130000u), (140009u, 140000u),
            (150009u, 150000u), (160009u, 160000u), (169999u, 169990u) })
            Assert.Equal(pair.Item2, LegacyNativeItemTypeRuneMetadata.GetBaseID(pair.Item1));
    }

    [Fact]
    public void TwentyFamilyRetainsItsEntireNonQualityPrefix()
    {
        Assert.Equal(200000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(200009u));
        Assert.Equal(209990u, LegacyNativeItemTypeRuneMetadata.GetBaseID(209999u));
    }

    [Fact]
    public void FourTwentySubfamilyAloneTakesGenericBranchInsideFortyTwo()
    {
        Assert.Equal(444000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(420009u));
        Assert.Equal(444990u, LegacyNativeItemTypeRuneMetadata.GetBaseID(420999u));
        Assert.Equal(421000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(421009u));
        Assert.Equal(429990u, LegacyNativeItemTypeRuneMetadata.GetBaseID(429999u));
    }

    [Fact]
    public void SixtyThroughSixtyTwoFamiliesRemoveOnlyQualityDigit()
    {
        foreach (var pair in new[] { (600009u, 600000u), (610009u, 610000u),
            (620009u, 620000u), (629999u, 629990u) })
            Assert.Equal(pair.Item2, LegacyNativeItemTypeRuneMetadata.GetBaseID(pair.Item1));
    }

    [Fact]
    public void SixtySevenAndSixtyEightFamiliesRemoveOnlyQualityDigit()
    {
        Assert.Equal(670000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(670009u));
        Assert.Equal(680000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(680009u));
        Assert.Equal(689990u, LegacyNativeItemTypeRuneMetadata.GetBaseID(689999u));
    }

    [Fact]
    public void NinetyFamilyUsesTheSameNativeDirectReturn()
    {
        Assert.Equal(900000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(900009u));
        Assert.Equal(909990u, LegacyNativeItemTypeRuneMetadata.GetBaseID(909999u));
    }

    [Fact]
    public void NeighborFamiliesStayOnGenericRatherThanExpandedEligibility()
    {
        foreach (var pair in new[] { (100009u, 111000u), (170009u, 111000u),
            (190009u, 111000u), (210009u, 222000u), (410009u, 444000u),
            (430009u, 444000u), (590009u, 555000u), (630009u, 666000u),
            (660009u, 666000u), (690009u, 666000u), (890009u, 888000u),
            (910009u, 999000u) })
            Assert.Equal(pair.Item2, LegacyNativeItemTypeRuneMetadata.GetBaseID(pair.Item1));
    }

    [Fact]
    public void GenericRebuildRepeatsNarrowedLeadingByteAndRetainsLowerHundredsAndTens()
    {
        Assert.Equal(111230u, LegacyNativeItemTypeRuneMetadata.GetBaseID(180239u));
        Assert.Equal(777980u, LegacyNativeItemTypeRuneMetadata.GetBaseID(700987u));
        Assert.Equal(999990u, LegacyNativeItemTypeRuneMetadata.GetBaseID(999999u));
    }

    [Fact]
    public void LowUnsignedValuesKeepNativeZeroAndDecimalBoundaryResults()
    {
        foreach (var pair in new[] { (0u, 0u), (1u, 0u), (9u, 0u), (10u, 10u),
            (999u, 990u), (1000u, 0u), (10000u, 0u), (99999u, 990u),
            (100000u, 111000u) })
            Assert.Equal(pair.Item2, LegacyNativeItemTypeRuneMetadata.GetBaseID(pair.Item1));
    }

    [Fact]
    public void FirstByteNarrowingReentersAllNativeDirectFamilyGroups()
    {
        foreach (var pair in new[] { (2670009u, 2670000u), (2720009u, 2720000u),
            (2760009u, 2760000u), (2980009u, 2980000u), (3160009u, 3160000u),
            (3180009u, 3180000u), (3230009u, 3230000u), (3240009u, 3240000u),
            (3460009u, 3460000u) })
            Assert.Equal(pair.Item2, LegacyNativeItemTypeRuneMetadata.GetBaseID(pair.Item1));
    }

    [Fact]
    public void WrappedFortyTwoDoesNotConfuseOriginalFourTwentySubfamily()
    {
        Assert.Equal(2980000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(2980009u));
        Assert.Equal(444000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(420009u));
    }

    [Fact]
    public void FirstByteWrapAndNegativeRangeSubtractionsDoNotThrowOrWiden()
    {
        Assert.Equal(2775990u, LegacyNativeItemTypeRuneMetadata.GetBaseID(2559999u));
        Assert.Equal(2775000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(2560000u));
        Assert.Equal(2886000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(2660009u));
    }

    [Fact]
    public void SecondByteNarrowingWrapsAtTwoHundredFiftySixHundredThousands()
    {
        Assert.Equal(28305990u, LegacyNativeItemTypeRuneMetadata.GetBaseID(25599999u));
        Assert.Equal(0u, LegacyNativeItemTypeRuneMetadata.GetBaseID(25600000u));
        Assert.Equal(0u, LegacyNativeItemTypeRuneMetadata.GetBaseID(25600009u));
        Assert.Equal(10u, LegacyNativeItemTypeRuneMetadata.GetBaseID(25600010u));
    }

    [Fact]
    public void MaximumUintUsesBothNativeNarrowingsWithoutOverflow()
    {
        Assert.Equal(21867290u, LegacyNativeItemTypeRuneMetadata.GetBaseID(uint.MaxValue));
        Assert.Equal(21867290u, LegacyNativeItemTypeRuneMetadata.GetBaseID(uint.MaxValue - 5u));
    }

    [Fact]
    public void CheckedCallerDoesNotChangeNativeUncheckedCalleeBehavior()
    {
        checked
        {
            Assert.Equal(0u, LegacyNativeItemTypeRuneMetadata.GetBaseID(0u));
            Assert.Equal(2670000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(2670009u));
            Assert.Equal(0u, LegacyNativeItemTypeRuneMetadata.GetBaseID(25600000u));
            Assert.Equal(21867290u, LegacyNativeItemTypeRuneMetadata.GetBaseID(uint.MaxValue));
        }
    }

    [Fact]
    public void ResultsDoNotDependOnInvocationOrderOrPriorInputs()
    {
        var ids = new[] { uint.MaxValue, 300000u, 0u, 420009u, 2670009u, 25600000u };
        var expected = new[] { 21867290u, 300000u, 0u, 444000u, 2670000u, 0u };
        for (var repeat = 0; repeat < 3; repeat++)
            for (var index = ids.Length - 1; index >= 0; index--)
                Assert.Equal(expected[index], LegacyNativeItemTypeRuneMetadata.GetBaseID(ids[index]));
    }

    [Fact]
    public void NullLiveCacheReferencesAreNeitherReadNorRepaired()
    {
        // Arbitrary replaceable-owner state is a model control, not a live loader reachability claim.
        var purification = LegacyNativeItemTypeRuneMetadata.PurificationItems;
        var accessorys = LegacyNativeItemTypeRuneMetadata.Accessorys;
        var garments = LegacyNativeItemTypeRuneMetadata.Garments;
        var mounts = LegacyNativeItemTypeRuneMetadata.SteedMounts;
        try
        {
            LegacyNativeItemTypeRuneMetadata.PurificationItems = null!;
            LegacyNativeItemTypeRuneMetadata.Accessorys = null!;
            LegacyNativeItemTypeRuneMetadata.Garments = null!;
            LegacyNativeItemTypeRuneMetadata.SteedMounts = null!;
            Assert.Equal(2670000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(2670009u));
            Assert.Null(LegacyNativeItemTypeRuneMetadata.PurificationItems);
            Assert.Null(LegacyNativeItemTypeRuneMetadata.Accessorys);
            Assert.Null(LegacyNativeItemTypeRuneMetadata.Garments);
            Assert.Null(LegacyNativeItemTypeRuneMetadata.SteedMounts);
        }
        finally
        {
            LegacyNativeItemTypeRuneMetadata.PurificationItems = purification;
            LegacyNativeItemTypeRuneMetadata.Accessorys = accessorys;
            LegacyNativeItemTypeRuneMetadata.Garments = garments;
            LegacyNativeItemTypeRuneMetadata.SteedMounts = mounts;
        }
    }

    [Fact]
    public void SameConcreteMainTableRowAndGenuinePlusArrayRemainUnchanged()
    {
        var plus = new LegacyNativeItemTypePlus { ID = 2670009u, Plus = 3, ItemHP = 17 };
        var array = new LegacyNativeItemTypePlus[13];
        array[3] = plus;
        var row = new LegacyNativeItemRuneMetadata { ID = 2670009u, Plus = array, RuneSpellID = 123, Class = uint.MaxValue };
        var table = new LegacyNativeItemTypeRuneMetadata { [row.ID] = row };
        Assert.Equal(2670000u, LegacyNativeItemTypeRuneMetadata.GetBaseID(row.ID));
        Assert.True(table.ContainsKey(2670009u));
        Assert.False(table.ContainsKey(2670000u));
        Assert.Same(row, table[2670009u]);
        Assert.Equal(2670009u, row.ID);
        Assert.Equal(uint.MaxValue, row.Class);
        Assert.Equal(123u, row.RuneSpellID);
        Assert.Same(array, row.Plus);
        Assert.Same(plus, row.Plus![3]);
        Assert.Equal(2670009u, plus.ID);
        Assert.Equal((byte)3, plus.Plus);
        Assert.Equal((ushort)17, plus.ItemHP);
    }

    private static IReadOnlyList<OpCode> Instructions(MethodBase method)
    {
        var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .ToDictionary(code => unchecked((ushort)code.Value));
        var result = new List<OpCode>();
        for (var offset = 0; offset < bytes.Length;)
        {
            ushort value = bytes[offset++];
            if (value == 0xfe)
                value = (ushort)(0xfe00 | bytes[offset++]);
            var code = opcodes[value];
            result.Add(code);
            offset += code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + (BitConverter.ToInt32(bytes, offset) * 4),
                _ => 4
            };
        }
        return result;
    }
}
