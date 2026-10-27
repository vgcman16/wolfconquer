using System.Text.Json;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class UnknownPacketAndFieldLoggerTests
{
    [Fact]
    public void UnknownPayloadIsOpaqueMetadataByDefault()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open();
            Observe(logger, 4999, [0x12, 0x06, 0x73, 0x65, 0x63, 0x72, 0x65, 0x74], segmented: true);
            var record = Assert.Single(files.Read(logger));
            Assert.Equal(1, record.GetProperty("version").GetInt32());
            Assert.Equal("VisibleDx8D13", record.GetProperty("profile").GetString());
            Assert.Equal(4999, record.GetProperty("opcode").GetInt32());
            Assert.Equal(12, record.GetProperty("headerLength").GetInt32());
            Assert.Equal(8, record.GetProperty("payloadLength").GetInt32());
            Assert.True(record.GetProperty("unknownOpcode").GetBoolean());
            Assert.Equal("coherent", record.GetProperty("frameStatus").GetString());
            Assert.Equal("not_requested", record.GetProperty("scanStatus").GetString());
            Assert.Equal(JsonValueKind.Null, record.GetProperty("schema").ValueKind);
            Assert.Empty(record.GetProperty("fields").EnumerateArray());
            AssertMetadataOnly(record);
            Assert.DoesNotContain("secret", record.GetRawText(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("736563726574", record.GetRawText(), StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void KnownUnconfiguredPacketsAndInvalidCallArgumentsAreIgnored()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open();
            Observe(logger, 2419, [0x08, 0x00], unknown: false);
            Observe(logger, 4999, [0xAA], profile: (ConquerProtocolProfile)999);
            logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13, Frame(4999, [0xAA]), true, null!, new WorldSessionStore());
            logger.RecordPacket(ConquerProtocolProfile.VisibleDx8D13, Frame(4999, [0xAA]), true, new ConquerTcpConnection(null, null), null!);
            Assert.Empty(files.Read(logger));
            Observe(logger, 4999, [0xBB]); // disposed output must remain inert
            Assert.False(logger.IsEnabled);
        });
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(1001)]
    [InlineData(1052)]
    [InlineData(1059)]
    [InlineData(1637)]
    [InlineData(1942)]
    [InlineData(2000)]
    [InlineData(2002)]
    [InlineData(2005)]
    [InlineData(2036)]
    [InlineData(2052)]
    [InlineData(2065)]
    [InlineData(2150)]
    [InlineData(2179)]
    [InlineData(2255)]
    public void AuthenticationPinNamingAndTextFamiliesProduceNoDiagnosticRecord(int opcode)
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open(new() { ProtobufOpcodes = [2419], RawPayloadOpcodes = [2419] });
            Observe(logger, (ushort)opcode, [0x08, 0x01, 0x12, 0x04, 0x50, 0x49, 0x4E, 0x21]);
            Assert.Empty(files.Read(logger));
        });
    }

    [Fact]
    public void D11OpcodeCoincidenceDoesNotSelectTheD13Schema()
    {
        WorldWireDiagnosticTestFiles.Run(files =>
        {
            var logger = files.Open(new() { ProtobufOpcodes = [2419], RawPayloadOpcodes = [2419] });
            Observe(logger, 2419, [0x08, 0x00, 0x48, 0x01], profile: ConquerProtocolProfile.RootD11_7899);
            var record = Assert.Single(files.Read(logger));
            Assert.Equal("RootD11_7899", record.GetProperty("profile").GetString());
            Assert.Equal("opaque_profile_or_opcode", record.GetProperty("scanStatus").GetString());
            Assert.Equal(JsonValueKind.Null, record.GetProperty("schema").ValueKind);
            Assert.Empty(record.GetProperty("fields").EnumerateArray());
            AssertMetadataOnly(record);
        });
    }
}
