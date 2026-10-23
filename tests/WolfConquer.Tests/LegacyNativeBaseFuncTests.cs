using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed class LegacyNativeBaseFuncTests
{
    private readonly ITestOutputHelper output;

    public LegacyNativeBaseFuncTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void NativeImportsKeepTheSourceDeclaredCdeclLongAndUlongAbi()
    {
        foreach (var name in new[] { "srand", "rand" })
        {
            var method = typeof(LegacyNativeBaseFunc).GetMethod(name)!;
            var import = method.GetCustomAttribute<DllImportAttribute>()!;
            Assert.Equal(typeof(long), method.ReturnType);
            Assert.Equal("msvcrt.dll", import.Value);
            Assert.Equal(name, import.EntryPoint);
            Assert.Equal(CallingConvention.Cdecl, import.CallingConvention);
            Assert.Equal(CharSet.None, import.CharSet);
            Assert.False(import.ExactSpelling);
            Assert.False(import.SetLastError);
            Assert.True(import.PreserveSig);
        }
        Assert.Equal(typeof(ulong), typeof(LegacyNativeBaseFunc).GetMethod("srand")!.GetParameters().Single().ParameterType);
        Assert.Empty(typeof(LegacyNativeBaseFunc).GetMethod("rand")!.GetParameters());
    }

    [Fact]
    public void RandGetKeepsItsLiteralPublicIntBoolSignatureWithoutOptionalParameters()
    {
        var method = typeof(LegacyNativeBaseFunc).GetMethod("RandGet")!;
        Assert.True(method.IsPublic && method.IsStatic);
        Assert.Equal(typeof(int), method.ReturnType);
        Assert.Equal(new[] { typeof(int), typeof(bool) }, method.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(new[] { "nMax", "bRealRand" }, method.GetParameters().Select(p => p.Name));
        Assert.All(method.GetParameters(), p => Assert.False(p.IsOptional));
        // Native BaseFunc145..152 is public too; the later whole-method port must
        // remain visible rather than being hidden to preserve an older API snapshot.
        var selection = typeof(LegacyNativeBaseFunc).GetMethod("RandFromGivingNums")!;
        Assert.True(selection.IsPublic && selection.IsStatic);
        Assert.Equal(typeof(uint), selection.ReturnType);
        var selectionParameter = Assert.Single(selection.GetParameters());
        Assert.Equal(typeof(uint[]), selectionParameter.ParameterType);
        Assert.Equal("nums", selectionParameter.Name);
        Assert.False(selectionParameter.IsOptional);
        // Whole native BaseFunc88..132 retains its public policy and optional-size contract.
        var nameCheck = typeof(LegacyNativeBaseFunc).GetMethod("NameStrCheck")!;
        Assert.True(nameCheck.IsPublic && nameCheck.IsStatic);
        Assert.Equal(typeof(bool), nameCheck.ReturnType);
        Assert.Equal(new[] { typeof(string), typeof(bool) }, nameCheck.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(new[] { "name", "ExceptedSize" }, nameCheck.GetParameters().Select(p => p.Name));
        Assert.False(nameCheck.GetParameters()[0].IsOptional);
        Assert.True(nameCheck.GetParameters()[1].IsOptional);
        Assert.Equal(true, nameCheck.GetParameters()[1].DefaultValue);
        // Whole native BaseFunc29..50 is public, with only forging optional.
        var animaRate = typeof(LegacyNativeBaseFunc).GetMethod("AnimaUpgradeRate")!;
        Assert.True(animaRate.IsPublic && animaRate.IsStatic);
        Assert.Equal(typeof(byte), animaRate.ReturnType);
        Assert.Equal(new[] { typeof(uint), typeof(bool) }, animaRate.GetParameters().Select(p => p.ParameterType));
        Assert.Equal(new[] { "AnimaID", "forging" }, animaRate.GetParameters().Select(p => p.Name));
        Assert.False(animaRate.GetParameters()[0].IsOptional);
        Assert.True(animaRate.GetParameters()[1].IsOptional);
        Assert.Equal(false, animaRate.GetParameters()[1].DefaultValue);
        Assert.Equal(new[] { "AnimaUpgradeRate", "NameStrCheck", "ProficiencyLevelExperience", "RandFromGivingNums", "RandGet", "ReverseString", "rand", "srand" },
            typeof(LegacyNativeBaseFunc).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
    }

    [Fact]
    public void TimeHelpersKeepSignedDateTimeExtensionSignatures()
    {
        var methods = typeof(LegacyNativeSharedExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "AllMilliseconds", "AllSeconds", "GetHashCode", "Iterate", "Iteratefun", "Iteratefunbool", "RemoveIllegalCharacters", "Value" }, methods.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal));
        Assert.All(methods.Where(method => method.Name is "AllMilliseconds" or "AllSeconds" or "GetHashCode" or "Value"), method =>
        {
            Assert.Equal(typeof(int), method.ReturnType);
            Assert.Equal(typeof(DateTime), method.GetParameters().Single().ParameterType);
            Assert.NotNull(method.GetCustomAttribute<ExtensionAttribute>());
        });
    }

    [Fact]
    public void BothPrivateMutableTypeInitializerTablesKeepEveryNativeValue()
    {
        var fields = typeof(LegacyNativeBaseFunc)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .OrderBy(f => f.MetadataToken).ToArray();
        Assert.Equal(new[] { "_SteedSpeed", "proficiencyLevelExperience" }, fields.Select(f => f.Name));
        Assert.All(fields, f => Assert.True(f.IsPrivate && f.IsStatic && !f.IsInitOnly && !f.IsLiteral));
        Assert.Equal(typeof(byte[]), fields[0].FieldType);
        Assert.Equal(typeof(uint[]), fields[1].FieldType);
        Assert.Equal(new byte[] { 0, 5, 10, 15, 20, 30, 40, 50, 65, 85, 90, 95, 100 },
            Assert.IsType<byte[]>(fields[0].GetValue(null)));
        Assert.Equal(new uint[]
        {
            0u, 1200u, 68000u, 250000u, 640000u, 1600000u, 4000000u, 10000000u, 22000000u, 40000000u,
            90000000u, 95000000u, 142500000u, 213750000u, 320625000u, 480937500u, 721406250u,
            1082109375u, 1623164062u, 2100000000u, 3200000000u
        }, Assert.IsType<uint[]>(fields[1].GetValue(null)));
        Assert.True((typeof(LegacyNativeBaseFunc).Attributes & TypeAttributes.BeforeFieldInit) != 0);
    }

    [Fact]
    public void AllMillisecondsIlKeepsLiveTickCountAndTwoRawDateSamplesInOrder()
    {
        var instructions = Instructions(typeof(LegacyNativeSharedExtensions).GetMethod("AllMilliseconds")!);
        Assert.Equal(new[]
        {
            "System.Environment.get_TickCount", "System.DateTime.get_Ticks",
            "System.DateTime.get_Now", "System.DateTime.get_Ticks"
        }, CalledMethods(instructions));
        Assert.Contains(instructions, i => i.Op == OpCodes.Div);
        Assert.Contains(instructions, i => i.Op == OpCodes.Conv_I4);
        Assert.Contains(instructions, i => i.Op == OpCodes.Add);
        Assert.DoesNotContain(instructions, i => i.Op == OpCodes.Add_Ovf || i.Op == OpCodes.Conv_Ovf_I4);
    }

    [Fact]
    public void ValueIlDelegatesToTheSameSignedAllMillisecondsOwner()
    {
        Assert.Equal(new[] { "WolfConquer.Core.World.LegacyNativeSharedExtensions.AllMilliseconds" },
            CalledMethods(Instructions(typeof(LegacyNativeSharedExtensions).GetMethod("Value")!)));
    }

    [Fact]
    public void RandGetIlKeepsOptionalNowValueSeedThenOneNativeDraw()
    {
        Assert.Equal(new[]
        {
            "System.DateTime.get_Now", "WolfConquer.Core.World.LegacyNativeSharedExtensions.Value",
            "WolfConquer.Core.World.LegacyNativeBaseFunc.srand", "WolfConquer.Core.World.LegacyNativeBaseFunc.rand"
        }, CalledMethods(Instructions(typeof(LegacyNativeBaseFunc).GetMethod("RandGet")!)));
    }

    [Fact]
    public void IsolatedCrtImportsRepeatTheSameSeededStreamOnOneChildThread()
    {
        var data = LegacyNativeBaseFuncChildFixture.Run("SeedRepeatability", output);
        Assert.Equal(data.GetProperty("First").GetInt64(), data.GetProperty("RepeatedFirst").GetInt64());
        Assert.Equal(data.GetProperty("Second").GetInt64(), data.GetProperty("RepeatedSecond").GetInt64());
    }

    [Fact]
    public void IsolatedFalseRandGetConsumesOneDrawAtMaximum100()
    {
        AssertProbe(LegacyNativeBaseFuncChildFixture.Run("False100", output), 100);
    }

    [Fact]
    public void IsolatedZeroMaximumClampsToOneAndStillConsumesOneDraw()
    {
        var data = LegacyNativeBaseFuncChildFixture.Run("Zero", output);
        AssertProbe(data, 0);
        Assert.Equal(0, data.GetProperty("Actual").GetInt32());
    }

    [Fact]
    public void IsolatedNegativeMaximumClampsToOneAndStillConsumesOneDraw()
    {
        var data = LegacyNativeBaseFuncChildFixture.Run("Negative", output);
        Assert.Equal(2, data.GetArrayLength());
        AssertProbe(data[0], -1);
        AssertProbe(data[1], int.MinValue);
        Assert.Equal(0, data[0].GetProperty("Actual").GetInt32());
        Assert.Equal(0, data[1].GetProperty("Actual").GetInt32());
    }

    [Fact]
    public void IsolatedUnitMaximumStillConsumesOneDraw()
    {
        var data = LegacyNativeBaseFuncChildFixture.Run("One", output);
        AssertProbe(data, 1);
        Assert.Equal(0, data.GetProperty("Actual").GetInt32());
    }

    [Fact]
    public void IsolatedIntMaximumPreservesTheNativeLongRemainder()
    {
        AssertProbe(LegacyNativeBaseFuncChildFixture.Run("IntMaximum", output), int.MaxValue);
    }

    [Fact]
    public void IsolatedFalseCallsShareOneCrtStreamAcrossDifferentMaxima()
    {
        var data = LegacyNativeBaseFuncChildFixture.Run("SharedStream", output);
        Assert.Equal(4, data.GetProperty("Actual").GetArrayLength());
        Assert.Equal(5, data.GetProperty("Reference").GetArrayLength());
        for (var i = 0; i < 4; i++)
            Assert.Equal((int)(data.GetProperty("Reference")[i].GetInt64() % data.GetProperty("Maxima")[i].GetInt32()),
                data.GetProperty("Actual")[i].GetInt32());
        Assert.Equal(data.GetProperty("Reference")[4].GetInt64(), data.GetProperty("Next").GetInt64());
    }

    [Fact]
    public void IsolatedTrueReseedPathWithUnitMaximumReturnsZero()
    {
        var data = LegacyNativeBaseFuncChildFixture.Run("TrueUnit", output);
        Assert.Equal(0, data.GetProperty("Actual").GetInt32());
        Assert.True(data.GetProperty("ReseedBranch").GetBoolean());
        Assert.False(data.GetProperty("ExactClockSeedOrOriginalAbiClaimed").GetBoolean());
    }

    private static void AssertProbe(System.Text.Json.JsonElement data, int maximum)
    {
        Assert.Equal(maximum, data.GetProperty("Maximum").GetInt32());
        Assert.Equal(data.GetProperty("Expected").GetInt32(), data.GetProperty("Actual").GetInt32());
        Assert.Equal(data.GetProperty("ExpectedNext").GetInt64(), data.GetProperty("ActualNext").GetInt64());
    }

    private sealed record Instruction(OpCode Op, MethodBase? CalledMethod);

    private static string[] CalledMethods(Instruction[] instructions) =>
        instructions.Where(i => i.CalledMethod is not null)
            .Select(i => i.CalledMethod!.DeclaringType!.FullName + "." + i.CalledMethod.Name).ToArray();

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
            var width = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                _ => 4,
            };
            result.Add(new Instruction(op, called));
            offset += width;
        }
        return result.ToArray();
    }
}
