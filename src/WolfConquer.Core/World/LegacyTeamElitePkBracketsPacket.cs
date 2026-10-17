using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyTeamElitePkBracketAction : ushort
{
    InitialList = 0,
    StaticUpdate = 1,
    GuiEdit = 2,
    UpdateList = 3,
    RequestInformation = 4,
    State = 6,
}

public enum LegacyTeamElitePkBracketGuiType : ushort
{
    Top8Ranking = 0,
    Knockout = 3,
    Top8Qualifier = 4,
    Top4Qualifier = 5,
    Top2Qualifier = 6,
    Top3 = 7,
    Top1 = 8,
    ReconstructTop = 9,
}

public enum LegacyTeamElitePkBracketMatchStatus : ushort
{
    AcceptingWagers = 0,
    Watchable = 1,
    Ok = 2,
    SwitchOut = 3,
}

public enum LegacyTeamElitePkBracketFighterStatus : uint
{
    None = 0,
    Waiting = 1,
    Fighting = 2,
    Qualified = 3,
    Lost = 4,
}

public sealed record LegacyTeamElitePkBracketFighter(
    uint Uid,
    uint LeaderUid,
    uint LeaderMesh,
    string Name,
    LegacyTeamElitePkBracketFighterStatus Status);

public sealed record LegacyTeamElitePkBracketMatch(
    uint Id,
    ushort TeamCount,
    ushort Index,
    LegacyTeamElitePkBracketMatchStatus Status,
    IReadOnlyList<LegacyTeamElitePkBracketFighter?> Fighters);

