using System.Buffers;
using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacySpellPacket(
    int Experience,
    ushort Id,
    ushort Unknown,
    ushort Level,
    ushort Type,
    uint UseSoul,
    byte Soul,
    int HashLevelSoul)
{
    private const int ExperienceOffset = 0;
    private const int IdOffset = ExperienceOffset + sizeof(int);
    private const int UnknownOffset = IdOffset + sizeof(ushort);
    private const int LevelOffset = UnknownOffset + sizeof(ushort);
    private const int TypeOffset = LevelOffset + sizeof(ushort);
    private const int UseSoulOffset = TypeOffset + sizeof(ushort);
    private const int SoulOffset = UseSoulOffset + sizeof(uint);
    private const int HashLevelSoulOffset = SoulOffset + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacySpell;
    public const int PayloadLength = HashLevelSoulOffset + sizeof(int);
    public const int LegacySnapshotRecordLength = PlayerSpellSnapshotLoader.LegacySpellSize;

    private const int SnapshotIdOffset = 0;
    private const int SnapshotLevelOffset = SnapshotIdOffset + sizeof(ushort);
    private const int SnapshotExperienceOffset = SnapshotLevelOffset + sizeof(ushort);
    private const int SnapshotPreviousLevelOffset = SnapshotExperienceOffset + sizeof(int);
    private const int SnapshotSoulLevelOffset = SnapshotPreviousLevelOffset + sizeof(byte);
    private const int SnapshotUseSpellSoulOffset = SnapshotSoulLevelOffset + sizeof(byte);

    public static LegacySpellPacket FromSnapshot(PlayerSpellSnapshotEntry spell)
    {
        return new LegacySpellPacket(
            spell.Experience,
            spell.Id,
            Unknown: 0,
            spell.Level,
            Type: 0,
            MapUseSoul(spell.UseSpellSoul),
            Soul: 0,
            ComputeHashLevelSoul(spell.SoulLevel));
    }

    public static byte[] ToLegacySnapshotRecordBytes(PlayerSpellSnapshotEntry spell)
    {
        var bytes = new byte[LegacySnapshotRecordLength];
        WriteLegacySnapshotRecord(bytes, spell);
        return bytes;
    }

    public static void WriteLegacySnapshotRecord(Span<byte> destination, PlayerSpellSnapshotEntry spell)
    {
        if (destination.Length < LegacySnapshotRecordLength)
        {
            throw new ArgumentException(
                $"Destination must be at least {LegacySnapshotRecordLength} bytes.",
                nameof(destination));
        }

        var record = destination[..LegacySnapshotRecordLength];
        record.Clear();
        BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(SnapshotIdOffset, sizeof(ushort)), spell.Id);
        BinaryPrimitives.WriteUInt16LittleEndian(record.Slice(SnapshotLevelOffset, sizeof(ushort)), spell.Level);
        BinaryPrimitives.WriteInt32LittleEndian(record.Slice(SnapshotExperienceOffset, sizeof(int)), spell.Experience);
        record[SnapshotPreviousLevelOffset] = spell.PreviousLevel;
        record[SnapshotSoulLevelOffset] = spell.SoulLevel;
        record[SnapshotUseSpellSoulOffset] = spell.UseSpellSoul;
    }

    public static uint MapUseSoul(byte useSpellSoul)
    {
        return useSpellSoul switch
        {
            1 => 9u,
            2 => 12u,
            3 => 16u,
            4 => 24u,
            _ => 0u,
        };
    }

    public static byte MapUseSoulLevel(uint useSoul)
    {
        return useSoul switch
        {
            9u => 1,
            12u => 2,
            16u => 3,
            24u => 4,
            _ => 0,
        };
    }

    public static bool TryCreateSoulSelectionResponse(
        PlayerSpellSnapshotEntry learnedSpell,
        LegacySpellPacket request,
        out LegacySpellPacket response)
    {
        return TryCreateSoulSelectionResponse(learnedSpell, request, out _, out response);
    }

    public static bool TryCreateSoulSelectionResponse(
        PlayerSpellSnapshotEntry learnedSpell,
        LegacySpellPacket request,
        out PlayerSpellSnapshotEntry updatedSpell,
        out LegacySpellPacket response)
    {
        response = default;
        updatedSpell = default!;
        var selectedSoulLevel = MapUseSoulLevel(request.UseSoul);
        if (learnedSpell.Id != request.Id || learnedSpell.SoulLevel < selectedSoulLevel)
        {
            return false;
        }

        updatedSpell = learnedSpell with { UseSpellSoul = selectedSoulLevel };
        response = request with { Type = 3 };
        return true;
    }

    public static int ComputeHashLevelSoul(byte soulLevel)
    {
        var hash = 0;
        if (soulLevel > 0)
        {
            hash |= 2;
        }

        if (soulLevel > 1)
        {
            hash |= 16;
        }

        if (soulLevel > 2)
        {
            hash |= 256;
        }

        if (soulLevel > 3)
        {
            hash |= 65_536;
        }

        return hash;
    }

    public static bool TryParse(PacketFrame frame, out LegacySpellPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacySpellPacket(
            reader.ReadInt32(ExperienceOffset),
            reader.ReadUInt16(IdOffset),
            reader.ReadUInt16(UnknownOffset),
            reader.ReadUInt16(LevelOffset),
            reader.ReadUInt16(TypeOffset),
            reader.ReadUInt32(UseSoulOffset),
            reader.Payload[SoulOffset],
            reader.ReadInt32(HashLevelSoulOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteInt32(ExperienceOffset, Experience);
        writer.WriteUInt16(IdOffset, Id);
        writer.WriteUInt16(UnknownOffset, Unknown);
        writer.WriteUInt16(LevelOffset, Level);
        writer.WriteUInt16(TypeOffset, Type);
        writer.WriteUInt32(UseSoulOffset, UseSoul);
        writer.Payload[SoulOffset] = Soul;
        writer.WriteInt32(HashLevelSoulOffset, HashLevelSoul);
        return writer.ToArray();
    }
}
