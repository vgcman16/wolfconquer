using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;
using WolfConquer.Core.World;
using Xunit.Abstractions;
using Table = WolfConquer.Core.World.LegacyNativeItemTypeRuneMetadata;
using Row = WolfConquer.Core.World.LegacyNativeItemRuneMetadata;

namespace WolfConquer.Tests;

// Managed calls use only source-proven enough-cost/fault paths; all actual CRT paths use fresh children.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeItemTypeRngAttemptsTests
{
    private readonly ITestOutputHelper output;

    public LegacyNativeItemTypeRngAttemptsTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void ThreePublicStaticSignaturesKeepNativeWidthsAndParameterNames()
    {
        var refinery = typeof(Table).GetMethod("GetNextRefineryItem")!;
        Assert.True(refinery.IsPublic && refinery.IsStatic);
        Assert.Equal(typeof(byte), refinery.ReturnType);
        Assert.Empty(refinery.GetParameters());
        foreach (var pair in new[] { ("UpQualityDB", "DBs"), ("UpItemMeteors", "Meteors") })
        {
            var method = typeof(Table).GetMethod(pair.Item1)!;
            Assert.True(method.IsPublic && method.IsStatic);
            Assert.Equal(typeof(bool), method.ReturnType);
            Assert.Equal(new[] { typeof(uint), typeof(uint) }, method.GetParameters().Select(p => p.ParameterType));
            Assert.Equal(new[] { "ID", pair.Item2 }, method.GetParameters().Select(p => p.Name));
            Assert.All(method.GetParameters(), p => Assert.False(p.IsOptional));
        }
    }

    [Fact]
    public void RefineryIlKeepsTwoFalseHundredDrawsAndThirtyComparisons()
    {
        var instructions = Instructions(typeof(Table).GetMethod("GetNextRefineryItem")!);
        Assert.Equal(new[] { "RandGet", "RandGet" }, Calls(instructions).Select(m => m.Name));
        var calls = instructions.Select((item, index) => (item, index))
            .Where(pair => pair.item.Called?.Name == "RandGet").ToArray();
        Assert.Equal(2, calls.Length);
        foreach (var pair in calls)
        {
            Assert.Equal(typeof(LegacyNativeBaseFunc), pair.item.Called!.DeclaringType);
            Assert.Equal(100, instructions[pair.index - 2].Immediate);
            Assert.Equal(OpCodes.Ldc_I4_0, instructions[pair.index - 1].Op);
        }
        Assert.Equal(2, instructions.Count(i => i.Immediate == 30));
    }

    [Fact]
    public void QualityIlReadsRealCostThenIntegerDividesBeforeDoubleMultiplyAndTrueDraw()
    {
        AssertUpgradeIl("UpQualityDB", "GetUpEpQualityInfo");
    }

    [Fact]
    public void MeteorIlReadsRealCostThenIntegerDividesBeforeDoubleMultiplyAndTrueDraw()
    {
        AssertUpgradeIl("UpItemMeteors", "GetUpEpLevelInfo");
    }

    [Fact]
    public void ExactLiveQualityCostsShortCircuitAcrossEveryQualityArm()
    {
        WithPool(items =>
        {
            uint[] ids = { 125000, 125006, 125007, 125008 };
            int[] costs = { 1, 2, 3, 5 };
            for (var i = 0; i < ids.Length; i++)
            {
                items.Add(ids[i], new Row { ID = ids[i], Level = 0 });
                Assert.Equal(costs[i], Table.GetUpEpQualityInfo(ids[i]));
                Assert.True(Table.UpQualityDB(ids[i], (uint)costs[i]));
                Assert.True(Table.UpQualityDB(ids[i], uint.MaxValue));
            }
        });
    }

    [Fact]
    public void MaximumDbAmountStillReadsNullGlobalBeforeAnyEarlySuccess()
    {
        WithPool(_ =>
        {
            LegacyNativePoolItems.ItemsBase = null;
            Assert.Throws<NullReferenceException>(() => Table.UpQualityDB(125007, uint.MaxValue));
        });
    }

    [Fact]
    public void MissingDictionaryKeyFaultPrecedesMaximumDbComparison()
    {
        WithPool(items =>
        {
            Assert.Throws<KeyNotFoundException>(() => Table.UpQualityDB(125007, uint.MaxValue));
            Assert.Empty(items);
        });
    }

    [Fact]
    public void PresentNullRowFaultPrecedesMaximumDbComparison()
    {
        WithPool(items =>
        {
            items.Add(125007, null!);
            Assert.Throws<NullReferenceException>(() => Table.UpQualityDB(125007, uint.MaxValue));
            Assert.Null(items[125007]);
        });
    }

    [Fact]
    public void CostZeroDenominatorEscapesBeforeMaximumDbComparison()
    {
        WithPool(items =>
        {
            items.Add(125000, new Row { ID = 125000, Level = 170 });
            Assert.Throws<DivideByZeroException>(() => Table.UpQualityDB(125000, uint.MaxValue));
            Assert.Equal((byte)170, items[125000].Level);
        });
    }

    [Fact]
    public void EachQualityAttemptRereadsTheActualMutableGlobalRowBeforeSuccess()
    {
        WithPool(items =>
        {
            var row = new Row { ID = 125000, Level = 0 };
            items.Add(125000, row);
            Assert.True(Table.UpQualityDB(125000, uint.MaxValue));
            row.Level = 170;
            Assert.Throws<DivideByZeroException>(() => Table.UpQualityDB(125000, uint.MaxValue));
            var second = new Table();
            LegacyNativePoolItems.ItemsBase = second;
            Assert.Throws<KeyNotFoundException>(() => Table.UpQualityDB(125000, uint.MaxValue));
            second.Add(125000, new Row { ID = 999, Level = 0 });
            Assert.True(Table.UpQualityDB(125000, 1));
            Assert.Same(row, items[125000]);
        });
    }

    [Fact]
    public void GuaranteedQualityPredicateDoesNotSpendOrReplaceGenuineStoredState()
    {
        WithPool(items =>
        {
            var row = new Row { ID = 125007, Level = 150, GoldWorth = 777, ConquerPointsWorth = 999 };
            items.Add(125007, row);
            var amount = 16u;
            Assert.True(Table.UpQualityDB(125007, amount));
            Assert.Equal(16u, amount);
            Assert.Single(items);
            Assert.Same(row, items[125007]);
            Assert.Equal(777u, row.GoldWorth);
            Assert.Equal(999u, row.ConquerPointsWorth);
            Assert.Equal((byte)150, row.Level);
        });
    }

    [Fact]
    public void GuaranteedMeteorPredicateUsesRealIdCostWithoutAnyGlobalItemTable()
    {
        WithPool(_ =>
        {
            LegacyNativePoolItems.ItemsBase = null;
            Assert.Equal(3, Table.GetUpEpLevelInfo(410216));
            Assert.Equal(25, Table.GetUpEpLevelInfo(130059));
            Assert.True(Table.UpItemMeteors(410216, 3));
            Assert.True(Table.UpItemMeteors(130059, 25));
            Assert.Null(LegacyNativePoolItems.ItemsBase);
        });
    }

    [Fact]
    public void MaximumMeteorAmountDoesNotNarrowToSignedIntOrReadMissingRows()
    {
        WithPool(_ =>
        {
            LegacyNativePoolItems.ItemsBase = null;
            foreach (var id in new uint[] { 0, 410216, 130059, uint.MaxValue })
                Assert.True(Table.UpItemMeteors(id, uint.MaxValue));
            Assert.Null(LegacyNativePoolItems.ItemsBase);
        });
    }

    [Fact]
    public void IsolatedRefineryFirstBranchReturnsTwoWithOneDrawContinuation()
    {
        AssertRefinery(LegacyNativeItemTypeRngChildFixture.Run("RefineryTwo", output), 2, 1);
    }

    [Fact]
    public void IsolatedRefinerySecondBranchReturnsOneWithTwoDrawContinuation()
    {
        AssertRefinery(LegacyNativeItemTypeRngChildFixture.Run("RefineryOne", output), 1, 2);
    }

    [Fact]
    public void IsolatedRefineryFinalBranchReturnsZeroWithTwoDrawContinuation()
    {
        AssertRefinery(LegacyNativeItemTypeRngChildFixture.Run("RefineryZero", output), 0, 2);
    }

    [Fact]
    public void IsolatedSubCostQualityPathCompletesWithRealClockAndCrtWithoutExactSeedOracle()
    {
        var data = LegacyNativeItemTypeRngChildFixture.Run("TrueQuality", output);
        Assert.Equal(3, data.GetProperty("Cost").GetInt32());
        Assert.Equal(2u, data.GetProperty("Offered").GetUInt32());
        Assert.Equal(33, data.GetProperty("TruncatedHundredDivCost").GetInt32());
        Assert.Equal(66d, data.GetProperty("LiteralThreshold").GetDouble());
        Assert.NotEqual(2d * (100d / 3), data.GetProperty("LiteralThreshold").GetDouble());
        Assert.True(data.GetProperty("Actual").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.True(data.GetProperty("SubCostRealReseedPathCompleted").GetBoolean());
        Assert.False(data.GetProperty("ExactClockSeedProbabilityOrDrawEquivalenceClaimed").GetBoolean());
    }

    [Fact]
    public void IsolatedSubCostMeteorPathCompletesWithRealClockAndCrtWithoutExactSeedOracle()
    {
        var data = LegacyNativeItemTypeRngChildFixture.Run("TrueMeteors", output);
        Assert.Equal(61, data.GetProperty("Cost").GetInt32());
        Assert.Equal(60u, data.GetProperty("Offered").GetUInt32());
        Assert.Equal(1, data.GetProperty("TruncatedHundredDivCost").GetInt32());
        Assert.Equal(60d, data.GetProperty("LiteralThreshold").GetDouble());
        Assert.NotEqual(60d * (100d / 61), data.GetProperty("LiteralThreshold").GetDouble());
        Assert.True(data.GetProperty("Actual").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.True(data.GetProperty("SubCostRealReseedPathCompleted").GetBoolean());
        Assert.False(data.GetProperty("ExactClockSeedProbabilityOrDrawEquivalenceClaimed").GetBoolean());
    }

    [Fact]
    public void IsolatedZeroDbAmountCompletesTheLiteralBelowCostPathWithoutAFalseOracle()
    {
        AssertZeroCompletion(LegacyNativeItemTypeRngChildFixture.Run("ZeroQuality", output), 3);
    }

    [Fact]
    public void IsolatedZeroMeteorAmountCompletesTheLiteralBelowCostPathWithoutAFalseOracle()
    {
        AssertZeroCompletion(LegacyNativeItemTypeRngChildFixture.Run("ZeroMeteors", output), 61);
    }

    private static void AssertZeroCompletion(JsonElement data, int cost)
    {
        Assert.Equal(cost, data.GetProperty("Cost").GetInt32());
        Assert.Equal(0u, data.GetProperty("Offered").GetUInt32());
        Assert.Equal(0d, data.GetProperty("LiteralThreshold").GetDouble());
        Assert.True(data.GetProperty("Actual").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.True(data.GetProperty("SubCostRealReseedPathCompleted").GetBoolean());
        Assert.False(data.GetProperty("ExactClockSeedProbabilityOrDrawEquivalenceClaimed").GetBoolean());
    }

    private static void AssertRefinery(JsonElement data, int expected, int draws)
    {
        Assert.Equal(expected, data.GetProperty("Expected").GetInt32());
        Assert.Equal(expected, data.GetProperty("Actual").GetInt32());
        Assert.Equal(draws, data.GetProperty("ExpectedDraws").GetInt32());
        Assert.Equal(data.GetProperty("ExpectedNext").GetInt64(), data.GetProperty("ActualNext").GetInt64());
        Assert.InRange(data.GetProperty("Seed").GetUInt64(), 1UL, 512UL);
        Assert.False(data.GetProperty("UniversalPrngStateOrOriginalAbiClaimed").GetBoolean());
    }

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

    private static void AssertUpgradeIl(string name, string costName)
    {
        var instructions = Instructions(typeof(Table).GetMethod(name)!);
        Assert.Equal(new[] { costName, "RandGet" }, Calls(instructions).Select(m => m.Name));
        var cost = Array.FindIndex(instructions, i => i.Called?.Name == costName);
        var division = Array.FindIndex(instructions, i => i.Op == OpCodes.Div);
        var multiplication = Array.FindIndex(instructions, i => i.Op == OpCodes.Mul);
        var rng = Array.FindIndex(instructions, i => i.Called?.Name == "RandGet");
        Assert.True(cost >= 0 && cost < division && division < multiplication && multiplication < rng);
        Assert.Contains(instructions[(division + 1)..rng], i => i.Op == OpCodes.Conv_R8);
        Assert.DoesNotContain(instructions, i => i.Op == OpCodes.Conv_I4 || i.Op == OpCodes.Conv_Ovf_I4 || i.Op == OpCodes.Conv_Ovf_I4_Un);
        Assert.Equal(typeof(LegacyNativeBaseFunc), instructions[rng].Called!.DeclaringType);
        Assert.Equal(100, instructions[rng - 2].Immediate);
        Assert.Equal(OpCodes.Ldc_I4_1, instructions[rng - 1].Op);
    }

    private sealed record Instruction(OpCode Op, MethodBase? Called, int? Immediate);

    private static MethodBase[] Calls(Instruction[] instructions) =>
        instructions.Where(i => i.Called is not null).Select(i => i.Called!).ToArray();

    private static Instruction[] Instructions(MethodInfo method)
    {
        var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!).GroupBy(o => unchecked((ushort)o.Value))
            .ToDictionary(g => g.Key, g => g.First());
        var result = new List<Instruction>();
        for (var offset = 0; offset < bytes.Length;)
        {
            ushort key = bytes[offset++];
            if (key == 0xfe)
                key = (ushort)(0xfe00 | bytes[offset++]);
            var op = opcodes[key];
            MethodBase? called = op.OperandType == OperandType.InlineMethod
                ? method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset)) : null;
            int? immediate = op.OperandType == OperandType.ShortInlineI ? (sbyte)bytes[offset]
                : op.OperandType == OperandType.InlineI ? BitConverter.ToInt32(bytes, offset) : null;
            var width = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                _ => 4,
            };
            if (op != OpCodes.Nop)
                result.Add(new Instruction(op, called, immediate));
            offset += width;
        }
        return result.ToArray();
    }
}
