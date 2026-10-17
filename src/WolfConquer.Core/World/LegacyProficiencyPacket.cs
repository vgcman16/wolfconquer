using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyProficiencyPacket(
    uint Id,
    uint Level,
    uint Experience,
    uint NeededExperience)
{
    private const int IdOffset = 0;
    private const int LevelOffset = IdOffset + sizeof(uint);
    private const int ExperienceOffset = LevelOffset + sizeof(uint);
    private const int NeededExperienceOffset = ExperienceOffset + sizeof(uint);

    private static readonly uint[] LegacyLevelExperience =
    [
        0u, 1200u, 68000u, 250000u, 640000u, 1600000u, 4000000u, 10000000u, 22000000u, 40000000u,
        90000000u, 95000000u, 142500000u, 213750000u, 320625000u, 480937500u, 721406250u, 1082109375u, 1623164062u, 2100000000u,
        3200000000u,
    ];

    private static readonly HashSet<uint> LegacyWeaponProficiencyIds =
    [
        0u,
        410u,
        420u,
        421u,
        430u,
        440u,
        441u,
        450u,
        460u,
        470u,
        480u,
        481u,
        490u,
        491u,
        492u,
        493u,
        500u,
        510u,
        511u,
        520u,
        521u,
        522u,
        530u,
        540u,
        560u,
        561u,
        562u,
        570u,
        580u,
        601u,
        610u,
        611u,
        612u,
        613u,
        614u,
        616u,
        617u,
        624u,
        626u,
        670u,
        671u,
        680u,
        681u,
        721u,
        900u,
    ];

    public const ushort Opcode = WorldPacketOpcodes.LegacyProficiency;
    public const int PayloadLength = NeededExperienceOffset + sizeof(uint);
    public const int LegacySnapshotRecordLength = PlayerProficiencySnapshotLoader.LegacyProficiencySize;

    private const int SnapshotIdOffset = 0;
    private const int SnapshotLevelOffset = SnapshotIdOffset + sizeof(uint);
    private const int SnapshotPreviousLevelOffset = SnapshotLevelOffset + sizeof(uint);
    private const int SnapshotExperienceOffset = 12;
    private const int SnapshotNeededExperienceOffset = SnapshotExperienceOffset + sizeof(uint);

    public static LegacyProficiencyPacket FromSnapshot(PlayerProficiencySnapshotEntry proficiency)
    {
        return new LegacyProficiencyPacket(
            proficiency.Id,
            proficiency.Level,
            proficiency.Experience,
            GetNeededExperience(proficiency.Level));
    }

    public static byte[] ToLegacySnapshotRecordBytes(PlayerProficiencySnapshotEntry proficiency)
    {
        var bytes = new byte[LegacySnapshotRecordLength];
        WriteLegacySnapshotRecord(bytes, proficiency);
        return bytes;
    }

    public static void WriteLegacySnapshotRecord(Span<byte> destination, PlayerProficiencySnapshotEntry proficiency)
    {
        if (destination.Length < LegacySnapshotRecordLength)
        {
            throw new ArgumentException(
                $"Destination must be at least {LegacySnapshotRecordLength} bytes.",
                nameof(destination));
        }

        var record = destination[..LegacySnapshotRecordLength];
        record.Clear();
        BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(SnapshotIdOffset, sizeof(uint)), proficiency.Id);
        BinaryPrimitives.WriteUInt32LittleEndian(record.Slice(SnapshotLevelOffset, sizeof(uint)), proficiency.Level);
        record[SnapshotPreviousLevelOffset] = proficiency.PreviousLevel;
        BinaryPrimitives.WriteUInt32LittleEndian(
            record.Slice(SnapshotExperienceOffset, sizeof(uint)),
            proficiency.Experience);
        BinaryPrimitives.WriteUInt32LittleEndian(
            record.Slice(SnapshotNeededExperienceOffset, sizeof(uint)),
            proficiency.NeededExperience);
    }

    public static uint GetNeededExperience(uint level)
    {
        return level >= (uint)LegacyLevelExperience.Length
            ? LegacyLevelExperience[^1]
            : LegacyLevelExperience[(int)level];
    }

    public static bool IsLegacyWeaponProficiencyId(uint id)
    {
        return LegacyWeaponProficiencyIds.Contains(id);
    }

    public static LegacyProficiencyExperienceResult ApplyExperience(
        PlayerProficiencySnapshotEntry proficiency,
        uint rawExperience,
        uint violetGemBonusPercent = 0)
    {
        if (rawExperience == 0 ||
            !IsLegacyWeaponProficiencyId(proficiency.Id) ||
            proficiency.Level >= 20)
        {
            return CreateExperienceResult(proficiency, 0, updated: false, levelChanged: false);
        }

        var scaledExperience = ScaleLegacyProficiencyExperience(rawExperience, proficiency.Level);
        var appliedExperience = AddSaturating(
            scaledExperience,
            GetPercentBonus(scaledExperience, violetGemBonusPercent));

        var experience = AddSaturating(proficiency.Experience, appliedExperience);
        var level = proficiency.Level;
        var levelChanged = false;

        while (level < 20)
        {
            var neededExperience = GetNeededExperience(level);
            if (neededExperience == 0 || experience < neededExperience)
            {
                break;
            }

            experience -= neededExperience;
            level++;
            levelChanged = true;

            if (level == 20)
            {
                experience = 0;
                break;
            }

            if (proficiency.PreviousLevel != 0 &&
                level >= proficiency.PreviousLevel / 2 &&
                level < proficiency.PreviousLevel)
            {
                level = proficiency.PreviousLevel;
                experience = 0;
            }
        }

        var updatedProficiency = proficiency with
        {
            Level = level,
            Experience = experience,
            NeededExperience = GetNeededExperience(level),
        };

        return CreateExperienceResult(updatedProficiency, appliedExperience, updated: true, levelChanged);
    }

    public static bool TryParse(PacketFrame frame, out LegacyProficiencyPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyProficiencyPacket(
            reader.ReadUInt32(IdOffset),
            reader.ReadUInt32(LevelOffset),
            reader.ReadUInt32(ExperienceOffset),
            reader.ReadUInt32(NeededExperienceOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(IdOffset, Id);
        writer.WriteUInt32(LevelOffset, Level);
        writer.WriteUInt32(ExperienceOffset, Experience);
        writer.WriteUInt32(NeededExperienceOffset, NeededExperience);
        return writer.ToArray();
    }

    private static uint ScaleLegacyProficiencyExperience(uint rawExperience, uint level)
    {
        var scaled = (ulong)rawExperience;
        if (level < 5)
        {
            scaled *= 100;
        }
        else if (level > 13)
        {
            scaled /= 100;
        }

        scaled /= level + 1UL;
        return scaled > uint.MaxValue ? uint.MaxValue : (uint)scaled;
    }

    private static uint GetPercentBonus(uint value, uint percent)
    {
        var bonus = (ulong)value * percent / 100;
        return bonus > uint.MaxValue ? uint.MaxValue : (uint)bonus;
    }

    private static uint AddSaturating(uint left, uint right)
    {
        var sum = (ulong)left + right;
        return sum > uint.MaxValue ? uint.MaxValue : (uint)sum;
    }

    private static LegacyProficiencyExperienceResult CreateExperienceResult(
        PlayerProficiencySnapshotEntry proficiency,
        uint appliedExperience,
        bool updated,
        bool levelChanged)
    {
        return new LegacyProficiencyExperienceResult(
            proficiency,
            FromSnapshot(proficiency),
            appliedExperience,
            updated,
            levelChanged);
    }
}

public readonly record struct LegacyProficiencyExperienceResult(
    PlayerProficiencySnapshotEntry Proficiency,
    LegacyProficiencyPacket Packet,
    uint AppliedExperience,
    bool Updated,
    bool LevelChanged);
