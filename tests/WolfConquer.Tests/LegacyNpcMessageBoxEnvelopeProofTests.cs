using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNpcMessageBoxEnvelopeProofTests
{
    private const string EnglishText = "Sorry, you cannot do this while vending.";

    [Fact]
    public void EmptyMessageBoxMatchesCompleteNativeLiteralEnvelope()
    {
        byte[] expected =
        [
            0x0F, 0x00, 0xCF, 0x08,
            0x00, 0x00, 0x00, 0x00,
            0xFF, 0xFF, 0x00, 0x06,
            0x01, 0x00, 0x00,
        ];

        var actual = LegacyNpcReplyPacket.ForMessageBox(string.Empty).ToFrameBytes();

        Assert.Equal(expected, actual);
        Assert.Equal(15, actual.Length);
        AssertMessageBoxFields(actual, string.Empty, expectedTextBytes: 0);
    }

    [Fact]
    public void EnglishMessageBoxMatchesCompleteNativeLiteralEnvelope()
    {
        // Source-derived55-byte frame; no packet factory/writer builds the expected bytes.
        byte[] expected =
        [
            0x37, 0x00, 0xCF, 0x08,
            0x00, 0x00, 0x00, 0x00,
            0xFF, 0xFF, 0x00, 0x06,
            0x01, 0x28, 0x00,
            0x53, 0x6F, 0x72, 0x72, 0x79, 0x2C, 0x20, 0x79,
            0x6F, 0x75, 0x20, 0x63, 0x61, 0x6E, 0x6E, 0x6F,
            0x74, 0x20, 0x64, 0x6F, 0x20, 0x74, 0x68, 0x69,
            0x73, 0x20, 0x77, 0x68, 0x69, 0x6C, 0x65, 0x20,
            0x76, 0x65, 0x6E, 0x64, 0x69, 0x6E, 0x67, 0x2E,
        ];

        var actual = LegacyNpcReplyPacket.ForMessageBox(EnglishText).ToFrameBytes();

        Assert.Equal(expected, actual);
        Assert.Equal(55, actual.Length);
        AssertMessageBoxFields(actual, EnglishText, expectedTextBytes: 40);
    }

    [Fact]
    public void MessageBoxServerSealAppendsOnceAndStripsTheWholeNativeWire()
    {
        // Independent full63-byte wire:55-byte messagebox plus ONE literalTQServer footer.
        byte[] expectedWire =
        [
            0x37, 0x00, 0xCF, 0x08,
            0x00, 0x00, 0x00, 0x00,
            0xFF, 0xFF, 0x00, 0x06,
            0x01, 0x28, 0x00,
            0x53, 0x6F, 0x72, 0x72, 0x79, 0x2C, 0x20, 0x79,
            0x6F, 0x75, 0x20, 0x63, 0x61, 0x6E, 0x6E, 0x6F,
            0x74, 0x20, 0x64, 0x6F, 0x20, 0x74, 0x68, 0x69,
            0x73, 0x20, 0x77, 0x68, 0x69, 0x6C, 0x65, 0x20,
            0x76, 0x65, 0x6E, 0x64, 0x69, 0x6E, 0x67, 0x2E,
            0x54, 0x51, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72,
        ];
        var unsealed = LegacyNpcReplyPacket.ForMessageBox(EnglishText).ToFrameBytes();
        Assert.Equal(expectedWire.AsSpan(0, 55).ToArray(), unsealed);

        var wire = LegacyGameServerFrameCodec.AppendSeal(unsealed);

        Assert.Equal(expectedWire, wire);
        Assert.Equal(63, wire.Length);
        Assert.Equal(unsealed.AsSpan(0, 4).ToArray(), wire.AsSpan(0, 4).ToArray());
        Assert.Equal((ushort)55, BinaryPrimitives.ReadUInt16LittleEndian(wire));
        Assert.Equal(unsealed, wire.AsSpan(0, 55).ToArray());
        Assert.Equal(new byte[] { 0x54, 0x51, 0x53, 0x65, 0x72, 0x76, 0x65, 0x72 },
            wire.AsSpan(55).ToArray());
        Assert.True(LegacyGameServerFrameCodec.TryStripSeal(wire, out var restored, out var consumed));
        Assert.Equal(63, consumed);
        Assert.Equal(unsealed, restored);
        AssertMessageBoxFields(restored, EnglishText, expectedTextBytes: 40);
        // Exact total length plus full consumption leaves no second frame/Finish bytes.
    }

    private static void AssertMessageBoxFields(byte[] frameBytes, string expectedText, ushort expectedTextBytes)
    {
        Assert.Equal(15 + expectedTextBytes, frameBytes.Length);
        Assert.True(PacketHeader.TryRead(frameBytes, out var header));
        Assert.Equal((ushort)frameBytes.Length, header.Length);
        Assert.Equal((ushort)2255, header.Opcode);
        Assert.Equal(0U, BinaryPrimitives.ReadUInt32LittleEndian(frameBytes.AsSpan(4, 4)));
        Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(frameBytes.AsSpan(8, 2)));
        Assert.Equal((byte)0, frameBytes[10]);
        Assert.Equal((byte)6, frameBytes[11]);
        Assert.Equal((byte)1, frameBytes[12]);
        Assert.Equal(expectedTextBytes, BinaryPrimitives.ReadUInt16LittleEndian(frameBytes.AsSpan(13, 2)));
        Assert.Equal(expectedText, Encoding.UTF8.GetString(frameBytes.AsSpan(15)));
        var sequence = new ReadOnlySequence<byte>(frameBytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out var consumed, out _));
        Assert.Equal(sequence.End, consumed);
        Assert.True(LegacyNpcReplyPacket.TryParse(frame, out var message));
        Assert.Equal(0U, message.Unknown);
        Assert.Equal(ushort.MaxValue, message.InputMaxLength);
        Assert.Equal((byte)0, message.OptionId);
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, message.InteractType);
        Assert.Equal(expectedText, message.Text);
    }
}
