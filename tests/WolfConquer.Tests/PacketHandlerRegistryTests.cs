using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Tests;

public sealed class PacketHandlerRegistryTests
{
    [Fact]
    public async Task RegistryBuildsPrivateStaticAttributedHandlers()
    {
        var registry = PacketHandlerRegistry<PacketHandlerContext>.FromTypes([typeof(SamplePacketHandlers)]);
        var context = new PacketHandlerContext();
        var frame = ReadFrame(new PacketWriter(2243, payloadLength: 0).ToArray());

        Assert.Equal(2, registry.Count);
        Assert.Contains((ushort)2243, registry.Opcodes);
        Assert.True(registry.TryGetHandler(2243, out var handler));

        await handler!(context, frame, CancellationToken.None);

        Assert.Equal(["team:2243"], context.Calls);
    }

    [Fact]
    public async Task HandleAsyncDispatchesByFrameOpcode()
    {
        var registry = PacketHandlerRegistry<PacketHandlerContext>.FromTypes([typeof(SamplePacketHandlers)]);
        var context = new PacketHandlerContext();
        var writer = new PacketWriter(2135, payloadLength: 4);
        writer.WriteUInt32(0, 0xABCDEF01);

        await registry.HandleAsync(context, ReadFrame(writer.ToArray()));

        Assert.Equal(["attack:ABCDEF01"], context.Calls);
    }

    [Fact]
    public void HandleAsyncRejectsMissingOpcode()
    {
        var registry = PacketHandlerRegistry<PacketHandlerContext>.FromTypes([typeof(SamplePacketHandlers)]);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.HandleAsync(new PacketHandlerContext(), ReadFrame(new PacketWriter(999, 0).ToArray())).GetAwaiter().GetResult());

        Assert.Contains("999", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryRejectsDuplicateOpcodes()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PacketHandlerRegistry<PacketHandlerContext>.FromTypes([typeof(DuplicatePacketHandlers)]));

        Assert.Contains("1001", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistryRejectsInvalidHandlerSignature()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            PacketHandlerRegistry<PacketHandlerContext>.FromTypes([typeof(InvalidPacketHandlers)]));

        Assert.Contains("1002", exception.Message, StringComparison.Ordinal);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private sealed class PacketHandlerContext
    {
        public List<string> Calls { get; } = [];
    }

    private static class SamplePacketHandlers
    {
        [PacketHandler(2243)]
        private static ValueTask HandleTeam(
            PacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
        {
            context.Calls.Add("team:" + frame.Header.Opcode);
            return ValueTask.CompletedTask;
        }

        [PacketHandler(2135)]
        private static ValueTask HandleAttack(
            PacketHandlerContext context,
            PacketFrame frame,
            CancellationToken cancellationToken)
        {
            Assert.True(PacketReader.TryRead(frame.Frame.ToArray(), out var reader));
            context.Calls.Add("attack:" + reader.ReadUInt32(0).ToString("X8"));
            return ValueTask.CompletedTask;
        }
    }

    private static class DuplicatePacketHandlers
    {
        [PacketHandler(1001)]
        private static ValueTask First(PacketHandlerContext context, PacketFrame frame, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }

        [PacketHandler(1001)]
        private static ValueTask Second(PacketHandlerContext context, PacketFrame frame, CancellationToken cancellationToken)
        {
            return ValueTask.CompletedTask;
        }
    }

    private static class InvalidPacketHandlers
    {
        [PacketHandler(1002)]
        private static void MissingValueTask(PacketHandlerContext context, PacketFrame frame, CancellationToken cancellationToken)
        {
        }
    }
}
