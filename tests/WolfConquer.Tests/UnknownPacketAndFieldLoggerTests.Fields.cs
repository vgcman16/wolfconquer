using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class UnknownPacketAndFieldLoggerTests
{
    [Fact]
    public void KnownD13TagsDoNotCreateAnUnknownFieldRecord()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open(new() { ProtobufOpcodes = [2419] });
            byte[] payload = [0x08, 0, 0x10, 1, 0x18, 2, 0x20, 3, 0x28, 4, 0x30, 0, 0x38, 6, 0x40, 7, 0x42, 2, 8, 9];
            Observe(logger, 2419, payload, unknown: false, segmented: true);
            Observe(logger, 2419, payload, unknown: true, segmented: true);
            var record = Assert.Single(files.Read(logger));
            Assert.Equal("VisibleDx8D13.CMsgRandomDailyTask.fields1-8", record.GetProperty("schema").GetString());
            Assert.Equal("complete", record.GetProperty("scanStatus").GetString());
            var fields = record.GetProperty("fields").EnumerateArray().ToArray();
            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 8 }, fields.Select(field => field.GetProperty("number").GetInt32()));
            Assert.All(fields, field => Assert.True(field.GetProperty("known").GetBoolean()));
            AssertField(fields[7], 8, 0, 14, 15, 2, null, true);
            AssertField(fields[8], 8, 2, 16, 17, 4, 2, true);
            AssertMetadataOnly(record);
        });
    }

    [Fact]
    public void UnknownLengthBodyStaysOpaqueAcrossSequenceSegments()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open(new() { ProtobufOpcodes = [2419] });
            Observe(logger, 2419, [0x08, 0x00, 0x4A, 0x03, 0x73, 0x65, 0x63], unknown: false, segmented: true);
            var record = Assert.Single(files.Read(logger));
            Assert.False(record.GetProperty("unknownOpcode").GetBoolean());
            Assert.Equal("complete", record.GetProperty("scanStatus").GetString());
            var fields = record.GetProperty("fields").EnumerateArray().ToArray();
            Assert.Equal(2, fields.Length);
            AssertField(fields[0], 1, 0, 0, 1, 2, null, true);
            AssertField(fields[1], 9, 2, 2, 3, 5, 3, false);
            AssertMetadataOnly(record);
            Assert.DoesNotContain("sec", record.GetRawText(), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void WrongKnownWireAndUnknownFixedWidthsAreDescribedWithoutValues()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open(new() { ProtobufOpcodes = [2419] });
            Observe(logger, 2419, [0x0A, 0x01, 0xFF], unknown: false);
            Observe(logger, 2419, [0x08, 0, 0x49, 1, 2, 3, 4, 5, 6, 7, 8, 0x55, 9, 10, 11, 12], unknown: false);
            var records = files.Read(logger);
            Assert.Equal(2, records.Length);
            AssertField(Assert.Single(records[0].GetProperty("fields").EnumerateArray()), 1, 2, 0, 1, 3, 1, false);
            var fields = records[1].GetProperty("fields").EnumerateArray().ToArray();
            Assert.Equal(3, fields.Length);
            AssertField(fields[1], 9, 1, 2, 3, 9, null, false);
            AssertField(fields[2], 10, 5, 11, 12, 5, null, false);
            Assert.All(records, AssertMetadataOnly);
        });
    }

    [Theory]
    [InlineData("00", "malformed", 0)]
    [InlineData("80", "truncated", 0)]
    [InlineData("08", "truncated", 1)]
    [InlineData("0A02FF", "truncated", 1)]
    [InlineData("48FFFFFFFFFFFFFFFFFF02", "malformed", 1)]
    [InlineData("4E", "malformed", 1)]
    [InlineData("4B", "unsupported_group", 1)]
    [InlineData("4C", "unsupported_group", 1)]
    [InlineData("4D0102", "truncated", 1)]
    public void MalformedOrIncompleteWireTailsAreFiniteObservations(string hex, string status, int count)
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open(new() { ProtobufOpcodes = [2419] });
            Observe(logger, 2419, Convert.FromHexString(hex), unknown: false, segmented: true);
            var record = Assert.Single(files.Read(logger));
            Assert.Equal(status, record.GetProperty("scanStatus").GetString());
            Assert.Equal(count, record.GetProperty("fields").GetArrayLength());
            AssertMetadataOnly(record);
        });
    }

    [Fact]
    public void OptedSchemaListIsCapturedIndependentlyOfCallerMutation()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var opcodes = new List<ushort> { 2419 };
            var logger = files.Open(new() { ProtobufOpcodes = opcodes });
            opcodes.Clear();
            opcodes.Add(2179);
            Observe(logger, 2419, [0x08, 0, 0x48, 1], unknown: false);
            var record = Assert.Single(files.Read(logger));
            Assert.Equal("complete", record.GetProperty("scanStatus").GetString());
            Assert.False(record.GetProperty("fields")[1].GetProperty("known").GetBoolean());
        });
    }

    private static void AssertField(JsonElement field, int number, int wire, int tagOffset, int valueOffset,
        int encodedLength, int? dataLength, bool known)
    {
        Assert.Equal(number, field.GetProperty("number").GetInt32());
        Assert.Equal(wire, field.GetProperty("wireType").GetInt32());
        Assert.Equal(tagOffset, field.GetProperty("tagOffset").GetInt32());
        Assert.Equal(valueOffset, field.GetProperty("valueOffset").GetInt32());
        Assert.Equal(encodedLength, field.GetProperty("encodedLength").GetInt32());
        if (dataLength is null) Assert.Equal(JsonValueKind.Null, field.GetProperty("dataLength").ValueKind);
        else Assert.Equal(dataLength.Value, field.GetProperty("dataLength").GetInt32());
        Assert.Equal(known, field.GetProperty("known").GetBoolean());
        Assert.Equal(new[] { "dataLength", "encodedLength", "known", "number", "tagOffset", "valueOffset", "wireType" },
            field.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }
}
