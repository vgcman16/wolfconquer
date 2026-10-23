using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNativeSharedExtensionsTimeWrapperTests
{
    [Fact]
    public void AllSecondsKeepsARequiredSignedDateTimeExtensionSignature()
    {
        AssertTimeExtension("AllSeconds");
    }

    [Fact]
    public void GetHashCodeKeepsARequiredSignedDateTimeExtensionSignature()
    {
        AssertTimeExtension("GetHashCode");
    }

    [Fact]
    public void AllSecondsIlCallsSameOwnerOnceThenDividesByLiteralSigned1000()
    {
        var instructions = ReadInstructions(TimeMethod("AllSeconds"));
        var expected = TimeMethod("AllMilliseconds");
        var call = Assert.Single(instructions, i => i.CalledMethod is not null);
        Assert.Equal(OpCodes.Call, call.Op);
        Assert.Equal(expected, call.CalledMethod);
        var significant = instructions.Where(i => i.Op != OpCodes.Nop).ToArray();
        var division = Assert.Single(significant, i => i.Op == OpCodes.Div);
        var divisionIndex = Array.IndexOf(significant, division);
        Assert.True(divisionIndex >= 2);
        Assert.Equal((int?)1000, significant[divisionIndex - 1].Int32Operand);
        Assert.Equal(call, significant[divisionIndex - 2]);
        Assert.DoesNotContain(instructions, i => i.Op == OpCodes.Div_Un);
    }

    [Fact]
    public void GetHashCodeIlCallsSameOwnerValueOnceWithoutDateTimeInstanceHash()
    {
        var call = Assert.Single(ReadInstructions(TimeMethod("GetHashCode")),
            i => i.CalledMethod is not null);
        Assert.Equal(OpCodes.Call, call.Op);
        Assert.Equal(TimeMethod("Value"), call.CalledMethod);
    }

    private static MethodInfo TimeMethod(string name) =>
        typeof(LegacyNativeSharedExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Single(method => method.Name == name);

    private static void AssertTimeExtension(string name)
    {
        var method = TimeMethod(name);
        Assert.Equal(typeof(LegacyNativeSharedExtensions), method.DeclaringType);
        Assert.True(method.IsPublic && method.IsStatic);
        Assert.False(method.IsGenericMethod || method.ContainsGenericParameters);
        Assert.Equal(typeof(int), method.ReturnType);
        Assert.NotNull(method.GetCustomAttribute<ExtensionAttribute>());
        Assert.False((method.Attributes & MethodAttributes.PinvokeImpl) != 0);
        var parameter = Assert.Single(method.GetParameters());
        Assert.Equal("date", parameter.Name);
        Assert.Equal(typeof(DateTime), parameter.ParameterType);
        Assert.False(parameter.IsOptional || parameter.HasDefaultValue || parameter.IsOut);
        Assert.False(parameter.ParameterType.IsByRef);
    }

    // Test-local decoder: retains actual integer operands as well as resolved method identities.
    // It never invokes a decoded method and makes no fixed token, byte-length or nop-layout assumption.
    private sealed record Instruction(OpCode Op, MethodBase? CalledMethod, int? Int32Operand);

    private static Instruction[] ReadInstructions(MethodInfo method)
    {
        var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (OpCode)field.GetValue(null)!)
            .GroupBy(op => unchecked((ushort)op.Value))
            .ToDictionary(group => group.Key, group => group.First());
        var result = new List<Instruction>();
        for (var offset = 0; offset < bytes.Length;)
        {
            ushort key = bytes[offset++];
            if (key == 0xfe)
            {
                if (offset >= bytes.Length)
                    throw new InvalidOperationException("Incomplete two-byte IL opcode.");
                key = (ushort)(0xfe00 | bytes[offset++]);
            }
            var op = opcodes[key];
            var width = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField
                    or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
                    or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => SwitchWidth(bytes, offset),
                _ => throw new InvalidOperationException("Unsupported IL operand kind.")
            };
            if (width > bytes.Length - offset)
                throw new InvalidOperationException("Incomplete IL operand.");
            MethodBase? called = op.OperandType == OperandType.InlineMethod
                ? method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset)) : null;
            int? integer = op == OpCodes.Ldc_I4 ? BitConverter.ToInt32(bytes, offset)
                : op == OpCodes.Ldc_I4_S ? (sbyte)bytes[offset]
                : op == OpCodes.Ldc_I4_M1 ? -1
                : op == OpCodes.Ldc_I4_0 ? 0
                : op == OpCodes.Ldc_I4_1 ? 1
                : op == OpCodes.Ldc_I4_2 ? 2
                : op == OpCodes.Ldc_I4_3 ? 3
                : op == OpCodes.Ldc_I4_4 ? 4
                : op == OpCodes.Ldc_I4_5 ? 5
                : op == OpCodes.Ldc_I4_6 ? 6
                : op == OpCodes.Ldc_I4_7 ? 7
                : op == OpCodes.Ldc_I4_8 ? 8 : null;
            result.Add(new Instruction(op, called, integer));
            offset += width;
        }
        return result.ToArray();
    }

    private static int SwitchWidth(byte[] bytes, int offset)
    {
        if (bytes.Length - offset < 4)
            throw new InvalidOperationException("Incomplete IL switch count.");
        var count = BitConverter.ToInt32(bytes, offset);
        if (count < 0)
            throw new InvalidOperationException("Negative IL switch count.");
        return checked(4 + 4 * count);
    }
}
