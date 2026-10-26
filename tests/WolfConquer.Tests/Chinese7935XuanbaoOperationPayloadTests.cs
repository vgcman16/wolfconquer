using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class Chinese7935XuanbaoOperationPayloadTests
{
    private const string Golden = "080D10AC021800202828F40330808080800838FFFFFFFF0F42044100C3A94A04080110044A004A08080010FFFFFFFF0F5005";

    [Fact]
    public void ConfirmedTenFieldAndTwoFieldChildSchemaHasAnExactIndependentWireGolden()
    {
        var packet = new Chinese7935XuanbaoOperationPayload(13,
            new(Field2: 300, Field3: 0, Field4: 40, Field5: 500, Field6: 0x8000_0000,
                Field7: uint.MaxValue, Field10: 5),
            new ReadOnlyMemory<byte>([65, 0, 0xc3, 0xa9]), [new(1, 4), default, new(0, uint.MaxValue)]);

        Assert.Equal(Convert.FromHexString(Golden), packet.ToPayloadBytes());
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(Convert.FromHexString(Golden), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(13U, parsed.Operation);
        Assert.Equal(packet.Fields, parsed.Fields);
        Assert.Equal(new byte[] { 65, 0, 0xc3, 0xa9 }, parsed.CopyData8());
        Assert.Equal(packet.AccInfo.ToArray(), parsed.AccInfo.ToArray());
        Assert.Equal(Convert.FromHexString(Golden), parsed.ToPayloadBytes());
    }

    [Fact]
    public void RequiredPresentZeroAndOptionalAbsentZeroEmptyRemainDistinct()
    {
        var absent = new Chinese7935XuanbaoOperationPayload(0);
        var present = new Chinese7935XuanbaoOperationPayload(0, new(Field2: 0, Field10: 0),
            ReadOnlyMemory<byte>.Empty, [default]);
        Assert.Equal(Convert.FromHexString("0800"), absent.ToPayloadBytes());
        Assert.Equal(Convert.FromHexString("0800100042004A005000"), present.ToPayloadBytes());
        Assert.False(absent.HasData8);
        Assert.Null(absent.CopyData8());
        Assert.True(present.HasData8);
        Assert.Empty(present.CopyData8()!);
        Assert.Single(present.AccInfo.ToArray());
    }

    [Fact]
    public void UnorderedOptionalDuplicatesTakeLastValueWhileChildrenRetainNativeAppendOrder()
    {
        var bytes = Convert.FromHexString("4A04080110024A0210031001080D10024201414201425005");
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(bytes, out var packet));
        Assert.NotNull(packet);
        Assert.Equal(2U, packet.Fields.Field2);
        Assert.Equal(new byte[] { 66 }, packet.CopyData8());
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(1, 2), new(null, 3) }, packet.AccInfo.ToArray());
        Assert.Equal(Convert.FromHexString("080D10024201424A04080110024A0210035005"), packet.ToPayloadBytes());
    }

    [Fact]
    public void DuplicateChildScalarsTakeLastValuesWithoutCreatingAnotherChild()
    {
        var bytes = Convert.FromHexString("08014A080801100208031004");
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(bytes, out var packet));
        Assert.NotNull(packet);
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(3, 4) }, packet.AccInfo.ToArray());
        Assert.Equal(Convert.FromHexString("08014A0408031004"), packet.ToPayloadBytes());
    }

    [Fact]
    public void ParsedAndConstructedPayloadsOwnTheirBytesAndChildArray()
    {
        byte[] data = [65, 0, 0xc3, 0xa9];
        Chinese7935XuanbaoAccInfo[] children = [new(1, 4)];
        var packet = new Chinese7935XuanbaoOperationPayload(13, data8: data, accInfo: children);
        var golden = Convert.FromHexString("080D42044100C3A94A0408011004");
        data[0] = 90;
        children[0] = new(99, 100);
        var copy = packet.CopyData8()!;
        copy[0] = 90;
        var emitted = packet.ToPayloadBytes();
        emitted[0] = 0;
        Assert.Equal(golden, packet.ToPayloadBytes());
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(golden, out var parsed));
        Array.Fill(golden, (byte)0);
        Assert.NotNull(parsed);
        Assert.Equal(packet.ToPayloadBytes(), parsed.ToPayloadBytes());
    }

    [Fact]
    public void ExplicitFrameReaderChecksTheObservedOpcodeWithoutRegisteringADefaultHandler()
    {
        var bytes = Convert.FromHexString(Golden);
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownFrame(Frame(2132, bytes), out var packet));
        Assert.NotNull(packet);
        Assert.Equal(bytes, packet.ToPayloadBytes());
        Assert.False(Chinese7935XuanbaoOperationPayload.TryParseKnownFrame(Frame(2133, bytes), out packet));
        Assert.Null(packet);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        Assert.False(registry.TryGetHandler(Chinese7935XuanbaoOperationPayload.Opcode, out _));
    }

    [Fact]
    public void UInt32MaximumUsesFiveBytesRatherThanSignedOrWidenedWireEncoding()
    {
        var packet = new Chinese7935XuanbaoOperationPayload(uint.MaxValue, new(Field10: uint.MaxValue));
        Assert.Equal(Convert.FromHexString("08FFFFFFFF0F50FFFFFFFF0F"), packet.ToPayloadBytes());
        Assert.True(Chinese7935XuanbaoOperationPayload.TryParseKnownPayload(packet.ToPayloadBytes(), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(uint.MaxValue, parsed.Operation);
        Assert.Equal(uint.MaxValue, parsed.Fields.Field10);
    }

    private static PacketFrame Frame(ushort opcode, byte[] payload)
    {
        var writer = new PacketWriter(opcode, payload.Length);
        payload.CopyTo(writer.Payload);
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(writer.ToArray()), out var frame, out _, out _));
        return frame;
    }
}
