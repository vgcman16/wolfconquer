using System.Data.Common;
using System.Reflection;
using System.Reflection.Emit;
using WolfConquer.Core.Data;
using Holder = WolfConquer.Core.Data.LegacyNativeDataHolder;

namespace WolfConquer.Tests;

// Public-export regression controls only. No valid connection is opened and no private
// provider identity, original binary, hash, database, credentials or endpoint is required.
[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativePublicationConfigurationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void MissingConfigurationIsRejectedAndExactSharedStorageIsPreserved(string? missing)
    {
        using var restore = new RestoreString();
        string? raw = missing is null ? null : new string(missing.ToCharArray());
        Holder.ConnectionString = raw;
        using var command = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.SELECT);
        command.Select("synthetic_publication_probe");
        string text = command.Command;

        var error = Assert.Throws<InvalidOperationException>(() => command.Execute2());

        Assert.Equal("Execute2 requires an explicitly configured database connection string.", error.Message);
        Assert.Same(raw, Holder.ConnectionString);
        Assert.Equal(text, command.Command);
        Assert.Equal(LegacyNativeMySqlCommandType.SELECT, command.Type);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void ExecuteAndReaderRejectMissingConfigurationBeforeTheirConnectionGetter(string? missing)
    {
        using var restore = new RestoreString();
        Holder.ConnectionString = missing;
        using var command = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.SELECT);
        command.Select("synthetic_publication_probe").Where("Value", "preserved'");
        string text = command.Command;

        var execute = Assert.Throws<InvalidOperationException>(() => command.Execute());
        Assert.Equal("Execute requires an explicitly configured database connection string.", execute.Message);
        var reader = Assert.Throws<InvalidOperationException>(() => command.CreateReader());
        Assert.Equal("Reader requires an explicitly configured database connection string.", reader.Message);
        Assert.Same(missing, Holder.ConnectionString);
        Assert.Equal(text, command.Command);
        using var provider = command.CreateProviderCommand();
        Assert.Equal("preserved'", provider.Parameters[0].Value);
    }

    [Fact]
    public void ExplicitConnectionExecutionDoesNotConsultUnrelatedSharedConfiguration()
    {
        // A real caller-owned closed connection must be refused by the provider, never opened here.
        using var restore = new RestoreString();
        Holder.ConnectionString = null;
        using var command = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.UPDATE);
        command.Update("synthetic_publication_probe").Set("Value", 1L);
        using var connection = new MySql.Data.MySqlClient.MySqlConnection();
        bool disposed = false;
        connection.Disposed += (_, _) => disposed = true;
        var error = Assert.Throws<InvalidOperationException>(() => command.Execute(connection));
        Assert.DoesNotContain("configured database connection string", error.Message, StringComparison.Ordinal);
        Assert.Equal(System.Data.ConnectionState.Closed, connection.State);
        Assert.False(disposed);
    }

    [Fact]
    public void Execute2GuardsRawConfigurationThenUsesTheHolderGetterWithoutAnOverride()
    {
        // Structural, non-network proof for the actual shared guard and Execute2 call route.
        MethodInfo execute = typeof(LegacyNativeMySqlCommand).GetMethod(nameof(LegacyNativeMySqlCommand.Execute2))!;
        MethodInfo guard = typeof(LegacyNativeMySqlCommand).GetMethod(nameof(LegacyNativeMySqlCommand.RequireConfiguredConnection), BindingFlags.Static | BindingFlags.NonPublic)!;
        var instructions = ReadMethod(execute);
        var validation = ReadMethod(guard);
        int check = Array.FindIndex(instructions, instruction => Equals(instruction.Member, guard));
        int getter = Array.FindIndex(instructions, instruction => instruction.Member is MethodInfo method &&
            method.DeclaringType == typeof(Holder) && method.Name == "get_MySqlConnection");
        var openContract = typeof(DbConnection).GetMethod(nameof(DbConnection.Open), Type.EmptyTypes)!;
        int open = Array.FindIndex(instructions, instruction => instruction.Member is MethodInfo method &&
            method.IsVirtual && !method.IsStatic && method.GetBaseDefinition().Equals(openContract));
        Assert.InRange(check, 1, instructions.Length - 1);
        Assert.InRange(getter, check + 1, instructions.Length - 1);
        Assert.InRange(open, getter + 1, instructions.Length - 1);
        int whitespace = Array.FindIndex(validation, instruction => instruction.Member is MethodInfo method &&
            method.DeclaringType == typeof(string) && method.Name == nameof(string.IsNullOrWhiteSpace));
        Assert.InRange(whitespace, 1, validation.Length - 1);
        var prior = validation.Take(whitespace).Last(instruction => instruction.Code != OpCodes.Nop);
        Assert.Equal(OpCodes.Ldsfld, prior.Code);
        Assert.Equal(typeof(Holder).GetField(nameof(Holder.ConnectionString)), prior.Member);
        Assert.Contains(validation[(whitespace + 1)..], instruction => instruction.Code == OpCodes.Throw);
        Assert.Contains(validation[(whitespace + 1)..], instruction => instruction.Member is ConstructorInfo ctor &&
            ctor.DeclaringType == typeof(InvalidOperationException));
        Assert.Single(instructions, instruction => instruction.Member is MethodInfo method &&
            method.DeclaringType == typeof(Holder) && method.Name == "get_MySqlConnection");
        var all = instructions.Concat(validation).ToArray();
        Assert.DoesNotContain(all, instruction => instruction.Member is MethodInfo method && method.Name == "set_ConnectionString");
        Assert.DoesNotContain(all, instruction => instruction.Code == OpCodes.Stsfld &&
            Equals(instruction.Member, typeof(Holder).GetField(nameof(Holder.ConnectionString))));
        Assert.DoesNotContain(all, instruction => instruction.Text is string text &&
            (text.Contains("Server=", StringComparison.OrdinalIgnoreCase) || text.Contains("Password=", StringComparison.OrdinalIgnoreCase)));
    }
    private static Instruction[] ReadMethod(MethodInfo method)
    {
        byte[] bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        Assert.InRange(bytes.Length, 1, 8192);
        var codes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode))
            .Select(field => (OpCode)field.GetValue(null)!).ToDictionary(code => code.Value);
        var result = new List<Instruction>();
        int offset = 0;
        while (offset < bytes.Length)
        {
            byte first = bytes[offset++];
            short value = first == 0xFE ? (short)(0xFE00 | bytes[offset++]) : first;
            OpCode code = codes[value];
            int size = code.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI or
                    OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or
                    OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => checked(4 + BitConverter.ToInt32(bytes, offset) * 4),
                _ => throw new InvalidOperationException("Unsupported test IL operand."),
            };
            Assert.InRange(size, 0, bytes.Length - offset);
            MemberInfo? member = code.OperandType is OperandType.InlineMethod or OperandType.InlineField
                ? method.Module.ResolveMember(BitConverter.ToInt32(bytes, offset)) : null;
            string? text = code.OperandType == OperandType.InlineString
                ? method.Module.ResolveString(BitConverter.ToInt32(bytes, offset)) : null;
            result.Add(new Instruction(code, member, text));
            offset += size;
        }
        Assert.Equal(bytes.Length, offset);
        return result.ToArray();
    }

    private sealed record Instruction(OpCode Code, MemberInfo? Member, string? Text);

    private sealed class RestoreString : IDisposable
    {
        private readonly string? previous = Holder.ConnectionString;
        public void Dispose() => Holder.ConnectionString = previous;
    }
}
