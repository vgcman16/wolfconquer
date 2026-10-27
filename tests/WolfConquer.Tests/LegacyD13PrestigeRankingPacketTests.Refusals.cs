using System.Collections;
using System.Buffers.Binary;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyD13PrestigeRankingPacketTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(int.MaxValue)]
    public void CountMustMatchCompletePhysicalRowsBeforeAllocation(int count)
    {
        var bytes = TwoRowGolden();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(13), count);
        Assert.False(LegacyD13PrestigeRankingPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(7)]
    public void CodecDoesNotReinterpretOtherRankingActions(byte action)
    {
        var bytes = TwoRowGolden();
        bytes[4] = action;
        Assert.False(LegacyD13PrestigeRankingPacket.TryParse(ReadFrame(bytes), out _));
    }

    [Fact]
    public void WrongOpcodeShortHeadersAndOld109ByteRowsAreRefused()
    {
        var wrongOpcode = TwoRowGolden();
        BinaryPrimitives.WriteUInt16LittleEndian(wrongOpcode.AsSpan(2), 2166);
        Assert.False(LegacyD13PrestigeRankingPacket.TryParse(ReadFrame(wrongOpcode), out _));

        foreach (var payloadLength in new[] { 13, 16, 17 + 109, 17 + 113 + 1 })
        {
            var writer = new PacketWriter(2165, payloadLength);
            writer.Payload[0] = 6;
            writer.WriteInt32(9, 1);
            Assert.False(LegacyD13PrestigeRankingPacket.TryParse(ReadFrame(writer.ToArray()), out _));
        }
    }

    [Theory]
    [InlineData(31, 32, 16)]
    [InlineData(33, 32, 16)]
    [InlineData(32, 31, 16)]
    [InlineData(32, 33, 16)]
    [InlineData(32, 32, 0)]
    [InlineData(32, 32, 15)]
    [InlineData(32, 32, 17)]
    public void RawFieldWidthsCannotBeSilentlyPaddedOrTruncated(int nameLength, int secondaryLength, int tailLength)
    {
        Assert.Throws<ArgumentException>(() => new LegacyD13PrestigeRankingEntry(
            1, 2, 3, 4, new byte[nameLength], new byte[secondaryLength], 5, 6, 7, new byte[tailLength], 8));
    }

    [Fact]
    public void InvalidCallerRowsAndImpossibleLengthsDoNotProduceFrameBytes()
    {
        Assert.Throws<ArgumentNullException>(() => (TwoRowPacket() with { Entries = null! }).ToFrameBytes());
        Assert.Throws<ArgumentNullException>(() => (TwoRowPacket() with { Entries = [null!] }).ToFrameBytes());
        Assert.Throws<ArgumentOutOfRangeException>(() => LegacyD13PrestigeRankingPacket.PayloadLengthForEntryCount(-1));
        Assert.Throws<OverflowException>(() => LegacyD13PrestigeRankingPacket.PayloadLengthForEntryCount(int.MaxValue));
        Assert.Throws<ArgumentOutOfRangeException>(() => (TwoRowPacket() with
        {
            Entries = Enumerable.Repeat(TwoRowPacket().Entries[0], 61).ToArray(),
        }).ToFrameBytes());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(61)]
    [InlineData(int.MaxValue)]
    public void InvalidCallerCountRefusesBeforeAllocationIndexingOrEnumeration(int count)
    {
        var packet = TwoRowPacket() with { Entries = new UntouchableEntryList(count) };
        Assert.Throws<ArgumentOutOfRangeException>(() => packet.ToFrameBytes());
    }

    private sealed class UntouchableEntryList(int count) : IReadOnlyList<LegacyD13PrestigeRankingEntry>
    {
        public int Count => count;
        public LegacyD13PrestigeRankingEntry this[int index] =>
            throw new InvalidOperationException("The invalid-size list indexer must not be consulted.");
        public IEnumerator<LegacyD13PrestigeRankingEntry> GetEnumerator() =>
            throw new InvalidOperationException("The invalid-size list enumerator must not be consulted.");
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
