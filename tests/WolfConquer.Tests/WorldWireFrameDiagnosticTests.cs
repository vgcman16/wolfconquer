using System.Buffers;
using System.Reflection;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class WorldWireFrameDiagnosticTests
{
    [Theory]
    [InlineData(false, 4999)]
    [InlineData(true, 4999)]
    [InlineData(false, 2005)]
    [InlineData(true, 2005)]
    [InlineData(false, 2065)]
    [InlineData(true, 2065)]
    [InlineData(false, 2179)]
    [InlineData(true, 2179)]
    public void ExistingWorldFormattersExposeOnlyFrameMetadata(bool registryFormatter, int opcode)
    {
        byte[] payload = [0x73, 0x65, 0x63, 0x72, 0x65, 0x74, 0x00, 0xFF];
        var frame = Frame((ushort)opcode, payload);
        var type = registryFormatter ? typeof(WorldPacketHandlers) : typeof(WorldServerFrameHandler);
        var formatter = type.GetMethod("FormatFrameDiagnostic", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(formatter);

        var text = Assert.IsType<string>(formatter.Invoke(null, [frame]));

        Assert.Equal($"opcode={opcode} payloadLength=8", text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingWorldFormattersDoNotCopyLargeOrEmptyPayloads(bool registryFormatter)
    {
        var type = registryFormatter ? typeof(WorldPacketHandlers) : typeof(WorldServerFrameHandler);
        var formatter = type.GetMethod("FormatFrameDiagnostic", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(formatter);
        foreach (var payload in new[] { Array.Empty<byte>(), Enumerable.Repeat((byte)0xA5, 4096).ToArray() })
        {
            var text = Assert.IsType<string>(formatter.Invoke(null, [Frame(4999, payload)]));
            Assert.Equal($"opcode=4999 payloadLength={payload.Length}", text);
        }
    }

    private static PacketFrame Frame(ushort opcode, byte[] payload)
    {
        var writer = new PacketWriter(opcode, payload.Length);
        payload.CopyTo(writer.Payload);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(writer.ToArray()), out var frame, out _, out _));
        return frame;
    }
}
