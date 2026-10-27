using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class UnknownPacketAndFieldLoggerTests
{
    [Fact]
    public void MissingOptionsRefuseWithoutAnOutputOwner()
    {
        Assert.False(UnknownPacketAndFieldLogger.TryCreate(null!, out var logger, out var reason));
        Assert.Null(logger);
        Assert.Equal("invalid_options", reason);
    }

    [Fact]
    public void DisabledLoggerDoesNotValidateOrCreateAnOutputRoute()
    {
        var path = Path.Combine(Path.GetTempPath(), "wolf-wire-disabled-" + Guid.NewGuid().ToString("N"), "missing");
        Assert.True(UnknownPacketAndFieldLogger.TryCreate(new() { OutputDirectory = path, MaxRecords = 0 }, out var logger, out var reason), reason);
        Assert.NotNull(logger);
        using (logger)
        {
            Assert.False(logger.IsEnabled);
            Assert.Null(logger.OutputPath);
            Observe(logger, 4999, [0xAA]);
        }
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    [Theory]
    [InlineData("records", 0)]
    [InlineData("records", 100001)]
    [InlineData("bytes", 255)]
    [InlineData("bytes", 16777217)]
    [InlineData("rate", 0)]
    [InlineData("rate", 601)]
    [InlineData("signatures", 0)]
    [InlineData("signatures", 4097)]
    [InlineData("payload", 0)]
    [InlineData("payload", 16385)]
    [InlineData("fields", 0)]
    [InlineData("fields", 257)]
    [InlineData("raw", 0)]
    [InlineData("raw", 257)]
    public void EnabledInvalidLimitsRefuseBeforeOutputCreation(string limit, int value)
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var options = new UnknownPacketAndFieldLoggerOptions { Enabled = true, OutputDirectory = files.Root };
            options = limit switch
            {
                "records" => options with { MaxRecords = value }, "bytes" => options with { MaxFileBytes = value },
                "rate" => options with { MaxRecordsPerMinute = value }, "signatures" => options with { MaxDistinctSignatures = value },
                "payload" => options with { MaxPayloadBytes = value }, "fields" => options with { MaxFields = value },
                "raw" => options with { MaxRawPayloadBytes = value }, _ => throw new ArgumentOutOfRangeException(nameof(limit)),
            };
            Assert.False(UnknownPacketAndFieldLogger.TryCreate(options, out var logger, out var reason));
            Assert.Null(logger);
            Assert.Equal("invalid_limits_or_opcode_allowlist", reason);
            Assert.Empty(Directory.EnumerateFileSystemEntries(files.Root));
        });
    }

    [Theory]
    [InlineData(2005)]
    [InlineData(2065)]
    [InlineData(2132)]
    [InlineData(2179)]
    [InlineData(2350)]
    [InlineData(4999)]
    public void UnconfirmedOrSensitiveOpcodesCannotBeEnabledForRawOrProtobuf(int opcode)
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            foreach (var raw in new[] { false, true })
            {
                var options = new UnknownPacketAndFieldLoggerOptions { Enabled = true, OutputDirectory = files.Root };
                options = raw ? options with { RawPayloadOpcodes = [(ushort)opcode] } : options with { ProtobufOpcodes = [(ushort)opcode] };
                Assert.False(UnknownPacketAndFieldLogger.TryCreate(options, out var logger, out var reason));
                Assert.Null(logger);
                Assert.Equal("invalid_limits_or_opcode_allowlist", reason);
            }
            Assert.Empty(Directory.EnumerateFileSystemEntries(files.Root));
        });
    }

    [Fact]
    public void NullOrOversizedAllowlistsRefuseWithoutOpeningOutput()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var options = new UnknownPacketAndFieldLoggerOptions { Enabled = true, OutputDirectory = files.Root };
            foreach (var invalid in new[] { options with { ProtobufOpcodes = null! }, options with { RawPayloadOpcodes = null! },
                options with { ProtobufOpcodes = Enumerable.Repeat((ushort)2419, 33).ToArray() },
                options with { RawPayloadOpcodes = Enumerable.Repeat((ushort)2419, 33).ToArray() } })
            {
                Assert.False(UnknownPacketAndFieldLogger.TryCreate(invalid, out var logger, out var reason));
                Assert.Null(logger);
                Assert.Equal("invalid_limits_or_opcode_allowlist", reason);
            }
            Assert.Empty(Directory.EnumerateFileSystemEntries(files.Root));
        });
    }

    [Fact]
    public void ExistingFileOutputRouteRefusesWithoutOverwritingIt()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            byte[] original = [0x53, 0x41, 0x46, 0x45];
            var path = files.SeedFile("existing.bin", original);
            Assert.False(UnknownPacketAndFieldLogger.TryCreate(new() { Enabled = true, OutputDirectory = path }, out var logger, out var reason));
            Assert.Null(logger);
            Assert.Equal("private_output_unavailable", reason);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Single(Directory.EnumerateFileSystemEntries(files.Root));
        });
    }

    [Fact]
    public void MissingParentAndRelativeOutputRoutesAreNotCreated()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            foreach (var path in new[] { Path.Combine(files.Root, "missing"), "relative-wire-output" })
            {
                Assert.False(UnknownPacketAndFieldLogger.TryCreate(new() { Enabled = true, OutputDirectory = path }, out var logger, out var reason));
                Assert.Null(logger);
                Assert.Equal("private_output_unavailable", reason);
            }
            Assert.Empty(Directory.EnumerateFileSystemEntries(files.Root));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactConfiguredLimitEndpointsRemainUsable(bool upper)
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var options = upper
                ? new UnknownPacketAndFieldLoggerOptions { MaxRecords = 100000, MaxFileBytes = 16777216,
                    MaxRecordsPerMinute = 600, MaxDistinctSignatures = 4096, MaxPayloadBytes = 16384, MaxFields = 256, MaxRawPayloadBytes = 256 }
                : new UnknownPacketAndFieldLoggerOptions { MaxRecords = 1, MaxFileBytes = 256,
                    MaxRecordsPerMinute = 1, MaxDistinctSignatures = 1, MaxPayloadBytes = 1, MaxFields = 1, MaxRawPayloadBytes = 1 };
            var logger = files.Open(options);
            Assert.Empty(files.Read(logger));
        });
    }
}
