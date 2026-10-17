using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyProficiencyPacketTests
{
    [Fact]
    public void LegacyProficiencyOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2041, WorldPacketOpcodes.LegacyProficiency);
        Assert.Equal(WorldPacketOpcodes.LegacyProficiency, LegacyProficiencyPacket.Opcode);
    }

    [Fact]
    public void LegacyProficiencyPacketRoundTripsFields()
    {
        var packet = new LegacyProficiencyPacket(
            Id: 410,
            Level: 12,
            Experience: 123_456,
            NeededExperience: 142_500_000);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyProficiencyPacket.TryParse(frame, out var parsed));
        Assert.Equal(packet, parsed);
    }

    [Fact]
    public void LegacyProficiencyPacketWritesLegacyLittleEndianPayloadOrder()
    {
        var packet = new LegacyProficiencyPacket(
            Id: 420,
            Level: 5,
            Experience: 123,
            NeededExperience: 1_600_000);

        Assert.Equal(
        [
            0x14, 0x00, 0xF9, 0x07,
            0xA4, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x00, 0x00,
            0x7B, 0x00, 0x00, 0x00,
            0x00, 0x6A, 0x18, 0x00
        ],
        packet.ToFrameBytes());
    }

    [Fact]
    public void LegacyProficiencyPacketFromSnapshotRecomputesNeededExperienceFromLevel()
    {
        var packet = LegacyProficiencyPacket.FromSnapshot(
            new PlayerProficiencySnapshotEntry(
                Id: 420,
                Level: 5,
                PreviousLevel: 0,
                Experience: 123,
                NeededExperience: 99));

        Assert.Equal(1_600_000U, packet.NeededExperience);
        Assert.Equal(3_200_000_000U, LegacyProficiencyPacket.GetNeededExperience(999));
    }

    [Fact]
    public void LegacyProficiencyApplyExperienceScalesLowLevelWeaponExperienceAndLevels()
    {
        var proficiency = new PlayerProficiencySnapshotEntry(
            Id: 410,
            Level: 4,
            PreviousLevel: 0,
            Experience: 0,
            NeededExperience: 99);

        var result = LegacyProficiencyPacket.ApplyExperience(proficiency, rawExperience: 32_000);

        Assert.True(result.Updated);
        Assert.True(result.LevelChanged);
        Assert.Equal(640_000U, result.AppliedExperience);
        Assert.Equal(5U, result.Proficiency.Level);
        Assert.Equal(0U, result.Proficiency.Experience);
        Assert.Equal(1_600_000U, result.Proficiency.NeededExperience);
        Assert.Equal(LegacyProficiencyPacket.FromSnapshot(result.Proficiency), result.Packet);
    }

    [Fact]
    public void LegacyProficiencyFormatsUpdatedExperienceForLegacySnapshotRecord()
    {
        var proficiency = new PlayerProficiencySnapshotEntry(
            Id: 410,
            Level: 4,
            PreviousLevel: 0,
            Experience: 0,
            NeededExperience: 99);
        var result = LegacyProficiencyPacket.ApplyExperience(proficiency, rawExperience: 32_000);
        var record = Enumerable.Repeat((byte)0xFF, LegacyProficiencyPacket.LegacySnapshotRecordLength).ToArray();

        LegacyProficiencyPacket.WriteLegacySnapshotRecord(record, result.Proficiency);

        Assert.Equal(
        [
            0x9A, 0x01, 0x00, 0x00,
            0x05, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x6A, 0x18, 0x00
        ],
        record);
        Assert.Equal([result.Proficiency], ParseLegacySnapshotRecord(record).Proficiencies);
        Assert.Equal(record, LegacyProficiencyPacket.ToLegacySnapshotRecordBytes(result.Proficiency));
    }

    [Fact]
    public void LegacyProficiencyApplyExperienceScalesHighLevelExperienceAndVioletGemBonus()
    {
        // Source pin: Proficiency.cs adds GetExperience * Owner.GemValues(NormalVioletGem) / 100,
        // but Equip.cs AddGem has no Violet case wiring into Owner.Gems. Keep packet arithmetic
        // available here; runtime socket plumbing needs separate source evidence before use.
        var proficiency = new PlayerProficiencySnapshotEntry(
            Id: 420,
            Level: 14,
            PreviousLevel: 0,
            Experience: 1_000,
            NeededExperience: 99);

        var noBonus = LegacyProficiencyPacket.ApplyExperience(
            proficiency,
            rawExperience: 10_000);
        var result = LegacyProficiencyPacket.ApplyExperience(
            proficiency,
            rawExperience: 10_000,
            violetGemBonusPercent: 50);

        Assert.Equal(6U, noBonus.AppliedExperience);
        Assert.True(result.Updated);
        Assert.False(result.LevelChanged);
        Assert.Equal(9U, result.AppliedExperience);
        Assert.Equal(14U, result.Proficiency.Level);
        Assert.Equal(1_009U, result.Proficiency.Experience);
        Assert.Equal(320_625_000U, result.Proficiency.NeededExperience);
    }

    [Fact]
    public void LegacyProficiencyApplyExperienceRestoresPreviousLevelAtRebornThreshold()
    {
        var proficiency = new PlayerProficiencySnapshotEntry(
            Id: 601,
            Level: 2,
            PreviousLevel: 6,
            Experience: 0,
            NeededExperience: 99);

        var result = LegacyProficiencyPacket.ApplyExperience(proficiency, rawExperience: 2_040);

        Assert.True(result.Updated);
        Assert.True(result.LevelChanged);
        Assert.Equal(6U, result.Proficiency.Level);
        Assert.Equal(0U, result.Proficiency.Experience);
        Assert.Equal(4_000_000U, result.Proficiency.NeededExperience);
    }

    [Fact]
    public void LegacyProficiencyApplyExperienceIgnoresNonWeaponProficiencyIds()
    {
        var proficiency = new PlayerProficiencySnapshotEntry(
            Id: 999,
            Level: 4,
            PreviousLevel: 0,
            Experience: 0,
            NeededExperience: 99);

        var result = LegacyProficiencyPacket.ApplyExperience(proficiency, rawExperience: 32_000);

        Assert.False(LegacyProficiencyPacket.IsLegacyWeaponProficiencyId(999));
        Assert.False(result.Updated);
        Assert.False(result.LevelChanged);
        Assert.Equal(proficiency, result.Proficiency);
    }

    [Fact]
    public void LegacyProficiencyPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyProficiencyPacket.Opcode + 1, LegacyProficiencyPacket.PayloadLength);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyProficiencyPacket.TryParse(frame, out _));
    }

    [Fact]
    public void LegacyProficiencyPacketRejectsShortPayload()
    {
        var writer = new PacketWriter(LegacyProficiencyPacket.Opcode, LegacyProficiencyPacket.PayloadLength - 1);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyProficiencyPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static PlayerProficiencySnapshot ParseLegacySnapshotRecord(byte[] record)
    {
        var bytes = new byte[sizeof(int) + record.Length];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, sizeof(int)), 1);
        record.CopyTo(bytes.AsSpan(sizeof(int)));
        return PlayerProficiencySnapshotLoader.Parse(bytes, "updated-proficiency.bin");
    }
}
