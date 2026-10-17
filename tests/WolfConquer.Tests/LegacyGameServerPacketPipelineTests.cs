using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed class LegacyGameServerPacketPipelineTests
{
    private static readonly byte[] CipherKey = Encoding.ASCII.GetBytes("AS19RayF54ZO1D6P");

    [Fact]
    public async Task ReadFramesDecryptsSealedLegacyGameServerFrame()
    {
        var modernFrame = CreateFrame(2065, 0x0013BEED);
        var encrypted = LegacyGameServerFrameCodec.EncodeEncrypted(
            modernFrame,
            new LegacyGameServerCipher(CipherKey));
        var frames = new List<PacketFrame>();
        var pipe = new Pipe();

        var readTask = LegacyGameServerPacketPipeline.ReadFramesAsync(
            pipe.Reader,
            new LegacyGameServerCipher(CipherKey),
            (frame, _) =>
            {
                frames.Add(frame);
                return ValueTask.CompletedTask;
            }).AsTask();

        await pipe.Writer.WriteAsync(encrypted);
        await pipe.Writer.CompleteAsync();
        await readTask;

        var frame = Assert.Single(frames);
        Assert.Equal(2065, frame.Header.Opcode);
        Assert.Equal(modernFrame, frame.Frame.ToArray());
    }

    [Fact]
    public async Task ReadFramesDecryptsClientSealedLegacyGameServerFrame()
    {
        var modernFrame = CreateFrame(2065, 0x0013BEED);
        var encrypted = LegacyGameServerFrameCodec.EncodeEncrypted(
            modernFrame,
            new LegacyGameServerCipher(CipherKey),
            LegacyGameServerFrameSeal.Client);
        var frames = new List<PacketFrame>();
        var pipe = new Pipe();

        var readTask = LegacyGameServerPacketPipeline.ReadFramesAsync(
            pipe.Reader,
            new LegacyGameServerCipher(CipherKey),
            (frame, _) =>
            {
                frames.Add(frame);
                return ValueTask.CompletedTask;
            },
            expectedSeal: LegacyGameServerFrameSeal.Client).AsTask();

        await pipe.Writer.WriteAsync(encrypted);
        await pipe.Writer.CompleteAsync();
        await readTask;

        var frame = Assert.Single(frames);
        Assert.Equal(2065, frame.Header.Opcode);
        Assert.Equal(modernFrame, frame.Frame.ToArray());
    }

    [Fact]
    public async Task ReadFramesHandlesEncryptedLegacyFrameAcrossWrites()
    {
        var modernFrame = CreateFrame(2179, 0xAABBCCDD);
        var encrypted = LegacyGameServerFrameCodec.EncodeEncrypted(
            modernFrame,
            new LegacyGameServerCipher(CipherKey));
        var frames = new List<PacketFrame>();
        var pipe = new Pipe();

        var readTask = LegacyGameServerPacketPipeline.ReadFramesAsync(
            pipe.Reader,
            new LegacyGameServerCipher(CipherKey),
            (frame, _) =>
            {
                frames.Add(frame);
                return ValueTask.CompletedTask;
            }).AsTask();

        await pipe.Writer.WriteAsync(encrypted.AsMemory(0, 3));
        await Task.Delay(25);
        await pipe.Writer.WriteAsync(encrypted.AsMemory(3, 5));
        await Task.Delay(25);
        await pipe.Writer.WriteAsync(encrypted.AsMemory(8));
        await pipe.Writer.CompleteAsync();
        await readTask;

        Assert.Single(frames);
        Assert.Equal(2179, frames[0].Header.Opcode);
        Assert.Equal(modernFrame, frames[0].Frame.ToArray());
    }

    [Fact]
    public async Task ReadFramesPreservesCipherStreamAcrossMultipleFrames()
    {
        var firstModernFrame = CreateFrame(2065, 0x0013BEED);
        var secondModernFrame = CreateFrame(2243, 0x00000123);
        var encryptCipher = new LegacyGameServerCipher(CipherKey);
        var firstEncrypted = LegacyGameServerFrameCodec.EncodeEncrypted(firstModernFrame, encryptCipher);
        var secondEncrypted = LegacyGameServerFrameCodec.EncodeEncrypted(secondModernFrame, encryptCipher);
        var frames = new List<PacketFrame>();
        var pipe = new Pipe();

        var readTask = LegacyGameServerPacketPipeline.ReadFramesAsync(
            pipe.Reader,
            new LegacyGameServerCipher(CipherKey),
            (frame, _) =>
            {
                frames.Add(frame);
                return ValueTask.CompletedTask;
            }).AsTask();

        await pipe.Writer.WriteAsync(firstEncrypted);
        await pipe.Writer.WriteAsync(secondEncrypted);
        await pipe.Writer.CompleteAsync();
        await readTask;

        Assert.Equal([2065, 2243], frames.Select(frame => frame.Header.Opcode));
        Assert.Equal(firstModernFrame, frames[0].Frame.ToArray());
        Assert.Equal(secondModernFrame, frames[1].Frame.ToArray());
    }

    [Fact]
    public async Task ReadFramesRejectsIncompleteStreamOnCompletion()
    {
        var encrypted = LegacyGameServerFrameCodec.EncodeEncrypted(
            CreateFrame(2065, 0x0013BEED),
            new LegacyGameServerCipher(CipherKey));
        var pipe = new Pipe();
        var readTask = LegacyGameServerPacketPipeline.ReadFramesAsync(
            pipe.Reader,
            new LegacyGameServerCipher(CipherKey),
            (_, _) => ValueTask.CompletedTask).AsTask();

        await pipe.Writer.WriteAsync(encrypted.AsMemory(0, encrypted.Length - 1));
        await pipe.Writer.CompleteAsync();

        await Assert.ThrowsAsync<EndOfStreamException>(() => readTask);
    }

    private static byte[] CreateFrame(ushort opcode, uint value)
    {
        var writer = new PacketWriter(opcode, payloadLength: sizeof(uint));
        writer.WriteUInt32(0, value);
        return writer.ToArray();
    }
}
