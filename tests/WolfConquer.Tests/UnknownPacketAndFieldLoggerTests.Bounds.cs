using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class UnknownPacketAndFieldLoggerTests
{
    [Theory]
    [InlineData("records")]
    [InlineData("rate")]
    [InlineData("signatures")]
    public void IndependentBudgetsStopNewRecords(string budget)
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var options = new UnknownPacketAndFieldLoggerOptions();
            options = budget switch
            {
                "records" => options with { MaxRecords = 1 }, "rate" => options with { MaxRecordsPerMinute = 1 },
                "signatures" => options with { MaxDistinctSignatures = 1 }, _ => throw new ArgumentOutOfRangeException(nameof(budget)),
            };
            var logger = files.Open(options);
            Observe(logger, 4998, [0xAA]);
            Observe(logger, 4999, [0xBB]);
            var record = Assert.Single(files.Read(logger));
            Assert.Equal(4998, record.GetProperty("opcode").GetInt32());
        });
    }

    [Fact]
    public void FileByteBudgetNeverLeavesAPartialJsonLine()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open(new() { MaxFileBytes = 1024, MaxRecordsPerMinute = 600 });
            for (var index = 0; index < 50; index++) Observe(logger, (ushort)(4900 + index), [0xAA]);
            var records = files.Read(logger);
            Assert.InRange(records.Length, 1, 49);
            Assert.InRange(new FileInfo(logger.OutputPath!).Length, 1, 1024);
        });
    }

    [Fact]
    public void StructuralDedupeIgnoresScalarValuesAndTimestamps()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open(new() { ProtobufOpcodes = [2419] });
            Observe(logger, 2419, [0x08, 1, 0x48, 2], unknown: false);
            Observe(logger, 2419, [0x08, 3, 0x48, 4], unknown: false);
            Assert.Single(files.Read(logger));
        });
    }

    [Fact]
    public void PayloadAndFieldLimitsRemainDistinguishableFromMalformedInput()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var bytes = files.Open(new() { ProtobufOpcodes = [2419], MaxPayloadBytes = 1 });
            var fields = files.Open(new() { ProtobufOpcodes = [2419], MaxFields = 1 });
            Observe(bytes, 2419, [0x08, 0x00], unknown: false);
            Observe(fields, 2419, [0x08, 0x00, 0x48, 0x01], unknown: false);
            var byteRecord = Assert.Single(files.Read(bytes));
            var fieldRecord = Assert.Single(files.Read(fields));
            Assert.Equal("scan_limit", byteRecord.GetProperty("scanStatus").GetString());
            AssertField(Assert.Single(byteRecord.GetProperty("fields").EnumerateArray()), 1, 0, 0, 1, 1, null, true);
            Assert.Equal("field_limit", fieldRecord.GetProperty("scanStatus").GetString());
            AssertField(Assert.Single(fieldRecord.GetProperty("fields").EnumerateArray()), 1, 0, 0, 1, 2, null, true);
        });
    }

    [Fact]
    public void InconsistentFrameNeverSuppliesStructuralOrRawPayload()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open(new() { ProtobufOpcodes = [2419], RawPayloadOpcodes = [2419] });
            var valid = Frame(2419, [0x08, 0, 0x48, 1]);
            var frame = valid with { Header = valid.Header with { Length = 7 } };
            logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13, frame, true,
                new ConquerTcpConnection(null, null), new WorldSessionStore());
            var record = Assert.Single(files.Read(logger));
            Assert.Equal("inconsistent", record.GetProperty("frameStatus").GetString());
            Assert.Equal("malformed", record.GetProperty("scanStatus").GetString());
            Assert.Empty(record.GetProperty("fields").EnumerateArray());
            AssertMetadataOnly(record);
        });
    }

    [Fact]
    public void IndependentLoggerInstancesOwnDistinctOutputs()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var first = files.Open();
            var second = files.Open();
            Assert.NotEqual(first.OutputPath, second.OutputPath);
            Observe(first, 4998, [0xAA]);
            Observe(second, 4999, [0xBB]);
            Assert.Equal(4998, Assert.Single(files.Read(first)).GetProperty("opcode").GetInt32());
            Assert.Equal(4999, Assert.Single(files.Read(second)).GetProperty("opcode").GetInt32());
        });
    }
}
