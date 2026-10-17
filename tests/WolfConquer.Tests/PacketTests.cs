using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed class PacketTests
{
    [Fact]
    public void PacketHeaderReadsLittleEndianLengthAndOpcode()
    {
        var bytes = new byte[] { 0x08, 0x00, 0xF2, 0x03 };

        Assert.True(PacketHeader.TryRead(bytes, out var header));

        Assert.Equal(8, header.Length);
        Assert.Equal(1010, header.Opcode);
    }

    [Fact]
    public void PacketReaderRejectsBuffersShorterThanHeader()
    {
        Assert.False(PacketReader.TryRead([0x01, 0x02, 0x03], out _));
    }

    [Fact]
    public void PacketReaderRejectsIncompleteFrame()
    {
        var bytes = new byte[] { 0x08, 0x00, 0xF2, 0x03, 0xAA };

        Assert.False(PacketReader.TryRead(bytes, out _));
    }

    [Fact]
    public void PacketReaderExposesPayloadAfterFourByteHeader()
    {
        var bytes = new byte[] { 0x08, 0x00, 0xF2, 0x03, 0x34, 0x12, 0xEF, 0xBE };

        Assert.True(PacketReader.TryRead(bytes, out var reader));

        Assert.Equal(0x1234, reader.ReadUInt16(0));
        Assert.Equal(0xBEEF, reader.ReadUInt16(2));
    }

    [Fact]
    public void PacketReaderReadsNullTerminatedStringsFromPayload()
    {
        var bytes = new byte[] { 0x0A, 0x00, 0xF2, 0x03, (byte)'R', (byte)'a', (byte)'y', 0, (byte)'x', 0 };

        Assert.True(PacketReader.TryRead(bytes, out var reader));

        Assert.Equal("Ray", reader.ReadCString(0, 6, Encoding.ASCII));
    }

    [Fact]
    public void PacketWriterWritesLengthOpcodeAndPayloadLittleEndian()
    {
        var writer = new PacketWriter(1010, payloadLength: 4);
        writer.WriteUInt16(0, 0x1234);
        writer.WriteUInt16(2, 0xBEEF);

        Assert.Equal([0x08, 0x00, 0xF2, 0x03, 0x34, 0x12, 0xEF, 0xBE], writer.ToArray());
    }

    [Fact]
    public void PacketReaderAndWriterHandleSignedLongPayloadValues()
    {
        var writer = new PacketWriter(1001, payloadLength: 8);
        writer.WriteInt64(0, 1_296_473);

        Assert.True(PacketReader.TryRead(writer.ToArray(), out var reader));
        Assert.Equal(1_296_473, reader.ReadInt64(0));
    }

    [Fact]
    public void PacketWriterRejectsPayloadBeyondReceiveSizeLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PacketWriter(1010, payloadLength: 6841));
    }

    [Fact]
    public void PacketFrameDecoderWaitsForCompleteHeader()
    {
        var buffer = new ReadOnlySequence<byte>([0x08, 0x00, 0xF2]);

        Assert.False(PacketFrameDecoder.TryRead(buffer, out _, out var consumed, out var examined));
        Assert.Equal(buffer.Length, buffer.Slice(consumed).Length);
        Assert.Equal(0, buffer.Slice(examined).Length);
    }

    [Fact]
    public void PacketFrameDecoderWaitsForCompleteFrame()
    {
        var buffer = new ReadOnlySequence<byte>([0x08, 0x00, 0xF2, 0x03, 0xAA]);

        Assert.False(PacketFrameDecoder.TryRead(buffer, out _, out var consumed, out var examined));
        Assert.Equal(buffer.Length, buffer.Slice(consumed).Length);
        Assert.Equal(0, buffer.Slice(examined).Length);
    }

    [Fact]
    public void PacketFrameDecoderReadsFrameSplitAcrossSegments()
    {
        var buffer = CreateSequence(
            [0x08, 0x00],
            [0xF2, 0x03, 0x34],
            [0x12, 0xEF, 0xBE]);

        Assert.True(PacketFrameDecoder.TryRead(buffer, out var frame, out var consumed, out var examined));

        Assert.Equal(8, frame.Header.Length);
        Assert.Equal(1010, frame.Header.Opcode);
        Assert.Equal([0x34, 0x12, 0xEF, 0xBE], frame.Payload.ToArray());
        Assert.Equal(0, buffer.Slice(consumed).Length);
        Assert.Equal(0, buffer.Slice(examined).Length);
    }

    [Fact]
    public void PacketFrameDecoderConsumesOnlyFirstFrame()
    {
        var first = new PacketWriter(1010, payloadLength: 2);
        first.WriteUInt16(0, 0x1234);
        var second = new PacketWriter(1001, payloadLength: 2);
        second.WriteUInt16(0, 0xBEEF);
        var buffer = new ReadOnlySequence<byte>(first.ToArray().Concat(second.ToArray()).ToArray());

        Assert.True(PacketFrameDecoder.TryRead(buffer, out var firstFrame, out var consumed, out _));

        var remaining = buffer.Slice(consumed);
        Assert.Equal(1010, firstFrame.Header.Opcode);
        Assert.Equal(second.Header.Length, remaining.Length);

        Assert.True(PacketFrameDecoder.TryRead(remaining, out var secondFrame, out var secondConsumed, out _));
        Assert.Equal(1001, secondFrame.Header.Opcode);
        Assert.Equal([0xEF, 0xBE], secondFrame.Payload.ToArray());
        Assert.Equal(0, remaining.Slice(secondConsumed).Length);
    }

    [Fact]
    public void PacketFrameDecoderRejectsInvalidFrameLength()
    {
        var buffer = new ReadOnlySequence<byte>([0x03, 0x00, 0xF2, 0x03]);

        Assert.Throws<InvalidDataException>(() => PacketFrameDecoder.TryRead(buffer, out _, out _, out _));
    }

    [Fact]
    public void PacketFrameDecoderRejectsFrameBeyondConfiguredLimit()
    {
        var buffer = new ReadOnlySequence<byte>([0x09, 0x00, 0xF2, 0x03]);

        Assert.Throws<InvalidDataException>(() => PacketFrameDecoder.TryRead(buffer, out _, out _, out _, maxFrameSize: 8));
    }

    [Fact]
    public async Task PacketPipelineReadsMultipleFramesFromPipe()
    {
        var first = new PacketWriter(1010, payloadLength: 2);
        first.WriteUInt16(0, 0x1234);
        var second = new PacketWriter(1001, payloadLength: 2);
        second.WriteUInt16(0, 0xBEEF);
        var pipe = new Pipe();
        var frames = new List<(ushort Opcode, byte[] Payload)>();

        await pipe.Writer.WriteAsync(first.ToArray());
        await pipe.Writer.WriteAsync(second.ToArray());
        await pipe.Writer.CompleteAsync();

        await PacketPipeline.ReadFramesAsync(pipe.Reader, (frame, _) =>
        {
            frames.Add((frame.Header.Opcode, frame.Payload.ToArray()));
            return ValueTask.CompletedTask;
        });

        Assert.Collection(
            frames,
            frame =>
            {
                Assert.Equal(1010, frame.Opcode);
                Assert.Equal([0x34, 0x12], frame.Payload);
            },
            frame =>
            {
                Assert.Equal(1001, frame.Opcode);
                Assert.Equal([0xEF, 0xBE], frame.Payload);
            });
    }

    [Fact]
    public async Task PacketPipelineHandlesCompleteFrameBeforePartialNextFrame()
    {
        var first = new PacketWriter(1010, payloadLength: 2);
        first.WriteUInt16(0, 0x1234);
        var second = new PacketWriter(1001, payloadLength: 2);
        second.WriteUInt16(0, 0xBEEF);
        var secondBytes = second.ToArray();
        var partialSecond = secondBytes[..5];
        var pipe = new Pipe();
        var firstSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frames = new List<ushort>();

        var readTask = PacketPipeline.ReadFramesAsync(pipe.Reader, (frame, _) =>
        {
            frames.Add(frame.Header.Opcode);
            if (frame.Header.Opcode == first.Header.Opcode)
            {
                firstSeen.TrySetResult();
            }

            return ValueTask.CompletedTask;
        }).AsTask();

        await pipe.Writer.WriteAsync(first.ToArray().Concat(partialSecond).ToArray());
        await firstSeen.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.False(readTask.IsCompleted);

        await pipe.Writer.WriteAsync(secondBytes[partialSecond.Length..]);
        await pipe.Writer.CompleteAsync();
        await readTask;

        Assert.Equal([1010, 1001], frames);
    }

    private static ReadOnlySequence<byte> CreateSequence(params byte[][] segments)
    {
        if (segments.Length == 0)
        {
            return ReadOnlySequence<byte>.Empty;
        }

        var first = new BufferSegment(segments[0]);
        var last = first;
        foreach (var segment in segments.Skip(1))
        {
            last = last.Append(segment);
        }

        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class BufferSegment : ReadOnlySequenceSegment<byte>
    {
        public BufferSegment(byte[] memory)
        {
            Memory = memory;
        }

        public BufferSegment Append(byte[] memory)
        {
            var segment = new BufferSegment(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = segment;
            return segment;
        }
    }
}
