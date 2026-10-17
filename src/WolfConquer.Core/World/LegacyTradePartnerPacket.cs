using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyTradePartnerAction : byte
{
    RequestPartnership = 0,
    RejectRequest = 1,
    AddOnline = 2,
    AddOffline = 3,
    BreakPartnership = 4,
    AddPartner = 5,
}

public sealed record LegacyTradePartnerMemberState(
    uint Uid,
    uint LookFace,
    uint Level,
    uint NobilityRank,
    string Signature,
    string Name)
{
    private const int MeshValueIndex = 11;
    private const int LevelValueIndex = 12;
    private const int NobilityRankValueIndex = 13;

    public static LegacyTradePartnerMemberState FromRecord(SocialAssociationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Values.Count <= NobilityRankValueIndex)
        {
            throw new ArgumentException("Association record does not contain the stored trade-partner fields.", nameof(record));
        }

        return new LegacyTradePartnerMemberState(
            CheckedUInt32(record.TargetId, nameof(record.TargetId)),
            CheckedUInt32(record.Values[MeshValueIndex], "trade-partner lookface"),
            CheckedUInt32(record.Values[LevelValueIndex], "trade-partner level"),
            CheckedUInt32(record.Values[NobilityRankValueIndex], "trade-partner nobility rank"),
            record.Note,
            record.TargetName);
    }

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Trade-partner member value must fit UInt32.");
        }

        return (uint)value;
    }
}

public sealed record LegacyTradePartnerPacket(
    LegacyTradePartnerAction Action,
    uint Uid,
    bool Online,
    ushort HoursLeft,
    uint LookFace,
    uint Level,
    uint NobilityRank,
    uint UserInfo,
    string PersonalSignature,
    string Name)
{
    private const int UidOffset = 0;
    private const int ActionOffset = UidOffset + sizeof(uint);
    private const int OnlineOffset = ActionOffset + sizeof(byte);
    private const int HoursLeftOffset = OnlineOffset + sizeof(byte);
    private const int LookFaceOffset = HoursLeftOffset + sizeof(ushort);
    private const int LevelOffset = LookFaceOffset + sizeof(uint);
    private const int NobilityRankOffset = LevelOffset + sizeof(uint);
    private const int UserInfoOffset = NobilityRankOffset + sizeof(uint);
    private const int PersonalSignatureOffset = UserInfoOffset + sizeof(uint);
    private const int PersonalSignatureLength = 64;
    private const int FixedOneOffset = PersonalSignatureOffset + PersonalSignatureLength;
    private const int NameOffset = FixedOneOffset + sizeof(uint);
    private const int NameLength = 32;

    public const int PayloadLength = NameOffset + NameLength;
    public const ushort Opcode = WorldPacketOpcodes.LegacyTradePartner;

    public static LegacyTradePartnerPacket CreateStartup(
        LegacyTradePartnerMemberState member,
        bool online)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new LegacyTradePartnerPacket(
            LegacyTradePartnerAction.AddPartner,
            member.Uid,
            online,
            HoursLeft: 0,
            member.LookFace,
            member.Level,
            member.NobilityRank,
            UserInfo: 0,
            member.Signature,
            member.Name);
    }

    public static bool TryParse(PacketFrame frame, out LegacyTradePartnerPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode || frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        if (payload[OnlineOffset] > 1 ||
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(FixedOneOffset, sizeof(uint))) != 1 ||
            !Enum.IsDefined(typeof(LegacyTradePartnerAction), payload[ActionOffset]))
        {
            return false;
        }

        packet = new LegacyTradePartnerPacket(
            (LegacyTradePartnerAction)payload[ActionOffset],
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UidOffset, sizeof(uint))),
            payload[OnlineOffset] == 1,
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(HoursLeftOffset, sizeof(ushort))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(LookFaceOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(LevelOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(NobilityRankOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UserInfoOffset, sizeof(uint))),
            ReadFixedString(payload, PersonalSignatureOffset, PersonalSignatureLength),
            ReadFixedString(payload, NameOffset, NameLength));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(UidOffset, Uid);
        writer.Payload[ActionOffset] = (byte)Action;
        writer.Payload[OnlineOffset] = (byte)(Online ? 1 : 0);
        writer.WriteUInt16(HoursLeftOffset, HoursLeft);
        writer.WriteUInt32(LookFaceOffset, LookFace);
        writer.WriteUInt32(LevelOffset, Level);
        writer.WriteUInt32(NobilityRankOffset, NobilityRank);
        writer.WriteUInt32(UserInfoOffset, UserInfo);
        WriteFixedString(writer.Payload, PersonalSignatureOffset, PersonalSignatureLength, PersonalSignature);
        writer.WriteUInt32(FixedOneOffset, 1);
        WriteFixedString(writer.Payload, NameOffset, NameLength, Name);
        return writer.ToArray();
    }

    private static string ReadFixedString(ReadOnlySpan<byte> payload, int offset, int length)
    {
        var bytes = payload.Slice(offset, length);
        var terminator = bytes.IndexOf((byte)0);
        if (terminator >= 0)
        {
            bytes = bytes[..terminator];
        }

        return Encoding.UTF8.GetString(bytes);
    }

    private static void WriteFixedString(Span<byte> payload, int offset, int length, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = Encoding.UTF8.GetBytes(value);
        var count = Math.Min(bytes.Length, length);
        bytes.AsSpan(0, count).CopyTo(payload.Slice(offset, count));
    }
}

public static class LegacyTradePartnerStartupPackets
{
    private const int TradePartnerRelationType = 3;

    public static bool TryCreateTradePartner(
        SocialAssociationRecord record,
        bool targetOnline,
        out LegacyTradePartnerPacket packet)
    {
        ArgumentNullException.ThrowIfNull(record);
        packet = default!;
        if (record.RelationType != TradePartnerRelationType)
        {
            return false;
        }

        packet = LegacyTradePartnerPacket.CreateStartup(
            LegacyTradePartnerMemberState.FromRecord(record),
            targetOnline);
        return true;
    }

    public static IReadOnlyList<LegacyTradePartnerPacket> CreateTradePartnerStartup(
        IEnumerable<SocialAssociationRecord> records,
        uint ownerId,
        IReadOnlySet<uint>? onlineTargetIds = null)
    {
        ArgumentNullException.ThrowIfNull(records);

        var packets = new List<LegacyTradePartnerPacket>();
        foreach (var record in records)
        {
            if (record.OwnerId < 0 || (uint)record.OwnerId != ownerId)
            {
                continue;
            }

            if (TryCreateTradePartner(
                record,
                onlineTargetIds?.Contains(CheckedUInt32(record.TargetId, nameof(record.TargetId))) == true,
                out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Association id must fit UInt32.");
        }

        return (uint)value;
    }
}
