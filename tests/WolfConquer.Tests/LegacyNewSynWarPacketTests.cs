using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyNewSynWarPacketTests
{
    private const uint SyndicateId = 1234;

    [Fact]
    public void LegacyNewSynWarOpcodeMatchesLegacySourcePacket()
    {
        Assert.Equal((ushort)2410, WorldPacketOpcodes.LegacyNewSynWar);
        Assert.Equal((ushort)2410, LegacyNewSynWarPacket.Opcode);
    }

    [Fact]
    public void NamedDwParamConstantsMatchLegacySource()
    {
        Assert.Equal(100_009U, LegacyNewSynWarPacket.ExorcimOwnershipDwParam);
        Assert.Equal(100_008U, LegacyNewSynWarPacket.TenacityOwnershipDwParam);
        Assert.Equal(3_000U, LegacyNewSynWarPacket.LeftGateDestroyedDwParam);
        Assert.Equal(3_001U, LegacyNewSynWarPacket.RightGateDestroyedDwParam);
    }

    [Fact]
    public void ExorcimOwnershipFactoryRoundTripsSourceProtobufShape()
    {
        AssertRoundTripsSourceProtobufShape(
            LegacyNewSynWarPacket.ExorcimOwnershipChanged(SyndicateId),
            LegacyNewSynWarPacket.OwnershipChangedType,
            100_009U,
            [
                0x08, 0x09,
                0x18, 0xA9, 0x8D, 0x06,
                0x78, 0xD2, 0x09,
            ],
            expectedOwnershipChanged: true,
            expectedDestroyed: false);
    }

    [Fact]
    public void TenacityOwnershipFactoryRoundTripsSourceProtobufShape()
    {
        AssertRoundTripsSourceProtobufShape(
            LegacyNewSynWarPacket.TenacityOwnershipChanged(SyndicateId),
            LegacyNewSynWarPacket.OwnershipChangedType,
            100_008U,
            [
                0x08, 0x09,
                0x18, 0xA8, 0x8D, 0x06,
                0x78, 0xD2, 0x09,
            ],
            expectedOwnershipChanged: true,
            expectedDestroyed: false);
    }

    [Fact]
    public void LeftGateDestroyedFactoryRoundTripsSourceProtobufShape()
    {
        AssertRoundTripsSourceProtobufShape(
            LegacyNewSynWarPacket.LeftGateDestroyed(SyndicateId),
            LegacyNewSynWarPacket.DestroyedType,
            3_000U,
            [
                0x08, 0x0A,
                0x18, 0xB8, 0x17,
                0x78, 0xD2, 0x09,
            ],
            expectedOwnershipChanged: false,
            expectedDestroyed: true);
    }

    [Fact]
    public void RightGateDestroyedFactoryRoundTripsSourceProtobufShape()
    {
        AssertRoundTripsSourceProtobufShape(
            LegacyNewSynWarPacket.RightGateDestroyed(SyndicateId),
            LegacyNewSynWarPacket.DestroyedType,
            3_001U,
            [
                0x08, 0x0A,
                0x18, 0xB9, 0x17,
                0x78, 0xD2, 0x09,
            ],
            expectedOwnershipChanged: false,
            expectedDestroyed: true);
    }

    [Fact]
    public void ParserRejectsWrongOpcodeAndMalformedPayloads()
    {
        var wrongOpcode = new PacketWriter((ushort)(LegacyNewSynWarPacket.Opcode + 1), payloadLength: 2);
        wrongOpcode.Payload[0] = 0x08;
        wrongOpcode.Payload[1] = 0x09;
        Assert.False(LegacyNewSynWarPacket.TryParse(ReadFrame(wrongOpcode.ToArray()), out _));

        var incompleteVarint = new PacketWriter(LegacyNewSynWarPacket.Opcode, payloadLength: 2);
        incompleteVarint.Payload[0] = 0x08;
        incompleteVarint.Payload[1] = 0x80;
        Assert.False(LegacyNewSynWarPacket.TryParse(ReadFrame(incompleteVarint.ToArray()), out _));

        var missingRequiredType = new PacketWriter(LegacyNewSynWarPacket.Opcode, payloadLength: 2);
        missingRequiredType.Payload[0] = 0x18;
        missingRequiredType.Payload[1] = 0x01;
        Assert.False(LegacyNewSynWarPacket.TryParse(ReadFrame(missingRequiredType.ToArray()), out _));

        var wrongDwParamWireType = new PacketWriter(LegacyNewSynWarPacket.Opcode, payloadLength: 5);
        wrongDwParamWireType.Payload[0] = 0x08;
        wrongDwParamWireType.Payload[1] = 0x09;
        wrongDwParamWireType.Payload[2] = 0x1A;
        wrongDwParamWireType.Payload[3] = 0x01;
        wrongDwParamWireType.Payload[4] = 0x00;
        Assert.False(LegacyNewSynWarPacket.TryParse(ReadFrame(wrongDwParamWireType.ToArray()), out _));
    }

    private static void AssertRoundTripsSourceProtobufShape(
        LegacyNewSynWarPacket packet,
        uint expectedType,
        uint expectedDwParam,
        byte[] expectedPayload,
        bool expectedOwnershipChanged,
        bool expectedDestroyed)
    {
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyNewSynWarPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(expectedPayload, frame.Payload.ToArray());

        Assert.True(LegacyNewSynWarPacket.TryParse(frame, out var parsed));
        Assert.Equal(expectedType, parsed.Type);
        Assert.Equal(expectedDwParam, parsed.DwParam);
        Assert.Equal(SyndicateId, parsed.SyndicateId);
        Assert.Equal(expectedOwnershipChanged, parsed.IsOwnershipChanged);
        Assert.Equal(expectedDestroyed, parsed.IsDestroyed);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
