using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyTeamMemberInfoPacketTests
{
    [Fact]
    public void OpcodeConstantsAndActionValuesMatchLegacySource()
    {
        Assert.Equal((ushort)2074, LegacyTeamMemberInfoPacket.Opcode);
        Assert.Equal(WorldPacketOpcodes.LegacyTeamMemberInfo, LegacyTeamMemberInfoPacket.Opcode);
        Assert.Equal(5, LegacyTeamMemberInfoPacket.SlotCount);
        Assert.Equal(48, LegacyTeamMemberInfoPacket.SlotLength);
        Assert.Equal(244, LegacyTeamMemberInfoPacket.PayloadLength);
        Assert.Equal((byte)0, (byte)LegacyTeamMemberInfoAction.AddMember);
        Assert.Equal((byte)1, (byte)LegacyTeamMemberInfoAction.DropAddMember);
    }

    [Fact]
    public void OnePopulatedMemberRoundTripsAndZeroFillsUnusedSlots()
    {
        var packet = new LegacyTeamMemberInfoPacket(
            LegacyTeamMemberInfoAction.AddMember,
            Count: 1,
            Unknown: 1,
            [
                new LegacyTeamMemberInfoEntry(
                    Name: "HeroOne",
                    Uid: 0x01020304,
                    Mesh: 0x11121314,
                    MaxHitpoints: 0x21222324,
                    MinHitpoints: 0x31323334),
            ]);

        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyTeamMemberInfoPacket.Opcode, frame.Header.Opcode);
        Assert.Equal(LegacyTeamMemberInfoPacket.PayloadLength, frame.Payload.Length);
        Assert.True(LegacyTeamMemberInfoPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Action, parsed.Action);
        Assert.Equal(packet.Count, parsed.Count);
        Assert.Equal(packet.Unknown, parsed.Unknown);
        Assert.Equal(packet.Entries, parsed.Entries);

        var payload = frame.Payload.ToArray();
        for (var slotIndex = 1; slotIndex < LegacyTeamMemberInfoPacket.SlotCount; slotIndex++)
        {
            var slotOffset = 4 + (slotIndex * LegacyTeamMemberInfoPacket.SlotLength);
            Assert.All(
                payload.AsSpan(slotOffset, LegacyTeamMemberInfoPacket.SlotLength).ToArray(),
                value => Assert.Equal(0, value));
        }
    }

    [Fact]
    public void FiveMembersRoundTripExactlyAndPreserveUnknown()
    {
        var packet = new LegacyTeamMemberInfoPacket(
            LegacyTeamMemberInfoAction.DropAddMember,
            Count: 5,
            Unknown: 0xBEEF,
            Enumerable.Range(0, 5)
                .Select(index => new LegacyTeamMemberInfoEntry(
                    Name: "Member" + index,
                    Uid: 1000U + (uint)index,
                    Mesh: 2000U + (uint)index,
                    MaxHitpoints: 3000U + (uint)index,
                    MinHitpoints: 4000U + (uint)index))
                .ToArray());

        var bytes = packet.ToFrameBytes();

        Assert.True(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(LegacyTeamMemberInfoAction.DropAddMember, parsed.Action);
        Assert.Equal((byte)5, parsed.Count);
        Assert.Equal((ushort)0xBEEF, parsed.Unknown);
        Assert.Equal(packet.Entries, parsed.Entries);
        Assert.Equal(bytes, parsed.ToFrameBytes());
    }

    [Fact]
    public void WriterRejectsCountMismatchAndTooManyEntries()
    {
        var mismatch = new LegacyTeamMemberInfoPacket(
            LegacyTeamMemberInfoAction.AddMember,
            Count: 2,
            Unknown: 1,
            [new LegacyTeamMemberInfoEntry("Member", 1, 2, 3, 4)]);

        Assert.Throws<InvalidOperationException>(() => mismatch.ToFrameBytes());

        var tooMany = new LegacyTeamMemberInfoPacket(
            LegacyTeamMemberInfoAction.AddMember,
            Count: 6,
            Unknown: 1,
            Enumerable.Range(0, 6)
                .Select(index => new LegacyTeamMemberInfoEntry("Member" + index, 1, 2, 3, 4))
                .ToArray());

        Assert.Throws<InvalidOperationException>(() => tooMany.ToFrameBytes());
    }

    [Fact]
    public void ParserRejectsCountGreaterThanSlotCount()
    {
        var writer = new PacketWriter(LegacyTeamMemberInfoPacket.Opcode, LegacyTeamMemberInfoPacket.PayloadLength);
        writer.Payload[0] = (byte)LegacyTeamMemberInfoAction.AddMember;
        writer.Payload[1] = 6;
        writer.WriteUInt16(2, 1);

        Assert.False(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(writer.ToArray()), out _));
    }

    [Fact]
    public void ParserRejectsWrongOpcodeAndShortPayload()
    {
        var wrongOpcode = new PacketWriter(
            (ushort)(LegacyTeamMemberInfoPacket.Opcode + 1),
            LegacyTeamMemberInfoPacket.PayloadLength);
        Assert.False(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(wrongOpcode.ToArray()), out _));

        var shortPayload = new PacketWriter(
            LegacyTeamMemberInfoPacket.Opcode,
            LegacyTeamMemberInfoPacket.PayloadLength - 1);
        Assert.False(LegacyTeamMemberInfoPacket.TryParse(ReadFrame(shortPayload.ToArray()), out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
