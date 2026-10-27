using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class D13RandomDailyTaskPacketTests
{
    [Fact]
    public void CanonicalPayloadExactBoundProducesTheFullUInt16Envelope()
    {
        Assert.Equal(65531, D13RandomDailyTaskPacket.MaximumPayloadBytes);
        Assert.Equal(32764, D13RandomDailyTaskPacket.MaximumRepeatedValues);
        var values = new uint[32764];
        values[0] = 128; // action2 bytes + first repeated3 + 32763*2 =65531.
        var packet = new D13RandomDailyTaskPacket(0, values8: values);
        var payload = packet.ToPayloadBytes();
        Assert.Equal(65531, payload.Length);
        Assert.Equal(Convert.FromHexString("0800408001"), payload[..5]);
        Assert.Equal(Convert.FromHexString("4000"), payload[^2..]);
        var frame = packet.ToFrameBytes();
        Assert.Equal(65535, frame.Length);
        Assert.Equal(Convert.FromHexString("FFFF73090800408001"), frame[..9]);
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownFrame(Decode(frame), out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(values, parsed.Values8.ToArray());
        Assert.Equal(frame, parsed.ToFrameBytes());
        Assert.Equal(128U, values[0]);
        Assert.All(values[1..], value => Assert.Equal(0U, value));
    }

    [Fact]
    public void RepeatedCountBoundIsCheckedEvenWhenPackedInputFitsTheByteBound()
    {
        var accepted = new D13RandomDailyTaskPacket(0, values8: new uint[32764]);
        Assert.Equal(65530, accepted.ToPayloadBytes().Length);
        D13RandomDailyTaskPacket? rejected = null;
        Assert.Throws<ArgumentOutOfRangeException>(() => rejected = new(0, values8: new uint[32765]));
        Assert.Null(rejected);

        // Packed32765 zero bytes: literal length32765 =FD FF01, well below65531.
        var input = Convert.FromHexString("080042FDFF01").Concat(new byte[32765]).ToArray();
        var before = input.ToArray();
        Assert.InRange(input.Length, 1, 65531);
        Assert.False(D13RandomDailyTaskPacket.TryParseKnownPayload(input, out rejected));
        Assert.Null(rejected);
        Assert.Equal(before, input);

        var packedAtLimit = Convert.FromHexString("080042FCFF01").Concat(new byte[32764]).ToArray();
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownPayload(packedAtLimit, out var parsed));
        Assert.NotNull(parsed);
        Assert.Equal(32764, parsed.Values8.Length);
        Assert.Equal(accepted.ToPayloadBytes(), parsed.ToPayloadBytes());
    }

    [Fact]
    public void NativeUnpackedExpansionIsBoundedBeforeConstructingOrPublishingAParsedModel()
    {
        var values = new uint[32764];
        values[0] = uint.MaxValue; // Canonical65534 bytes despite legal count.
        D13RandomDailyTaskPacket? packet = null;
        Assert.Throws<ArgumentException>(() => packet = new(0, values8: values));
        Assert.Null(packet);
        Assert.Equal(uint.MaxValue, values[0]);

        // Packed length32768 =80 8002, first value5 bytes plus32763 zeros.
        // Input32774 bytes is legal-sized, but its native unpacked output is not.
        var input = Convert.FromHexString("080042808002FFFFFFFF0F").Concat(new byte[32763]).ToArray();
        var preserved = input.ToArray();
        Assert.Equal(32774, input.Length);
        Assert.False(D13RandomDailyTaskPacket.TryParseKnownPayload(input, out packet));
        Assert.Null(packet);
        Assert.Equal(preserved, input);
    }

    [Fact]
    public void OversizedWireInputRefusesWithoutAResultOrCallerBufferChanges()
    {
        var input = new byte[65532];
        input[0] = 8;
        D13RandomDailyTaskPacket? packet = new(6);
        Assert.False(D13RandomDailyTaskPacket.TryParseKnownPayload(input, out packet));
        Assert.Null(packet);
        Assert.Equal((byte)8, input[0]);
        Assert.All(input[1..], value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void CoherentDecoderFramesCheckOpcodeAndDoNotExposeAPartialMalformedPayload()
    {
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownFrame(Decode(Convert.FromHexString("060073090800")), out var packet));
        Assert.NotNull(packet);
        Assert.Equal(0U, packet.Action);
        var wrongOpcode = Convert.FromHexString("060072090800");
        var before = wrongOpcode.ToArray();
        Assert.False(D13RandomDailyTaskPacket.TryParseKnownFrame(Decode(wrongOpcode), out packet));
        Assert.Null(packet);
        Assert.Equal(before, wrongOpcode);
        Assert.False(D13RandomDailyTaskPacket.TryParseKnownFrame(Decode(Convert.FromHexString("07007309080610")), out packet));
        Assert.Null(packet);
    }

    [Fact]
    public void CoherentSegmentedDecoderFrameParsesAcrossPhysicalSegmentBoundaries()
    {
        var first = new BytesSegment(Convert.FromHexString("0A0073"));
        var last = first.Append(Convert.FromHexString("09080610001800"));
        var input = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        Assert.True(PacketFrameDecoder.TryRead(input, out var frame, out _, out _));
        Assert.True(D13RandomDailyTaskPacket.TryParseKnownFrame(frame, out var packet));
        Assert.NotNull(packet);
        Assert.Equal(Convert.FromHexString("080610001800"), packet.ToPayloadBytes());
    }

    private sealed class BytesSegment : ReadOnlySequenceSegment<byte>
    {
        internal BytesSegment(byte[] bytes) => Memory = bytes;
        internal BytesSegment Append(byte[] bytes)
        {
            var next = new BytesSegment(bytes) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
