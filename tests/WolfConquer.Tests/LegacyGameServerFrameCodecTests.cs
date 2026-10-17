using System.IO;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed class LegacyGameServerFrameCodecTests
{
    private static readonly byte[] CipherKey = Encoding.ASCII.GetBytes("AS19RayF54ZO1D6P");

    [Fact]
    public void AppendSealKeepsHeaderLengthAndAddsLegacySeal()
    {
        var modernFrame = CreateModernFrame();

        var legacyFrame = LegacyGameServerFrameCodec.AppendSeal(modernFrame);

        Assert.Equal(modernFrame.Length + LegacyGameServerFrameCodec.SealSize, legacyFrame.Length);
        Assert.Equal(modernFrame, legacyFrame[..modernFrame.Length]);
        Assert.Equal(8, legacyFrame[0]);
        Assert.Equal(0, legacyFrame[1]);
        Assert.Equal(LegacyGameServerFrameCodec.SealBytes.ToArray(), legacyFrame[^LegacyGameServerFrameCodec.SealSize..]);
    }

    [Fact]
    public void StripSealReturnsModernFrameAndConsumesLegacyWireLength()
    {
        var modernFrame = CreateModernFrame();
        var legacyFrame = LegacyGameServerFrameCodec.AppendSeal(modernFrame);

        var decoded = LegacyGameServerFrameCodec.TryStripSeal(legacyFrame, out var stripped, out var consumed);

        Assert.True(decoded);
        Assert.Equal(legacyFrame.Length, consumed);
        Assert.Equal(modernFrame, stripped);
        Assert.True(PacketHeader.TryRead(stripped, out var header));
        Assert.Equal(modernFrame.Length, header.Length);
    }

    [Fact]
    public void StripSealReturnsModernFrameWhenCallerRequestsClientSeal()
    {
        var modernFrame = CreateModernFrame();
        var legacyFrame = LegacyGameServerFrameCodec.AppendSeal(
            modernFrame,
            LegacyGameServerFrameSeal.Client);

        var decoded = LegacyGameServerFrameCodec.TryStripSeal(
            legacyFrame,
            LegacyGameServerFrameSeal.Client,
            out var stripped,
            out var consumed);

        Assert.True(decoded);
        Assert.Equal(legacyFrame.Length, consumed);
        Assert.Equal(modernFrame, stripped);
        Assert.Equal(
            LegacyGameServerFrameCodec.ClientSealBytes.ToArray(),
            legacyFrame[^LegacyGameServerFrameCodec.SealSize..]);
        Assert.True(PacketHeader.TryRead(stripped, out var header));
        Assert.Equal(modernFrame.Length, header.Length);
    }

    [Fact]
    public void StripSealRejectsWrongSeal()
    {
        var legacyFrame = LegacyGameServerFrameCodec.AppendSeal(CreateModernFrame());
        legacyFrame[^1] = (byte)'X';

        Assert.Throws<InvalidDataException>(() => LegacyGameServerFrameCodec.TryStripSeal(legacyFrame, out _));
    }

    [Fact]
    public void StripSealWaitsForIncompleteLegacyFrame()
    {
        var legacyFrame = LegacyGameServerFrameCodec.AppendSeal(CreateModernFrame());

        var decoded = LegacyGameServerFrameCodec.TryStripSeal(
            legacyFrame.AsSpan(0, legacyFrame.Length - 1),
            out var stripped,
            out var consumed);

        Assert.False(decoded);
        Assert.Empty(stripped);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void EncryptedFrameRoundTripsThroughSealCodec()
    {
        var modernFrame = CreateModernFrame();
        var encryptCipher = new LegacyGameServerCipher(CipherKey);
        var decryptCipher = new LegacyGameServerCipher(CipherKey);

        var encrypted = LegacyGameServerFrameCodec.EncodeEncrypted(modernFrame, encryptCipher);
        var decoded = LegacyGameServerFrameCodec.DecodeEncrypted(encrypted, decryptCipher);

        Assert.NotEqual(LegacyGameServerFrameCodec.AppendSeal(modernFrame), encrypted);
        Assert.Equal(modernFrame, decoded);
    }

    private static byte[] CreateModernFrame()
    {
        var writer = new PacketWriter(1010, payloadLength: 4);
        writer.WriteUInt16(0, 0x1234);
        writer.WriteUInt16(2, 0xBEEF);
        return writer.ToArray();
    }
}
