using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMailboxModeEnvelopeTests
{
    [Theory]
    [InlineData(268_435_454U)] // Last count whose old count*8+10 arithmetic fits int.
    [InlineData(268_435_455U)] // Addition overflow with multiplication still in range.
    [InlineData(268_435_456U)] // Multiplication overflow.
    [InlineData(536_870_912U)] // Multiplication would wrap to zero if unchecked.
    [InlineData(2_147_483_647U)]
    [InlineData(2_147_483_648U)]
    [InlineData(uint.MaxValue)]
    public void HugeDeclaredCountsRejectWithoutThrowingOrPublishing(uint count) =>
        AssertRejected(Frame(Raw((ushort)LegacyMailboxModeType.ClaimAll, count, [])));

    [Theory]
    [InlineData(0U, 8)]
    [InlineData(2U, 8)]
    [InlineData(1U, 0)]
    [InlineData(1U, 7)]
    [InlineData(1U, 9)]
    public void CountMustExactlyDescribeWholePhysicalUlongs(uint count, int itemBytes) =>
        AssertRejected(Frame(Raw((ushort)LegacyMailboxModeType.ClaimAll, count, new byte[itemBytes])));

    [Theory]
    [InlineData((ushort)0)]
    [InlineData((ushort)8)]
    [InlineData(ushort.MaxValue)]
    public void UndefinedModesStillReject(ushort type) => AssertRejected(Frame(Raw(type, 0, [])));

    [Fact]
    public void DefaultAndEveryShortCoherentPayloadReject()
    {
        AssertRejected(default);
        for (var length = 0; length < LegacyMailboxModePacket.HeaderPayloadLength; length++)
        {
            var bytes = new PacketWriter(LegacyMailboxModePacket.Opcode, length).ToArray();
            AssertRejected(Frame(bytes));
        }
    }

    [Theory]
    [InlineData(LegacyMailboxModeType.Content)]
    [InlineData(LegacyMailboxModeType.Delete)]
    [InlineData(LegacyMailboxModeType.ClaimMoney)]
    [InlineData(LegacyMailboxModeType.ClaimItem)]
    [InlineData(LegacyMailboxModeType.ClaimRecordType)]
    [InlineData(LegacyMailboxModeType.DeleteClaim)]
    [InlineData(LegacyMailboxModeType.ClaimAll)]
    public void EveryDefinedModePreservesZeroAndFullWidthItemLists(LegacyMailboxModeType type)
    {
        foreach (var items in new[] { Array.Empty<ulong>(), new[] { 0UL, 0x0102030405060708UL, ulong.MaxValue } })
        {
            var original = new LegacyMailboxModePacket(type, uint.MaxValue, items);
            var bytes = original.ToFrameBytes();
            Assert.True(LegacyMailboxModePacket.TryParse(Frame(bytes), out var parsed));
            Assert.Equal(type, parsed.Type);
            Assert.Equal(uint.MaxValue, parsed.Id);
            Assert.Equal(items, parsed.Items);
            Assert.Equal(bytes, parsed.ToFrameBytes());
        }
    }

    [Fact]
    public void LargestAlignedUshortFrameRemainsParseableWithoutChangingTransportLimits()
    {
        const int count = (ushort.MaxValue - PacketHeader.Size - LegacyMailboxModePacket.HeaderPayloadLength) / sizeof(ulong);
        var values = Enumerable.Range(0, count).Select(index => ((ulong)index << 32) | ~(uint)index).ToArray();
        var itemBytes = new byte[count * sizeof(ulong)];
        for (var index = 0; index < count; index++)
            BinaryPrimitives.WriteUInt64LittleEndian(itemBytes.AsSpan(index * sizeof(ulong)), values[index]);
        var bytes = Raw((ushort)LegacyMailboxModeType.ClaimAll, count, itemBytes);
        Assert.Equal(65_534, bytes.Length);

        Assert.True(LegacyMailboxModePacket.TryParse(Frame(bytes), out var parsed));

        Assert.Equal(values, parsed.Items);
        Assert.Equal((uint)count, parsed.Count);
        Assert.Equal(uint.MaxValue, parsed.Id);
        var trailing = new byte[ushort.MaxValue];
        bytes.CopyTo(trailing, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(trailing, ushort.MaxValue);
        AssertRejected(Frame(trailing)); // Coherent envelope, but one non-item trailing byte.
    }

    [Theory]
    [InlineData(EnvelopeDefect.OuterOpcode)]
    [InlineData(EnvelopeDefect.OuterLength)]
    [InlineData(EnvelopeDefect.EmbeddedOpcode)]
    [InlineData(EnvelopeDefect.EmbeddedLength)]
    [InlineData(EnvelopeDefect.MissingPhysical)]
    [InlineData(EnvelopeDefect.ShortPhysical)]
    [InlineData(EnvelopeDefect.TrailingPhysical)]
    [InlineData(EnvelopeDefect.ShortPayload)]
    [InlineData(EnvelopeDefect.TrailingPayload)]
    [InlineData(EnvelopeDefect.DifferentPayload)]
    [InlineData(EnvelopeDefect.PhysicalShortMetadata)]
    [InlineData(EnvelopeDefect.PhysicalLongMetadata)]
    [InlineData(EnvelopeDefect.PayloadShortMetadata)]
    [InlineData(EnvelopeDefect.PayloadLongMetadata)]
    public void IncoherentEnvelopeRejectsWithoutPublishing(EnvelopeDefect defect) => AssertRejected(Malformed(defect));

    private static byte[] Raw(ushort type, uint count, byte[] itemBytes)
    {
        var writer = new PacketWriter(LegacyMailboxModePacket.Opcode,
            LegacyMailboxModePacket.HeaderPayloadLength + itemBytes.Length, maxFrameSize: ushort.MaxValue);
        writer.WriteUInt16(0, type);
        writer.WriteUInt32(2, uint.MaxValue);
        writer.WriteUInt32(6, count);
        itemBytes.AsSpan().CopyTo(writer.Payload[LegacyMailboxModePacket.HeaderPayloadLength..]);
        return writer.ToArray();
    }

    private static PacketFrame Frame(byte[] bytes)
    {
        Assert.True(PacketHeader.TryRead(bytes, out var header));
        return new(header, new ReadOnlySequence<byte>(bytes), new ReadOnlySequence<byte>(bytes[PacketHeader.Size..]));
    }

    private static void AssertRejected(PacketFrame frame)
    {
        Assert.False(LegacyMailboxModePacket.TryParse(frame, out var packet));
        Assert.Null(packet);
    }
}
