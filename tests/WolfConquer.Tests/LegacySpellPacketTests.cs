using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySpellPacketTests
{
    [Fact]
    public void LegacySpellOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2101, WorldPacketOpcodes.LegacySpell);
        Assert.Equal(WorldPacketOpcodes.LegacySpell, LegacySpellPacket.Opcode);
    }

    [Fact]
    public void LegacySpellPacketRoundTripsFields()
    {
        var packet = new LegacySpellPacket(
            Experience: 123_456,
            Id: 10_311,
            Unknown: 0,
            Level: 3,
            Type: 0,
            UseSoul: 12,
            Soul: 0,
            HashLevelSoul: 65_810);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacySpellPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacySpellPacketWritesLegacyLittleEndianPayloadOrder()
    {
        var packet = new LegacySpellPacket(
            Experience: 123_456,
            Id: 10_311,
            Unknown: 0,
            Level: 3,
            Type: 0,
            UseSoul: 12,
            Soul: 0,
            HashLevelSoul: 65_810);

        Assert.Equal(
        [
            0x19, 0x00, 0x35, 0x08,
            0x40, 0xE2, 0x01, 0x00,
            0x47, 0x28,
            0x00, 0x00,
            0x03, 0x00,
            0x00, 0x00,
            0x0C, 0x00, 0x00, 0x00,
            0x00,
            0x12, 0x01, 0x01, 0x00
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void LegacySpellPacketFromSnapshotMapsSoulFields()
    {
        var packet = LegacySpellPacket.FromSnapshot(
            new PlayerSpellSnapshotEntry(
                Id: 10_311,
                Level: 3,
                Experience: 123_456,
                PreviousLevel: 1,
                SoulLevel: 4,
                UseSpellSoul: 2));

        Assert.Equal(12U, packet.UseSoul);
        Assert.Equal(0, packet.Soul);
        Assert.Equal(65_810, packet.HashLevelSoul);
        Assert.Equal(0U, LegacySpellPacket.MapUseSoul(99));
    }

    [Fact]
    public void LegacySpellFormatsUpdatedExperienceForLegacySnapshotRecord()
    {
        var spell = new PlayerSpellSnapshotEntry(
            Id: 10_311,
            Level: 4,
            Experience: 123_456,
            PreviousLevel: 1,
            SoulLevel: 3,
            UseSpellSoul: 2);
        var record = Enumerable.Repeat((byte)0xFF, LegacySpellPacket.LegacySnapshotRecordLength).ToArray();

        LegacySpellPacket.WriteLegacySnapshotRecord(record, spell);

        Assert.Equal(
        [
            0x47, 0x28,
            0x04, 0x00,
            0x40, 0xE2, 0x01, 0x00,
            0x01,
            0x03,
            0x02,
            0x00
        ],
        record);
        Assert.Equal([spell], ParseLegacySnapshotRecord(record).Spells);
        Assert.Equal(record, LegacySpellPacket.ToLegacySnapshotRecordBytes(spell));
    }

    [Fact]
    public void LegacySpellPacketCreatesSoulSelectionTypeThreeConfirmWhenSoulIsLearned()
    {
        var learnedSpell = new PlayerSpellSnapshotEntry(
            Id: 10_311,
            Level: 3,
            Experience: 123_456,
            PreviousLevel: 1,
            SoulLevel: 3,
            UseSpellSoul: 1);
        var request = LegacySpellPacket.FromSnapshot(learnedSpell) with
        {
            Type = 0,
            UseSoul = LegacySpellPacket.MapUseSoul(3),
        };

        Assert.True(LegacySpellPacket.TryCreateSoulSelectionResponse(
            learnedSpell,
            request,
            out var updatedSpell,
            out var response));

        Assert.Equal(learnedSpell with { UseSpellSoul = 3 }, updatedSpell);
        Assert.Equal(request with { Type = 3 }, response);
        Assert.Equal(3, LegacySpellPacket.MapUseSoulLevel(response.UseSoul));
    }

    [Fact]
    public void LegacySpellPacketParsesSoulSelectionRequestAndWritesTypeThreeConfirmation()
    {
        var requestBytes = new byte[]
        {
            0x19, 0x00, 0x35, 0x08,
            0x40, 0xE2, 0x01, 0x00,
            0x47, 0x28,
            0x00, 0x00,
            0x03, 0x00,
            0x00, 0x00,
            0x10, 0x00, 0x00, 0x00,
            0x00,
            0x12, 0x01, 0x01, 0x00
        };
        var expectedRequest = new LegacySpellPacket(
            Experience: 123_456,
            Id: 10_311,
            Unknown: 0,
            Level: 3,
            Type: 0,
            UseSoul: 16,
            Soul: 0,
            HashLevelSoul: 65_810);
        var learnedSpell = new PlayerSpellSnapshotEntry(
            Id: 10_311,
            Level: 3,
            Experience: 123_456,
            PreviousLevel: 1,
            SoulLevel: 3,
            UseSpellSoul: 1);

        Assert.True(LegacySpellPacket.TryParse(ReadFrame(requestBytes), out var request));
        Assert.Equal(expectedRequest, request);
        Assert.True(LegacySpellPacket.TryCreateSoulSelectionResponse(
            learnedSpell,
            request,
            out var updatedSpell,
            out var response));

        Assert.Equal(learnedSpell with { UseSpellSoul = 3 }, updatedSpell);
        Assert.Equal(expectedRequest with { Type = 3 }, response);
        Assert.Equal(
        [
            0x19, 0x00, 0x35, 0x08,
            0x40, 0xE2, 0x01, 0x00,
            0x47, 0x28,
            0x00, 0x00,
            0x03, 0x00,
            0x03, 0x00,
            0x10, 0x00, 0x00, 0x00,
            0x00,
            0x12, 0x01, 0x01, 0x00
        ],
        response.ToFrameBytes());
    }

    [Fact]
    public void LegacySpellPacketRejectsSoulSelectionAboveLearnedSoulLevel()
    {
        var learnedSpell = new PlayerSpellSnapshotEntry(
            Id: 10_311,
            Level: 3,
            Experience: 123_456,
            PreviousLevel: 1,
            SoulLevel: 2,
            UseSpellSoul: 1);
        var request = LegacySpellPacket.FromSnapshot(learnedSpell) with
        {
            UseSoul = LegacySpellPacket.MapUseSoul(4),
        };

        Assert.False(LegacySpellPacket.TryCreateSoulSelectionResponse(learnedSpell, request, out _));
    }

    [Fact]
    public void LegacySpellPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacySpellPacket.Opcode + 1, LegacySpellPacket.PayloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacySpellPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacySpellPacketRejectsShortPayload()
    {
        var writer = new PacketWriter(LegacySpellPacket.Opcode, LegacySpellPacket.PayloadLength - 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacySpellPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static PlayerSpellSnapshot ParseLegacySnapshotRecord(byte[] record)
    {
        var bytes = new byte[sizeof(int) + record.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, sizeof(int)), 1);
        record.CopyTo(bytes.AsSpan(sizeof(int)));
        return PlayerSpellSnapshotLoader.Parse(bytes, "updated-spell.bin");
    }
}
