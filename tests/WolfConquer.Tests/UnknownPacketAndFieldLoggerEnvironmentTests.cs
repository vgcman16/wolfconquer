using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[CollectionDefinition("WorldWireDiagnosticEnvironment", DisableParallelization = true)]
public sealed class WorldWireDiagnosticEnvironmentCollection { }

[Collection("WorldWireDiagnosticEnvironment")]
public sealed class UnknownPacketAndFieldLoggerEnvironmentTests
{
    private static readonly string[] Names =
    [
        UnknownPacketAndFieldLogger.EnabledEnvironmentVariable, UnknownPacketAndFieldLogger.DirectoryEnvironmentVariable,
        UnknownPacketAndFieldLogger.ProtobufOpcodesEnvironmentVariable, UnknownPacketAndFieldLogger.RawOpcodesEnvironmentVariable,
    ];

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData(" ")]
    public void MissingOrDisabledOptInDoesNotOpenOutput(string? enabled)
    {
        WorldWireDiagnosticTestFiles.Run(files => InEnvironment([enabled, files.Root, "invalid", "invalid"], () =>
        {
            Assert.True(UnknownPacketAndFieldLogger.TryCreateFromEnvironment(out var logger, out var reason), reason);
            Assert.NotNull(logger);
            using (logger) { Assert.False(logger.IsEnabled); Assert.Null(logger.OutputPath); }
            Assert.Empty(Directory.EnumerateFileSystemEntries(files.Root));
        }));
    }

    [Theory]
    [InlineData("01")]
    [InlineData("true")]
    [InlineData("1 ")]
    [InlineData("+1")]
    public void OptInRequiresTheExactEnabledToken(string enabled)
    {
        WorldWireDiagnosticTestFiles.Run(files => InEnvironment([enabled, files.Root, null, null], () =>
        {
            Assert.False(UnknownPacketAndFieldLogger.TryCreateFromEnvironment(out var logger, out var reason));
            Assert.Null(logger);
            Assert.Equal("invalid_diagnostic_environment", reason);
            Assert.Empty(Directory.EnumerateFileSystemEntries(files.Root));
        }));
    }

    [Theory]
    [InlineData(false, " 2419")]
    [InlineData(true, "2419,")]
    [InlineData(false, "２４１９")]
    [InlineData(true, "+2419")]
    [InlineData(false, "2132")]
    [InlineData(true, "2179")]
    public void OpcodeCsvUsesOnlyTheExplicitAsciiAllowlist(bool raw, string value)
    {
        WorldWireDiagnosticTestFiles.Run(files => InEnvironment(["1", files.Root, raw ? null : value, raw ? value : null], () =>
        {
            Assert.False(UnknownPacketAndFieldLogger.TryCreateFromEnvironment(out var logger, out var reason));
            Assert.Null(logger);
            Assert.Equal("invalid_diagnostic_environment", reason);
            Assert.Empty(Directory.EnumerateFileSystemEntries(files.Root));
        }));
    }

    [Theory]
    [InlineData("2419\0")]
    [InlineData("2419\0,2419")]
    public void ActualCsvParserRejectsTrailingNulWithoutEnvironmentSetterNormalization(string value)
    {
        // The real Environment setter normalizes NUL-containing values. Test
        // the lexical helper directly rather than pretending NUL survives it.
        var parser = typeof(UnknownPacketAndFieldLogger).GetMethod("TryReadOpcodeList", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(parser);
        object?[] arguments = [value, null];
        Assert.False(Assert.IsType<bool>(parser.Invoke(null, arguments)));
        Assert.Empty(Assert.IsType<ushort[]>(arguments[1]));
    }

    [Fact]
    public void ExactExplicitSchemaOptInReachesTheStructuralObserver()
    {
        WorldWireDiagnosticTestFiles.Run(files => InEnvironment(["1", files.Root, "2419", null], () =>
        {
            Assert.True(UnknownPacketAndFieldLogger.TryCreateFromEnvironment(out var logger, out var reason), reason);
            Assert.NotNull(logger);
            files.Track(logger);
            logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13,
                UnknownPacketAndFieldLoggerTests.Frame(2419, [0x08, 0, 0x48, 1]), false,
                new ConquerTcpConnection(null, null), new WorldSessionStore());
            var record = Assert.Single(files.Read(logger));
            Assert.Equal("complete", record.GetProperty("scanStatus").GetString());
            Assert.Equal("VisibleDx8D13.CMsgRandomDailyTask.fields1-8", record.GetProperty("schema").GetString());
        }));
    }

    private static void InEnvironment(string?[] values, Action body)
    {
        var before = Names.Select(Environment.GetEnvironmentVariable).ToArray();
        Exception? primary = null;
        try
        {
            for (var index = 0; index < Names.Length; index++) Environment.SetEnvironmentVariable(Names[index], values[index]);
            body();
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            var errors = new List<Exception>();
            for (var index = 0; index < Names.Length; index++)
                try { Environment.SetEnvironmentVariable(Names[index], before[index]); } catch (Exception error) { errors.Add(error); }
            if (errors.Count != 0)
            {
                if (primary is not null) errors.Insert(0, primary);
                throw new AggregateException("Diagnostics environment restoration failed.", errors);
            }
        }
    }
}
