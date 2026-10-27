using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class D13RandomDailyTaskPacketTests
{
    [Fact]
    public void ObservedActionSixRequestHasLiteralNativePayloadAnd2419Envelope()
    {
        var expected = Convert.FromHexString("080610001800");
        var packet = new D13RandomDailyTaskPacket(6, new(Field2: 0, Field3: 0));

        Assert.Equal(expected, packet.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString("0A007309080610001800"), packet.ToFrameBytes());
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(expected, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(6U, parsed.Action);
        Assert.Equal(new D13RandomDailyTaskFields(Field2: 0, Field3: 0), parsed.Fields);
        Assert.Empty(parsed.Values8.ToArray());
        Assert.Equal(expected, parsed.ToPayloadBytes());
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownFrame(Decode(packet.ToFrameBytes()), out parsed));
        Assert.NotNull(parsed);
        Assert.Equal(expected, parsed.ToPayloadBytes());
    }

    [Fact]
    public void RequiredActionZeroIsPresentAndDoesNotBecomeAnAbsentField()
    {
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(Convert.FromHexString("0800"), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(0U, parsed.Action);
        Assert.Equal(default(D13RandomDailyTaskFields), parsed.Fields);
        Assert.Equal(Convert.FromHexString("0800"), parsed.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString("08FFFFFFFF0F"), new D13RandomDailyTaskPacket(uint.MaxValue).ToPayloadBytes());
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(Convert.FromHexString("08FFFFFFFF0F"), out parsed));
        Assert.NotNull(parsed);
        Assert.Equal(uint.MaxValue, parsed.Action);
        foreach (var missing in new[] { "", "1000", "4000" })
        {
            Assert.False(D13RandomDailyTaskPacket.TryParseKnownPayload(Convert.FromHexString(missing), out parsed));
            Assert.Null(parsed);
        }
    }

    [Theory]
    [InlineData(2, "08001000")]
    [InlineData(3, "08001800")]
    [InlineData(4, "08002000")]
    [InlineData(5, "08002800")]
    [InlineData(6, "08003000")]
    [InlineData(7, "08003800")]
    public void EveryOptionalScalarRetainsExplicitZeroDistinctFromAbsence(int field, string hex)
    {
        var expectedFields = field switch
        {
            2 => new D13RandomDailyTaskFields(Field2: 0),
            3 => new D13RandomDailyTaskFields(Field3: 0),
            4 => new D13RandomDailyTaskFields(Field4: 0),
            5 => new D13RandomDailyTaskFields(Field5: 0),
            6 => new D13RandomDailyTaskFields(Field6: 0),
            7 => new D13RandomDailyTaskFields(Field7: 0),
            _ => throw new InvalidOperationException("Unexpected fixture field."),
        };
        var expected = Convert.FromHexString(hex);
        Assert.Equal(expected, new D13RandomDailyTaskPacket(0, expectedFields).ToPayloadBytes());
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(expected, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(expectedFields, parsed.Fields);
        Assert.Equal(expected, parsed.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString("0800"), new D13RandomDailyTaskPacket(0).ToPayloadBytes());
    }

    [Theory]
    [InlineData(-1, "FFFFFFFFFFFFFFFFFF01")]
    [InlineData(int.MinValue, "80808080F8FFFFFFFF01")]
    [InlineData(int.MaxValue, "FFFFFFFF07")]
    [InlineData(0, "00")]
    public void FieldSixUsesOrdinaryInt32VarintsWithTenByteNegativeSignExtension(int value, string encoded)
    {
        var expected = Convert.FromHexString("080630" + encoded);
        Assert.Equal(expected, new D13RandomDailyTaskPacket(6, new(Field6: value)).ToPayloadBytes());
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(expected, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(value, parsed.Fields.Field6);
        Assert.Equal(expected, parsed.ToPayloadBytes());
    }

    [Theory]
    [InlineData(-1, "FFFFFFFF0F", "FFFFFFFFFFFFFFFFFF01")]
    [InlineData(int.MinValue, "8080808008", "80808080F8FFFFFFFF01")]
    public void SupportedDwordNegativeInputRetainsBitsAndWritesNativeSignExtendedForm(
        int expected, string input, string canonical)
    {
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(Convert.FromHexString("080630" + input), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(expected, parsed.Fields.Field6);
        Assert.Equal(Convert.FromHexString("080630" + canonical), parsed.ToPayloadBytes());
    }

    [Fact]
    public void NativeWriterOrdersAllPresentScalarsThenUnpackedRepeatedValues()
    {
        const string canonical = "080610FFFFFFFF0F1800208001280130FFFFFFFFFFFFFFFFFF0138004000407F40800140FFFFFFFF0F";
        const string reversed = "40FFFFFFFF0F408001407F4000380030FFFFFFFFFFFFFFFFFF012801208001180010FFFFFFFF0F0806";
        var fields = new D13RandomDailyTaskFields(uint.MaxValue, 0, 128, 1, -1, 0);
        var packet = new D13RandomDailyTaskPacket(6, fields, [0, 127, 128, uint.MaxValue]);
        Assert.Equal(Convert.FromHexString(canonical), packet.ToPayloadBytes());
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(Convert.FromHexString(reversed), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(fields, parsed.Fields);
        Assert.Equal(new uint[] { uint.MaxValue, 128, 127, 0 }, parsed.Values8.ToArray());
        Assert.Equal(Convert.FromHexString(
            "080610FFFFFFFF0F1800208001280130FFFFFFFFFFFFFFFFFF01380040FFFFFFFF0F408001407F4000"), parsed.ToPayloadBytes());
    }

    [Fact]
    public void DuplicateKnownScalarsUseLastValueAndLastPresenceWithoutDuplicatingOutput()
    {
        const string input = "080110092001300108001000200030FFFFFFFFFFFFFFFFFF0138053800";
        const string expected = "08001000200030FFFFFFFFFFFFFFFFFF013800";
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(Convert.FromHexString(input), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(0U, parsed.Action);
        Assert.Equal(new D13RandomDailyTaskFields(Field2: 0, Field4: 0, Field6: -1, Field7: 0), parsed.Fields);
        Assert.Equal(Convert.FromHexString(expected), parsed.ToPayloadBytes());
    }

    [Theory]
    [InlineData("08064000407F40800140FFFFFFFF0F")]
    [InlineData("08064209007F8001FFFFFFFF0F")]
    [InlineData("0806400042037F800140FFFFFFFF0F")]
    public void PackedUnpackedAndInterleavedInputPreserveEncounterOrderAndWriteUnpacked(string input)
    {
        var expected = Convert.FromHexString("08064000407F40800140FFFFFFFF0F");
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(Convert.FromHexString(input), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(new uint[] { 0, 127, 128, uint.MaxValue }, parsed.Values8.ToArray());
        Assert.Equal(expected, parsed.ToPayloadBytes());
    }

    [Fact]
    public void EmptyPackedSegmentsDoNotCreateAValueOrReorderFollowingSegments()
    {
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(
            Convert.FromHexString("0806420040014202020340044200420105"), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(new uint[] { 1, 2, 3, 4, 5 }, parsed.Values8.ToArray());
        Assert.Equal(Convert.FromHexString("080640014002400340044005"), parsed.ToPayloadBytes());
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(Convert.FromHexString("08064200"), out parsed));
        Assert.NotNull(parsed);
        Assert.Empty(parsed.Values8.ToArray());
        Assert.Equal(Convert.FromHexString("0806"), parsed.ToPayloadBytes());
    }

    [Fact]
    public void ConstructionParsingAndReturnedBytesDoNotShareCallerMutableStorage()
    {
        uint[] values = [127, 128];
        var packet = new D13RandomDailyTaskPacket(6, new(Field2: 0), values);
        values[0] = uint.MaxValue;
        var expected = Convert.FromHexString("08061000407F408001");
        var output = packet.ToPayloadBytes();
        Assert.Equal(expected, output);
        output[0] = 0;
        Assert.Equal(expected, packet.ToPayloadBytes());
        var frame = packet.ToFrameBytes();
        frame[^1] = 0;
        Assert.Equal(expected, packet.ToFrameBytes()[4..]);

        var input = Convert.FromHexString("080642037F8001");
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(input, out var parsed));
        Assert.NotNull(parsed);
        Array.Fill(input, (byte)0);
        Assert.Equal(new uint[] { 127, 128 }, parsed.Values8.ToArray());
        Assert.Equal(Convert.FromHexString("0806407F408001"), parsed.ToPayloadBytes());
    }

    private static PacketFrame Decode(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _, ushort.MaxValue));
        return frame;
    }
}
