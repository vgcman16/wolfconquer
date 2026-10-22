using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;
using Xunit.Abstractions;

namespace WolfConquer.Tests;

public sealed class LegacyNativeConsoleWriteTests
{
    private readonly ITestOutputHelper output;

    public LegacyNativeConsoleWriteTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void MetadataKeepsNativeHandleStorageConstantsAndExceptionSignaturesWithoutInitializingIt()
    {
        var type = typeof(LegacyNativeConsole);
        var fields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "_InputHandle", "_OutputHandle", "ExceptionsCounter", "AllowSourcelOGIN" },
            fields.OrderBy(field => field.MetadataToken).Select(field => field.Name));
        Assert.Equal(new[] { typeof(IntPtr), typeof(IntPtr), typeof(LegacyNativeCounter), typeof(bool) },
            fields.OrderBy(field => field.MetadataToken).Select(field => field.FieldType));
        Assert.All(fields, field => Assert.False(field.IsInitOnly));
        Assert.Equal(-10, type.GetField("STD_INPUT_HANDLE")!.GetRawConstantValue());
        Assert.Equal(-11, type.GetField("STD_OUTPUT_HANDLE")!.GetRawConstantValue());
        Assert.Equal(-12, type.GetField("STD_ERROR_HANDLE")!.GetRawConstantValue());
        Assert.NotNull(type.GetMethod("AllowSource"));
        Assert.Null(type.GetMethod("Parse"));
        foreach (var name in new[] { "SaveException", "WriteException" })
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            Assert.NotNull(method);
            Assert.Equal(typeof(void), method.ReturnType);
            var parameter = Assert.Single(method.GetParameters());
            Assert.Equal(typeof(Exception), parameter.ParameterType);
            Assert.Equal("e", parameter.Name);
            Assert.False(parameter.IsOptional || parameter.IsOut || parameter.ParameterType.IsByRef);
        }
        Assert.Null(type.GetMethod("AllocConsole"));
        Assert.Null(type.GetMethod("AttachConsole"));
        Assert.Null(type.GetProperty("Title"));
    }

    [Fact]
    public void KernelImportsKeepExactDefaultCharacterSetWidthsAndLastErrorPolicy()
    {
        var type = typeof(LegacyNativeConsoleKernel32);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.Equal(new[] { "GetStdHandle", "SetStdHandle", "WriteConsole" },
            methods.OrderBy(method => method.MetadataToken).Select(method => method.Name));
        foreach (var method in methods)
        {
            var import = Assert.IsType<DllImportAttribute>(method.GetCustomAttribute<DllImportAttribute>());
            Assert.Equal("kernel32.dll", import.Value);
            Assert.Equal(method.Name, import.EntryPoint);
            Assert.Equal(CharSet.None, import.CharSet);
            Assert.Equal(method.Name == "GetStdHandle", import.SetLastError);
            Assert.False(import.ExactSpelling);
        }
        Assert.Equal(typeof(IntPtr), methods.Single(method => method.Name == "GetStdHandle").ReturnType);
        Assert.Equal(typeof(bool), methods.Single(method => method.Name == "SetStdHandle").ReturnType);
        var write = methods.Single(method => method.Name == "WriteConsole");
        Assert.Equal(typeof(bool), write.ReturnType);
        Assert.Equal(new[] { typeof(IntPtr), typeof(string), typeof(uint), typeof(uint).MakeByRefType(), typeof(IntPtr) },
            write.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.True(write.GetParameters()[3].IsOut);
    }

    [Fact]
    public void ExplicitTypeInitializerRetainsCounterHandleSetterAndInt32CheckOrder()
    {
        var type = typeof(LegacyNativeConsole);
        Assert.False((type.Attributes & TypeAttributes.BeforeFieldInit) != 0);
        var initializer = Assert.IsAssignableFrom<ConstructorInfo>(type.TypeInitializer);
        Assert.Equal(new[]
        {
            "WolfConquer.Core.World.LegacyNativeCounter::.ctor",
            "WolfConquer.Core.Runtime.LegacyNativeConsoleKernel32::GetStdHandle",
            "WolfConquer.Core.Runtime.LegacyNativeConsole::set_InputHandle",
            "WolfConquer.Core.Runtime.LegacyNativeConsoleKernel32::GetStdHandle",
            "WolfConquer.Core.Runtime.LegacyNativeConsole::set_OutputHandle",
            "WolfConquer.Core.Runtime.LegacyNativeConsole::get_InputHandle",
            "System.IntPtr::ToInt32",
            "WolfConquer.Core.Runtime.LegacyNativeConsole::get_OutputHandle",
            "System.IntPtr::ToInt32",
            "System.Exception::.ctor",
        }, Calls(initializer));
        Assert.Contains(Instructions(initializer), instruction =>
            instruction.Value is string text && text == "Unable to get the Console Handle");
        Assert.Empty(initializer.GetMethodBody()!.ExceptionHandlingClauses);
    }

    [Fact]
    public void HandleSettersCallNativeBeforeCommittingTheirOwnStorageAndDoNotCatch()
    {
        foreach (var propertyName in new[] { "InputHandle", "OutputHandle" })
        {
            var setter = typeof(LegacyNativeConsole).GetProperty(propertyName)!.SetMethod!;
            var instructions = Instructions(setter);
            var native = instructions.FindIndex(instruction => instruction.Member?.Name == "SetStdHandle");
            var store = instructions.FindIndex(instruction =>
                instruction.Code == OpCodes.Stsfld && instruction.Member?.Name == "_" + propertyName);
            Assert.True(native >= 0 && store > native);
            Assert.Contains(instructions, instruction => instruction.Member is ConstructorInfo constructor &&
                constructor.DeclaringType == typeof(Exception));
            Assert.Empty(setter.GetMethodBody()!.ExceptionHandlingClauses);
        }
    }

    [Fact]
    public void WriteOrdersClockPrefixAndHandleReadThenDiscardsNativeBooleanResult()
    {
        var method = typeof(LegacyNativeConsole).GetMethod("Write")!;
        var calls = Calls(method);
        Assert.Equal(new[]
        {
            "System.DateTime::get_Now",
            "System.DateTime::ToString",
            "System.String::Concat",
            "WolfConquer.Core.Runtime.LegacyNativeConsole::get_OutputHandle",
            "System.String::get_Length",
            "WolfConquer.Core.Runtime.LegacyNativeConsoleKernel32::WriteConsole",
        }, calls);
        var instructions = Instructions(method);
        Assert.Contains(instructions, instruction => instruction.Value is string text && text == "[hh:mm:ss ]: ");
        var native = instructions.FindIndex(instruction => instruction.Member?.Name == "WriteConsole");
        Assert.Equal(OpCodes.Pop, instructions[native + 1].Code);
        Assert.Empty(method.GetMethodBody()!.ExceptionHandlingClauses);
    }

    [Fact]
    public void WriteLineConstructsRealRandomAndDrawsOneToThirteenBeforeNullSensitiveChecks()
    {
        var method = typeof(LegacyNativeConsole).GetMethod("WriteLine")!;
        var instructions = Instructions(method);
        var calls = Calls(method);
        Assert.Equal("System.Random::.ctor", calls[0]);
        Assert.Equal("System.Random::Next", calls[1]);
        Assert.Equal(2, instructions.Count(instruction => instruction.Member is MethodInfo member &&
            member.Name == "ToString" && member.GetParameters().Length == 0 &&
            (member.DeclaringType == typeof(string) || member.DeclaringType == typeof(object))));
        Assert.Equal(1, calls.Count(call => call == "System.Random::Next"));
        Assert.True(Array.IndexOf(calls, "System.Console::WriteLine") > 1);
        Assert.True(Array.IndexOf(calls, "System.Console::set_ForegroundColor") >
            Array.IndexOf(calls, "System.Console::WriteLine"));
        Assert.Equal("WolfConquer.Core.Runtime.LegacyNativeConsole::Write", calls[^1]);
        var next = instructions.FindIndex(instruction => instruction.Member?.Name == "Next");
        Assert.Equal(new[] { 1, 14 }, instructions.Take(next).Where(instruction => instruction.Value is int)
            .Select(instruction => (int)instruction.Value!).TakeLast(2));
        Assert.Contains(instructions, instruction => instruction.Value is string text && text == "\r\n");
        Assert.Empty(method.GetMethodBody()!.ExceptionHandlingClauses);
        Assert.Equal(ConsoleColor.White, method.GetParameters()[1].DefaultValue);
    }

    [Fact]
    public void FreshChildInitializesRealPipeHandlesCounterAndAllowSource()
    {
        var result = LegacyNativeConsoleChildFixture.Run("Initialization", output);
        Assert.True(result.GetProperty("AllowSource").GetBoolean());
        Assert.Equal(1u, result.GetProperty("CounterCount").GetUInt32());
        Assert.True(result.GetProperty("NativeInputMatches").GetBoolean());
        Assert.True(result.GetProperty("NativeOutputMatches").GetBoolean());
    }

    [Fact]
    public void FreshChildSameHandleSettersCommitToStoredAndOperatingSystemSlots()
    {
        var result = LegacyNativeConsoleChildFixture.Run("SameHandleSetters", output);
        Assert.True(result.GetProperty("InputMatches").GetBoolean());
        Assert.True(result.GetProperty("OutputMatches").GetBoolean());
    }

    [Fact]
    public void FreshChildZeroHandleAssignmentsRemainZeroWithoutLazyRepair()
    {
        var result = LegacyNativeConsoleChildFixture.Run("ZeroHandleSetters", output);
        Assert.True(result.GetProperty("StoredInputZero").GetBoolean());
        Assert.True(result.GetProperty("StoredOutputZero").GetBoolean());
        Assert.True(result.GetProperty("NativeInputZero").GetBoolean());
        Assert.True(result.GetProperty("NativeOutputZero").GetBoolean());
    }

    [Fact]
    public void FreshChildRealWriteConsolePipeFailureIsIgnoredByOwnerWrite()
    {
        var result = LegacyNativeConsoleChildFixture.Run("IgnoredPipeWriteResult", output);
        Assert.False(result.GetProperty("NativeWriteReturned").GetBoolean());
        Assert.True(result.GetProperty("OwnerWriteReturnedNormally").GetBoolean());
    }

    [Fact]
    public void FreshChildBothNativeBlankWriteLineBranchesEmitOnlyTwoNewlines()
    {
        var result = LegacyNativeConsoleChildFixture.Run("BlankWriteLine", output, blankLines: 2);
        Assert.Equal(2, result.GetProperty("BlankCalls").GetInt32());
    }

    [Fact]
    public void FreshChildNullWriteLineRetainsNullReferenceFaultWithoutOutputFallback()
    {
        var result = LegacyNativeConsoleChildFixture.Run("NullWriteLine", output);
        Assert.Equal(typeof(NullReferenceException).FullName, result.GetProperty("ExceptionType").GetString());
    }

    [Fact]
    public void FreshChildNullWriteStillBuildsTimestampAndReturnsFromIgnoredPipeFailure()
    {
        var result = LegacyNativeConsoleChildFixture.Run("NullWrite", output);
        Assert.True(result.GetProperty("NullWriteReturnedNormally").GetBoolean());
    }

    private static string[] Calls(MethodBase method) => Instructions(method)
        .Where(instruction => instruction.Member is MethodBase)
        .Select(instruction => instruction.Member!.DeclaringType!.FullName + "::" + instruction.Member.Name)
        .ToArray();

    private static List<Instruction> Instructions(MethodBase method)
    {
        // Metadata/IL inspection only. No static member is invoked or field value read.
        var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!)
            .GroupBy(code => unchecked((ushort)code.Value))
            .ToDictionary(group => group.Key, group => group.First());
        var result = new List<Instruction>();
        for (var offset = 0; offset < bytes.Length;)
        {
            ushort value = bytes[offset++];
            if (value == 0xfe) value = (ushort)(0xfe00 | bytes[offset++]);
            var code = codes[value];
            object? operand = code.Value switch
            {
                0x15 => -1, 0x16 => 0, 0x17 => 1, 0x18 => 2, 0x19 => 3,
                0x1a => 4, 0x1b => 5, 0x1c => 6, 0x1d => 7, 0x1e => 8, _ => null,
            };
            MemberInfo? member = null;
            var size = code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(bytes, offset),
                _ => 4,
            };
            if (code.OperandType is OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineTok)
                member = method.Module.ResolveMember(BitConverter.ToInt32(bytes, offset));
            else if (code.OperandType == OperandType.InlineString)
                operand = method.Module.ResolveString(BitConverter.ToInt32(bytes, offset));
            else if (code.OperandType == OperandType.ShortInlineI)
                operand = (int)(sbyte)bytes[offset];
            else if (code.OperandType == OperandType.InlineI)
                operand = BitConverter.ToInt32(bytes, offset);
            result.Add(new Instruction(code, operand, member));
            offset += size;
        }
        return result;
    }

    private sealed record Instruction(OpCode Code, object? Value, MemberInfo? Member);
}
