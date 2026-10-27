using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyD13PrestigeRankingPacketTests
{
    // Synthetic byte golden from the documented physical offsets, not a client capture.
    private static byte[] TwoRowGolden() => Convert.FromHexString(
        "F7007508060403020106050708020000000C0B0A09" +
        "01010203040506070800B4C40444332211" +
        "4865726F00000000" + "0000000000000000" + "0000000000000000" + "0000000000000000" +
        "FFD0800000000000000000000000000000000000000000000000000000000000" +
        "040302011413121124232221101112131415161718191A1B1C1D1E1F34333231" +
        "0200000000000000800BB4C404FFFFFFFF" +
        "202122232425262728292A2B2C2D2E2F303132333435363738393A3B3C3D3E3F" +
        "404142434445464748494A4B4C4D4E4F505152535455565758595A5B5C5D5E5F" +
        "FFFFFFFF0000008000000010606162636465666768696A6B6C6D6E6FA4A3A2A1");

    private static LegacyD13PrestigeRankingPacket TwoRowPacket() => new(
        RankType: 0x01020304,
        RegisteredCount: 0x0506,
        Page: 7,
        Finish: 8,
        HeaderValue: 0x090a0b0c,
        Entries:
        [
            new LegacyD13PrestigeRankingEntry(
                1, 0x0807060504030201, 80_000_000, 0x11223344,
                Convert.FromHexString("4865726F00000000" + "0000000000000000" + "0000000000000000" + "0000000000000000"),
                Convert.FromHexString("FFD0800000000000000000000000000000000000000000000000000000000000"),
                0x01020304, 0x11121314, 0x21222324,
                Convert.FromHexString("101112131415161718191A1B1C1D1E1F"), 0x31323334),
            new LegacyD13PrestigeRankingEntry(
                2, long.MinValue, 80_000_011, uint.MaxValue,
                Convert.FromHexString("202122232425262728292A2B2C2D2E2F303132333435363738393A3B3C3D3E3F"),
                Convert.FromHexString("404142434445464748494A4B4C4D4E4F505152535455565758595A5B5C5D5E5F"),
                uint.MaxValue, 0x80000000, 0x10000000,
                Convert.FromHexString("606162636465666768696A6B6C6D6E6F"), 0xa1a2a3a4),
        ]);

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out var consumed, out _));
        Assert.Equal((long)bytes.Length, sequence.Slice(0, consumed).Length);
        return frame;
    }
}
