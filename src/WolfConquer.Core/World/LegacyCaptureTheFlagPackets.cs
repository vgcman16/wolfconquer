using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyCaptureTheFlagUpdateMode : uint
{
    InitializeCTF = 0,
    ScoreBase = 1,
    ScoreUpdate = 2,
    OccupiedBase = 5,
    GenerateEffect = 6,
    RemoveFlagEffect = 7,
    GenerateTimer = 8,
    InitializeRank = 11,
    X2Location = 12,
}

public readonly record struct LegacyCaptureTheFlagUpdatePacket(
    LegacyCaptureTheFlagUpdateMode Mode,
    uint Parameter,
    ushort Parameter2,
    ushort Count)
{
    private const int ModeOffset = 0;
    private const int ParameterOffset = ModeOffset + sizeof(uint);
    private const int ReservedOffset = ParameterOffset + sizeof(uint);
    private const int Parameter2Offset = ReservedOffset + 16;
    private const int CountOffset = Parameter2Offset + sizeof(ushort);

    public const ushort Opcode = 2062;
    public const int PayloadLength = CountOffset + sizeof(ushort);
    public const int ScoreEntryLength = sizeof(uint) + sizeof(uint) + sizeof(uint) + ScoreEntryNameLength;
    private const int ScoreEntryNameLength = 32;

    public static LegacyCaptureTheFlagUpdatePacket GenerateTimer(uint seconds, uint uid)
    {
        return new LegacyCaptureTheFlagUpdatePacket(
            LegacyCaptureTheFlagUpdateMode.GenerateTimer,
            seconds,
            unchecked((ushort)uid),
            Count: 1);
    }

    public static LegacyCaptureTheFlagUpdatePacket GenerateEffect(uint uid)
    {
        return new LegacyCaptureTheFlagUpdatePacket(
            LegacyCaptureTheFlagUpdateMode.GenerateEffect,
            uid,
            Parameter2: 0,
            Count: 1);
    }

    public static LegacyCaptureTheFlagUpdatePacket RemoveFlagEffect(uint uid)
    {
        return new LegacyCaptureTheFlagUpdatePacket(
            LegacyCaptureTheFlagUpdateMode.RemoveFlagEffect,
            uid,
            Parameter2: 0,
            Count: 1);
    }

    public static LegacyCaptureTheFlagUpdatePacket InitializeRank(bool isAlive)
    {
        return new LegacyCaptureTheFlagUpdatePacket(
            LegacyCaptureTheFlagUpdateMode.InitializeRank,
            isAlive ? 1u : 0u,
            Parameter2: 0,
            Count: 1);
    }

    public static byte[] ScoreBase(IReadOnlyList<LegacyCaptureTheFlagScoreEntry> entries)
    {
        var cappedEntries = entries
            .Take(5)
            .ToArray();
        var writer = new PacketWriter(
            Opcode,
            checked(PayloadLength + cappedEntries.Length * ScoreEntryLength));
        writer.WriteUInt32(ModeOffset, (uint)LegacyCaptureTheFlagUpdateMode.ScoreBase);
        writer.WriteUInt32(ParameterOffset, 0);
        writer.WriteUInt16(Parameter2Offset, (ushort)cappedEntries.Length);
        writer.WriteUInt16(CountOffset, 1);

        for (var index = 0; index < cappedEntries.Length; index++)
        {
            var entry = cappedEntries[index];
            var offset = PayloadLength + index * ScoreEntryLength;
            writer.WriteUInt32(offset, entry.Rank);
            writer.WriteUInt32(offset + sizeof(uint), entry.Score);
            writer.WriteUInt32(offset + sizeof(uint) + sizeof(uint), 0);
            var name = Encoding.ASCII.GetBytes(entry.GuildName);
            name.AsSpan(0, Math.Min(name.Length, ScoreEntryNameLength))
                .CopyTo(writer.Payload.Slice(
                    offset + sizeof(uint) + sizeof(uint) + sizeof(uint),
                    ScoreEntryNameLength));
        }

        return writer.ToArray();
    }

    public static bool TryParse(PacketFrame frame, out LegacyCaptureTheFlagUpdatePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyCaptureTheFlagUpdatePacket(
            (LegacyCaptureTheFlagUpdateMode)reader.ReadUInt32(ModeOffset),
            reader.ReadUInt32(ParameterOffset),
            reader.ReadUInt16(Parameter2Offset),
            reader.ReadUInt16(CountOffset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(ModeOffset, (uint)Mode);
        writer.WriteUInt32(ParameterOffset, Parameter);
        writer.WriteUInt16(Parameter2Offset, Parameter2);
        writer.WriteUInt16(CountOffset, Count);
        return writer.ToArray();
    }
}

public readonly record struct LegacyCaptureTheFlagScoreEntry(
    uint Rank,
    uint Score,
    string GuildName);

public enum LegacyCaptureTheFlagRankingAction : ushort
{
    RewardRanking = 0,
    Info = 1,
    SetRewardConquerPoints = 3,
    SetRewardMoney = 4,
    SetRewardAll = 5,
    RankMembers = 8,
    ArenaGui = 9,
    UpdatePoint = 13,
}

public readonly record struct LegacyCaptureTheFlagRankingPacket(
    LegacyCaptureTheFlagRankingAction Action,
    uint Page,
    uint Parameter1,
    uint Parameter2,
    ushort Marker,
    uint Parameter3,
    ulong Parameter4)
{
    private const int ActionOffset = 0;
    private const int PageOffset = ActionOffset + sizeof(ushort);
    private const int Parameter1Offset = PageOffset + sizeof(uint);
    private const int Parameter2Offset = Parameter1Offset + sizeof(uint);
    private const int MarkerOffset = Parameter2Offset + sizeof(uint);
    private const int Parameter3Offset = MarkerOffset + sizeof(ushort);
    private const int Parameter4Offset = Parameter3Offset + sizeof(uint);

    public const ushort Opcode = 2181;
    public const int PayloadLength = Parameter4Offset + sizeof(ulong);

    public static LegacyCaptureTheFlagRankingPacket UpdatePoint(uint points)
    {
        return new LegacyCaptureTheFlagRankingPacket(
            LegacyCaptureTheFlagRankingAction.UpdatePoint,
            Page: 1,
            Parameter1: 0,
            Parameter2: 0,
            Marker: 1,
            points,
            Parameter4: 0);
    }

    public static bool TryParse(PacketFrame frame, out LegacyCaptureTheFlagRankingPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyCaptureTheFlagRankingPacket(
            (LegacyCaptureTheFlagRankingAction)reader.ReadUInt16(ActionOffset),
            reader.ReadUInt32(PageOffset),
            reader.ReadUInt32(Parameter1Offset),
            reader.ReadUInt32(Parameter2Offset),
            reader.ReadUInt16(MarkerOffset),
            reader.ReadUInt32(Parameter3Offset),
            reader.ReadUInt64(Parameter4Offset));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt16(ActionOffset, (ushort)Action);
        writer.WriteUInt32(PageOffset, Page);
        writer.WriteUInt32(Parameter1Offset, Parameter1);
        writer.WriteUInt32(Parameter2Offset, Parameter2);
        writer.WriteUInt16(MarkerOffset, Marker);
        writer.WriteUInt32(Parameter3Offset, Parameter3);
        writer.WriteUInt64(Parameter4Offset, Parameter4);
        return writer.ToArray();
    }
}
