using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyFamilyOccupyType : uint
{
    ShowInformation = 6,
    Join = 8,
}

public readonly record struct LegacyFamilyOccupyPacket(
    LegacyFamilyOccupyType Type,
    uint NpcUid)
{
    private const int TypeOffset = 0;
    private const int ClanIdOffset = TypeOffset + sizeof(uint);
    private const int NpcUidOffset = ClanIdOffset + sizeof(uint);
    private const int Unknown5Offset = NpcUidOffset + sizeof(uint);
    private const int ClanNameOffset = Unknown5Offset + sizeof(uint);
    private const int FixedNameLength = 72;
    private const int MapNameOffset = ClanNameOffset + FixedNameLength;
    private const int AliveOffset = MapNameOffset + FixedNameLength;
    private const int UnknownByteOffset = AliveOffset + sizeof(byte);
    private const int UnknownShortOffset = UnknownByteOffset + sizeof(byte);
    private const int OccupationDaysOffset = UnknownShortOffset + sizeof(ushort);
    private const int RewardOffset = OccupationDaysOffset + sizeof(uint);
    private const int NextRewardOffset = RewardOffset + sizeof(uint);
    private const int ReservedOffset = NextRewardOffset + sizeof(uint);
    private const int ReservedLength = 26;

    public const ushort Opcode = WorldPacketOpcodes.LegacyFamilyOccupy;
    public const int QueryPayloadLength = sizeof(uint) + sizeof(uint);
    public const int ResponsePayloadLength = ReservedOffset + ReservedLength;
    public const string SourceNoClanJoinMessage = "Please make Clan, or join in one Clan.";

    public static bool TryParse(PacketFrame frame, out LegacyFamilyOccupyPacket packet)
    {
        packet = default;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < QueryPayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        packet = new LegacyFamilyOccupyPacket(
            (LegacyFamilyOccupyType)reader.ReadUInt32(TypeOffset),
            reader.ReadUInt32(sizeof(uint)));
        return true;
    }

    public static bool TryParseShowInformation(PacketFrame frame, out LegacyFamilyOccupyShowInformation information)
    {
        information = default;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < ResponsePayloadLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            (LegacyFamilyOccupyType)reader.ReadUInt32(TypeOffset) != LegacyFamilyOccupyType.ShowInformation)
        {
            return false;
        }

        information = new LegacyFamilyOccupyShowInformation(
            ClanId: reader.ReadUInt32(ClanIdOffset),
            NpcUid: reader.ReadUInt32(NpcUidOffset),
            Unknown5: reader.ReadUInt32(Unknown5Offset),
            ClanName: reader.ReadCString(ClanNameOffset, FixedNameLength, Encoding.UTF8),
            MapName: reader.ReadCString(MapNameOffset, FixedNameLength, Encoding.UTF8),
            Alive: reader.Payload[AliveOffset] != 0,
            UnknownByte: reader.Payload[UnknownByteOffset],
            UnknownShort: reader.ReadUInt16(UnknownShortOffset),
            OccupationDays: reader.ReadUInt32(OccupationDaysOffset),
            Reward: reader.ReadUInt32(RewardOffset),
            NextReward: reader.ReadUInt32(NextRewardOffset));
        return true;
    }

    public static byte[] CreateSourceDefaultShowInformationFrame(uint npcUid)
    {
        return CreateShowInformationFrame(CreateSourceDefaultShowInformation(npcUid));
    }

    public static LegacyFamilyOccupyShowInformation CreateSourceDefaultShowInformation(uint npcUid)
    {
        return new LegacyFamilyOccupyShowInformation(
            ClanId: 0,
            NpcUid: npcUid,
            Unknown5: 5,
            ClanName: "None",
            MapName: GetSourceClanWarCityName(npcUid) ?? string.Empty,
            Alive: false,
            UnknownByte: 1,
            UnknownShort: 1,
            OccupationDays: 0,
            Reward: 0,
            NextReward: 0);
    }

    public static byte[] CreateEmptyShowInformationFrame(uint npcUid)
    {
        return CreateSourceDefaultShowInformationFrame(npcUid);
    }

    public static bool TryGetSourceClanWarJoinCity(uint npcUid, out string city)
    {
        // Mirrors MsgClanWar.GetNpcTournament, which FamilyOccupy Join calls with client.ActiveNpc.
        city = npcUid switch
        {
            101888 or 101890 => "Twin",
            101907 => "Phoenix",
            101911 => "Ape",
            101921 => "Desert",
            101915 => "Bird",
            _ => string.Empty,
        };
        return city.Length > 0;
    }

    public static bool TryGetSourceClanWarInformationCity(uint npcUid, out string city, out bool useBestWinner)
    {
        // Mirrors MsgFamilyOccupy.Process: GetNpcTournament first, then GetNpcInformation with Info=true.
        if (TryGetSourceClanWarJoinCity(npcUid, out city))
        {
            useBestWinner = false;
            return true;
        }

        city = npcUid switch
        {
            101905 => "Twin",
            101909 => "Phoenix",
            101913 => "Ape",
            101917 => "Desert",
            101919 => "Bird",
            _ => string.Empty,
        };
        useBestWinner = city.Length > 0;
        return city.Length > 0;
    }

    public static byte[] CreateShowInformationFrame(LegacyFamilyOccupyShowInformation information)
    {
        var writer = new PacketWriter(Opcode, ResponsePayloadLength);
        writer.WriteUInt32(TypeOffset, (uint)LegacyFamilyOccupyType.ShowInformation);
        writer.WriteUInt32(ClanIdOffset, information.ClanId);
        writer.WriteUInt32(NpcUidOffset, information.NpcUid);
        writer.WriteUInt32(Unknown5Offset, information.Unknown5);
        WriteFixedUtf8(writer.Payload.Slice(ClanNameOffset, FixedNameLength), information.ClanName);
        WriteFixedUtf8(writer.Payload.Slice(MapNameOffset, FixedNameLength), information.MapName);
        writer.Payload[AliveOffset] = information.Alive ? (byte)1 : (byte)0;
        writer.Payload[UnknownByteOffset] = information.UnknownByte;
        writer.WriteUInt16(UnknownShortOffset, information.UnknownShort);
        writer.WriteUInt32(OccupationDaysOffset, information.OccupationDays);
        writer.WriteUInt32(RewardOffset, information.Reward);
        writer.WriteUInt32(NextRewardOffset, information.NextReward);
        return writer.ToArray();
    }

    private static string? GetSourceClanWarCityName(uint npcUid)
    {
        // Mirrors MsgClanWar.GetNpcTournament first, then GetNpcInformation.
        return npcUid switch
        {
            101888 or 101890 or 101905 => "Twin",
            101907 or 101909 => "Phoenix",
            101911 or 101913 => "Ape",
            101921 or 101917 => "Desert",
            101915 or 101919 => "Bird",
            _ => null,
        };
    }

    private static void WriteFixedUtf8(Span<byte> destination, string value)
    {
        destination.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}

public readonly record struct LegacyFamilyOccupyShowInformation(
    uint ClanId,
    uint NpcUid,
    uint Unknown5,
    string ClanName,
    string MapName,
    bool Alive,
    byte UnknownByte,
    ushort UnknownShort,
    uint OccupationDays,
    uint Reward,
    uint NextReward);
