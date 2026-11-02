using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class Chinese7937EchoResponseTests
{
    // Independent synthetic wire records deliberately differ from writer order.
    private const string Operation8Golden =
        "380728AC020808100B1800200130005009420341FF004A04080110044A004A08080010FFFFFFFF0F";
    private const string Operation10Golden =
        "2008180010FFFFFFFF0F080A380A280B300C50FFFFFFFF0F42004A0408011002";

    private static Chinese7937EchoResponse Parse(string hex) => Parse(Convert.FromHexString(hex));

    private static Chinese7937EchoResponse Parse(byte[] bytes)
    {
        Assert.True(Chinese7937EchoResponse.TryParsePayload(bytes, 7937, out var response, out var failure), failure.ToString());
        Assert.Equal(Chinese7937EchoResponseFailure.None, failure);
        return Assert.IsType<Chinese7937EchoResponse>(response);
    }

    [Fact]
    public void Operation8GoldenRetainsTheCompleteMessageAndItsObservedArgumentOrder()
    {
        var original = Convert.FromHexString(Operation8Golden);
        var response = Parse(original);
        Assert.Equal(7937, Chinese7937EchoResponse.ClientVersion);
        Assert.Equal((ushort)2132, Chinese7937EchoResponse.Opcode);
        Assert.Equal(8U, response.Operation);
        Assert.Equal(0x36U, response.InternalUiEventId);
        Assert.Equal(new Chinese7937EchoArgument[] { new(7, 7), new(5, 300) }, response.Arguments.ToArray());
        var known = response.KnownPayload;
        Assert.Equal(8U, known.Operation);
        Assert.Equal(new Chinese7935XuanbaoFields(11, 0, 1, 300, 0, 7, 9), known.Fields);
        Assert.Equal(new byte[] { 0x41, 0xff, 0 }, known.CopyData8());
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(1, 4), default, new(0, uint.MaxValue) }, known.AccInfo.ToArray());
        Assert.Equal(original, response.CopyPayloadBytes());
        Assert.False(original.AsSpan().SequenceEqual(known.ToPayloadBytes()));
    }

    [Fact]
    public void Operation10GoldenMapsFieldsTwoThreeFourAndDoesNotGateOnField10()
    {
        var original = Convert.FromHexString(Operation10Golden);
        var response = Parse(original);
        Assert.Equal(10U, response.Operation);
        Assert.Equal(0x37U, response.InternalUiEventId);
        Assert.Equal(new Chinese7937EchoArgument[] { new(2, uint.MaxValue), new(3, 0), new(4, 8) }, response.Arguments.ToArray());
        Assert.Equal(new Chinese7935XuanbaoFields(uint.MaxValue, 0, 8, 11, 12, 10, uint.MaxValue), response.KnownPayload.Fields);
        Assert.True(response.KnownPayload.HasData8);
        Assert.Empty(response.KnownPayload.CopyData8()!);
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(1, 2) }, response.KnownPayload.AccInfo.ToArray());
        Assert.Equal(original, response.CopyPayloadBytes());
    }

    [Theory]
    [InlineData("0808", "080838002800")]
    [InlineData("080A", "080A100018002000")]
    public void AbsentAndPresentZeroRemainDifferentWithTheSameNativeStorageValue(string absentHex, string zeroHex)
    {
        var absent = Parse(absentHex);
        var present = Parse(zeroHex);
        Assert.Equal(absent.Operation, present.Operation);
        Assert.Equal(absent.InternalUiEventId, present.InternalUiEventId);
        Assert.Equal(absent.Arguments.ToArray().Select(value => value.FieldNumber), present.Arguments.ToArray().Select(value => value.FieldNumber));
        Assert.All(absent.Arguments.ToArray(), argument =>
        {
            Assert.Null(argument.WireValue); Assert.False(argument.IsPresent); Assert.Equal(0U, argument.NativeStorageValue);
        });
        Assert.All(present.Arguments.ToArray(), argument =>
        {
            Assert.Equal(0U, argument.WireValue); Assert.True(argument.IsPresent); Assert.Equal(0U, argument.NativeStorageValue);
        });
        Assert.Equal(Convert.FromHexString(absentHex), absent.CopyPayloadBytes());
        Assert.Equal(Convert.FromHexString(zeroHex), present.CopyPayloadBytes());
    }

    [Theory]
    [InlineData("080838FFFFFFFF0F28FFFFFFFF0F")]
    [InlineData("080A10FFFFFFFF0F18FFFFFFFF0F20FFFFFFFF0F")]
    public void EveryProjectedArgumentRetainsTheFullUnsignedDword(string hex)
    {
        var response = Parse(hex);
        Assert.All(response.Arguments.ToArray(), argument =>
        {
            Assert.Equal(uint.MaxValue, argument.WireValue);
            Assert.Equal(uint.MaxValue, argument.NativeStorageValue);
            Assert.True(argument.IsPresent);
        });
        Assert.Equal(Convert.FromHexString(hex), response.CopyPayloadBytes());
    }

    [Fact]
    public void DuplicateLastValuesAndNoncanonicalVarintsDoNotNormalizeTheOriginalCapture()
    {
        // Final operation8, fields5/7 and string8 win; child scalars also take
        // their last values, while the second empty child stays a second child.
        const string original = "080A280138024201414A0808011002080310040888002881003883004201424A00";
        var response = Parse(original);
        Assert.Equal(8U, response.Operation);
        Assert.Equal(new Chinese7937EchoArgument[] { new(7, 3), new(5, 1) }, response.Arguments.ToArray());
        Assert.Equal(new byte[] { 0x42 }, response.KnownPayload.CopyData8());
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(3, 4), default }, response.KnownPayload.AccInfo.ToArray());
        Assert.Equal(Convert.FromHexString(original), response.CopyPayloadBytes());
        Assert.Equal(Convert.FromHexString("0808280138034201424A04080310044A00"), response.KnownPayload.ToPayloadBytes());
    }

    [Fact]
    public void ChildValuesCannotSupplyAbsentOuterResponseArguments()
    {
        var response = Parse("08084A0408071005");
        Assert.Equal(new Chinese7935XuanbaoAccInfo[] { new(7, 5) }, response.KnownPayload.AccInfo.ToArray());
        Assert.Equal(new Chinese7937EchoArgument[] { new(7, null), new(5, null) }, response.Arguments.ToArray());
    }

    [Fact]
    public void CapturedInputPayloadCopiesArgumentsAndKnownByteCopiesHaveSeparateOwnership()
    {
        var original = Convert.FromHexString(Operation8Golden);
        var source = original.ToArray();
        var response = Parse(source);
        Array.Clear(source);
        var copy = response.CopyPayloadBytes(); Array.Clear(copy);
        var arguments = response.Arguments.ToArray(); arguments[0] = new(99, 99);
        var knownBytes = response.KnownPayload.CopyData8()!; Array.Clear(knownBytes);
        var children = response.KnownPayload.AccInfo.ToArray(); children[0] = new(99, 99);
        Assert.Equal(original, response.CopyPayloadBytes());
        Assert.Equal(new Chinese7937EchoArgument(7, 7), response.Arguments[0]);
        Assert.Equal(new byte[] { 0x41, 0xff, 0 }, response.KnownPayload.CopyData8());
        Assert.Equal(new Chinese7935XuanbaoAccInfo(1, 4), response.KnownPayload.AccInfo[0]);
    }

    [Fact]
    public void ActualSegmentedFrameDecoderAcceptsTheSameCaptureWithoutRegisteringAHandler()
    {
        var bytes = FrameBytes(Convert.FromHexString(Operation10Golden));
        var first = new ByteSegment(bytes.AsMemory(0, 1));
        var last = first;
        for (var index = 1; index < bytes.Length; index++) last = last.Append(bytes.AsMemory(index, 1));
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        Assert.False(frame.Frame.IsSingleSegment); Assert.False(frame.Payload.IsSingleSegment);
        Assert.True(Chinese7937EchoResponse.TryParseFrame(frame, 7937, out var decoded, out var failure), failure.ToString());
        var response = Assert.IsType<Chinese7937EchoResponse>(decoded);
        Assert.Equal(Chinese7937EchoResponseFailure.None, failure);
        Array.Clear(bytes);
        Assert.Equal(Convert.FromHexString(Operation10Golden), response.CopyPayloadBytes());
        Assert.Equal(new Chinese7937EchoArgument[] { new(2, uint.MaxValue), new(3, 0), new(4, 8) }, response.Arguments.ToArray());
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        Assert.False(registry.TryGetHandler(2132, out _));
    }

    private static byte[] FrameBytes(byte[] payload, ushort opcode = 2132)
    {
        var writer = new PacketWriter(opcode, payload.Length, ushort.MaxValue);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static PacketFrame Frame(byte[] payload, ushort opcode = 2132)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(FrameBytes(payload, opcode)),
            out var frame, out _, out _, ushort.MaxValue));
        return frame;
    }

    private sealed class ByteSegment : ReadOnlySequenceSegment<byte>
    {
        internal ByteSegment(ReadOnlyMemory<byte> bytes) => Memory = bytes;

        internal ByteSegment Append(ReadOnlyMemory<byte> nextBytes)
        {
            var next = new ByteSegment(nextBytes) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