public readonly record struct LegacyTeamElitePkBracketsPacket(
    LegacyTeamElitePkBracketAction Action,
    ushort Page,
    ushort PacketNumber,
    uint MatchCount,
    ushort Group,
    LegacyTeamElitePkBracketGuiType GuiType,
    ushort TimeLeft,
    byte PacketNumber2,
    uint TotalMatches)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int ActionOffset = 0;
    private const int PageOffset = ActionOffset + sizeof(ushort);
    private const int PacketNumberOffset = PageOffset + sizeof(ushort);
    private const int MatchCountOffset = PacketNumberOffset + sizeof(ushort);
    private const int GroupOffset = MatchCountOffset + sizeof(uint);
    private const int GuiTypeOffset = GroupOffset + sizeof(ushort);
    private const int TimeLeftOffset = GuiTypeOffset + sizeof(ushort);
    private const int PacketNumber2Offset = TimeLeftOffset + sizeof(ushort);
    private const int TotalMatchesOffset = PacketNumber2Offset + sizeof(byte);
    private const int MatchIdOffset = 0;
    private const int MatchTeamCountOffset = MatchIdOffset + sizeof(uint);
    private const int MatchIndexOffset = MatchTeamCountOffset + sizeof(ushort);
    private const int MatchStatusOffset = MatchIndexOffset + sizeof(ushort);
    private const int MatchFightersOffset = MatchStatusOffset + sizeof(ushort);
    private const int FighterReservedOffset = 0;
    private const int FighterUidOffset = FighterReservedOffset + sizeof(uint);
    private const int FighterLeaderUidOffset = FighterUidOffset + sizeof(uint);
    private const int FighterLeaderMeshOffset = FighterLeaderUidOffset + sizeof(uint);
    private const int FighterNameOffset = FighterLeaderMeshOffset + sizeof(uint);
    private const int FighterStatusOffset = FighterNameOffset + FighterNameLength;

    public const ushort Opcode = WorldPacketOpcodes.LegacyTeamElitePkBrackets;
    public const int HeaderPayloadLength = TotalMatchesOffset + sizeof(uint);
    public const int PayloadLength = HeaderPayloadLength;
    public const int FighterNameLength = 64;
    public const int FighterSlotCount = 3;
    public const int FighterPayloadLength = FighterStatusOffset + sizeof(uint);
    public const int MatchPayloadLength = MatchFightersOffset + (FighterSlotCount * FighterPayloadLength);

    public static LegacyTeamElitePkBracketsPacket CreateSourceEcho(LegacyTeamElitePkBracketsPacket request)
    {
        return request with { PacketNumber2 = 0 };
    }

    public static bool TryParse(PacketFrame frame, out LegacyTeamElitePkBracketsPacket packet)
    {
        return TryParse(frame, out packet, out _);
    }

    public static bool TryParse(
        PacketFrame frame,
        out LegacyTeamElitePkBracketsPacket packet,
        out IReadOnlyList<LegacyTeamElitePkBracketMatch> matches)
    {
        packet = default;
        matches = [];

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < HeaderPayloadLength ||
            (frame.Payload.Length - HeaderPayloadLength) % MatchPayloadLength != 0 ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var matchCount = checked((frame.Payload.Length - HeaderPayloadLength) / MatchPayloadLength);
        var parsedMatches = new LegacyTeamElitePkBracketMatch[matchCount];
        for (var index = 0; index < parsedMatches.Length; index++)
        {
            parsedMatches[index] = ReadMatch(reader, HeaderPayloadLength + (index * MatchPayloadLength));
        }

        packet = new LegacyTeamElitePkBracketsPacket(
            (LegacyTeamElitePkBracketAction)reader.ReadUInt16(ActionOffset),
            reader.ReadUInt16(PageOffset),
            reader.ReadUInt16(PacketNumberOffset),
            reader.ReadUInt32(MatchCountOffset),
            reader.ReadUInt16(GroupOffset),
            (LegacyTeamElitePkBracketGuiType)reader.ReadUInt16(GuiTypeOffset),
            reader.ReadUInt16(TimeLeftOffset),
            reader.Payload[PacketNumber2Offset],
            reader.ReadUInt32(TotalMatchesOffset));
        matches = parsedMatches;
        return true;
    }

    public byte[] ToFrameBytes()
    {
        return ToFrameBytes([]);
    }

    public byte[] ToFrameBytes(IReadOnlyList<LegacyTeamElitePkBracketMatch> matches)
    {
        ArgumentNullException.ThrowIfNull(matches);

        var writer = new PacketWriter(Opcode, checked(HeaderPayloadLength + (matches.Count * MatchPayloadLength)));
        writer.WriteUInt16(ActionOffset, (ushort)Action);
        writer.WriteUInt16(PageOffset, Page);
        writer.WriteUInt16(PacketNumberOffset, PacketNumber);
        writer.WriteUInt32(MatchCountOffset, MatchCount);
        writer.WriteUInt16(GroupOffset, Group);
        writer.WriteUInt16(GuiTypeOffset, (ushort)GuiType);
        writer.WriteUInt16(TimeLeftOffset, TimeLeft);
        writer.Payload[PacketNumber2Offset] = PacketNumber2;
        writer.WriteUInt32(TotalMatchesOffset, TotalMatches);

        for (var index = 0; index < matches.Count; index++)
        {
            WriteMatch(writer, HeaderPayloadLength + (index * MatchPayloadLength), matches[index]);
        }

        return writer.ToArray();
    }

    private static LegacyTeamElitePkBracketMatch ReadMatch(PacketReader reader, int offset)
    {
        var fighters = new LegacyTeamElitePkBracketFighter?[FighterSlotCount];
        for (var index = 0; index < fighters.Length; index++)
        {
            fighters[index] = ReadFighter(reader, offset + MatchFightersOffset + (index * FighterPayloadLength));
        }

        return new LegacyTeamElitePkBracketMatch(
            reader.ReadUInt32(offset + MatchIdOffset),
            reader.ReadUInt16(offset + MatchTeamCountOffset),
            reader.ReadUInt16(offset + MatchIndexOffset),
            (LegacyTeamElitePkBracketMatchStatus)reader.ReadUInt16(offset + MatchStatusOffset),
            fighters);
    }

    private static LegacyTeamElitePkBracketFighter? ReadFighter(PacketReader reader, int offset)
    {
        var payload = reader.Payload.Slice(offset, FighterPayloadLength);
        if (IsZero(payload))
        {
            return null;
        }

        return new LegacyTeamElitePkBracketFighter(
            reader.ReadUInt32(offset + FighterUidOffset),
            reader.ReadUInt32(offset + FighterLeaderUidOffset),
            reader.ReadUInt32(offset + FighterLeaderMeshOffset),
            reader.ReadCString(offset + FighterNameOffset, FighterNameLength, NameEncoding),
            (LegacyTeamElitePkBracketFighterStatus)reader.ReadUInt32(offset + FighterStatusOffset));
    }

    private static void WriteMatch(PacketWriter writer, int offset, LegacyTeamElitePkBracketMatch match)
    {
        var fighters = match.Fighters ?? [];
        if (fighters.Count > FighterSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(match), fighters.Count, "Legacy team elite bracket matches can serialize at most three fighter slots.");
        }

        writer.WriteUInt32(offset + MatchIdOffset, match.Id);
        writer.WriteUInt16(offset + MatchTeamCountOffset, match.TeamCount);
        writer.WriteUInt16(offset + MatchIndexOffset, match.Index);
        writer.WriteUInt16(offset + MatchStatusOffset, (ushort)match.Status);

        for (var index = 0; index < FighterSlotCount; index++)
        {
            WriteFighter(
                writer,
                offset + MatchFightersOffset + (index * FighterPayloadLength),
                index < fighters.Count ? fighters[index] : null);
        }
    }

    private static void WriteFighter(PacketWriter writer, int offset, LegacyTeamElitePkBracketFighter? fighter)
    {
        writer.Payload.Slice(offset, FighterPayloadLength).Clear();
        if (fighter is null)
        {
            return;
        }

        writer.WriteUInt32(offset + FighterReservedOffset, 0);
        writer.WriteUInt32(offset + FighterUidOffset, fighter.Uid);
        writer.WriteUInt32(offset + FighterLeaderUidOffset, fighter.LeaderUid);
        writer.WriteUInt32(offset + FighterLeaderMeshOffset, fighter.LeaderMesh);
        WriteFixedString(writer.Payload.Slice(offset + FighterNameOffset, FighterNameLength), fighter.Name);
        writer.WriteUInt32(
            offset + FighterStatusOffset,
            (uint)(fighter.Status == LegacyTeamElitePkBracketFighterStatus.None
                ? LegacyTeamElitePkBracketFighterStatus.Waiting
                : fighter.Status));
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var encoder = NameEncoding.GetEncoder();
        encoder.Convert(value.AsSpan(), destination, flush: true, out _, out _, out _);
    }

    private static bool IsZero(ReadOnlySpan<byte> value)
    {
        foreach (var item in value)
        {
            if (item != 0)
            {
                return false;
            }
        }

        return true;
    }
}
