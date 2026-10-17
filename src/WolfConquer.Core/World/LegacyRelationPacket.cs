using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyRelationPacket(
    uint RequesterUid,
    uint ReceiverUid,
    uint RequesterLevel,
    uint RequesterBattlePower,
    bool SameSpouse,
    bool Friend,
    bool TradePartner,
    bool MentorApprentice,
    bool Teammate,
    bool GuildMember,
    bool Enemy,
    byte Unknown1,
    byte Unknown2,
    byte Unknown3,
    string ReceiverName)
{
    private static readonly Encoding TextEncoding = Encoding.UTF8;

    private const int RequesterUidOffset = 0;
    private const int ReceiverUidOffset = 4;
    private const int RequesterLevelOffset = 8;
    private const int RequesterBattlePowerOffset = 12;
    private const int SameSpouseOffset = 16;
    private const int FriendOffset = 17;
    private const int TradePartnerOffset = 18;
    private const int MentorApprenticeOffset = 19;
    private const int TeammateOffset = 20;
    private const int GuildMemberOffset = 21;
    private const int EnemyOffset = 22;
    private const int Unknown1Offset = 23;
    private const int Unknown2Offset = 24;
    private const int Unknown3Offset = 25;
    private const int StringListCountOffset = 26;
    private const int StringListLengthOffset = 27;
    private const int StringListValueOffset = 29;
    private const int MinimumPayloadLength = StringListValueOffset;

    public const ushort Opcode = WorldPacketOpcodes.LegacyRelation;

    public static LegacyRelationPacket CreateTeamRequest(
        uint requesterUid,
        uint receiverUid,
        uint requesterLevel,
        uint requesterBattlePower,
        string receiverName,
        bool teammate = false)
    {
        return new LegacyRelationPacket(
            requesterUid,
            receiverUid,
            requesterLevel,
            requesterBattlePower,
            SameSpouse: false,
            Friend: false,
            TradePartner: false,
            MentorApprentice: false,
            teammate,
            GuildMember: false,
            Enemy: false,
            Unknown1: 0,
            Unknown2: 0,
            Unknown3: 0,
            receiverName);
    }

    public static bool TryParse(PacketFrame frame, out LegacyRelationPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < MinimumPayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var nameLength = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(StringListLengthOffset, sizeof(ushort)));
        if (payload[StringListCountOffset] != 1 ||
            payload.Length != MinimumPayloadLength + nameLength)
        {
            return false;
        }

        packet = new LegacyRelationPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(RequesterUidOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ReceiverUidOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(RequesterLevelOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(RequesterBattlePowerOffset, sizeof(uint))),
            payload[SameSpouseOffset] != 0,
            payload[FriendOffset] != 0,
            payload[TradePartnerOffset] != 0,
            payload[MentorApprenticeOffset] != 0,
            payload[TeammateOffset] != 0,
            payload[GuildMemberOffset] != 0,
            payload[EnemyOffset] != 0,
            payload[Unknown1Offset],
            payload[Unknown2Offset],
            payload[Unknown3Offset],
            TextEncoding.GetString(payload, StringListValueOffset, nameLength));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var nameBytes = TextEncoding.GetBytes(ReceiverName ?? string.Empty);
        if (nameBytes.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException("Legacy relation receiver name is too long.");
        }

        var writer = new PacketWriter(Opcode, MinimumPayloadLength + nameBytes.Length);
        writer.WriteUInt32(RequesterUidOffset, RequesterUid);
        writer.WriteUInt32(ReceiverUidOffset, ReceiverUid);
        writer.WriteUInt32(RequesterLevelOffset, RequesterLevel);
        writer.WriteUInt32(RequesterBattlePowerOffset, RequesterBattlePower);
        writer.Payload[SameSpouseOffset] = ToByte(SameSpouse);
        writer.Payload[FriendOffset] = ToByte(Friend);
        writer.Payload[TradePartnerOffset] = ToByte(TradePartner);
        writer.Payload[MentorApprenticeOffset] = ToByte(MentorApprentice);
        writer.Payload[TeammateOffset] = ToByte(Teammate);
        writer.Payload[GuildMemberOffset] = ToByte(GuildMember);
        writer.Payload[EnemyOffset] = ToByte(Enemy);
        writer.Payload[Unknown1Offset] = Unknown1;
        writer.Payload[Unknown2Offset] = Unknown2;
        writer.Payload[Unknown3Offset] = Unknown3;
        writer.Payload[StringListCountOffset] = 1;
        writer.WriteUInt16(StringListLengthOffset, checked((ushort)nameBytes.Length));
        nameBytes.CopyTo(writer.Payload[StringListValueOffset..]);
        return writer.ToArray();
    }

    private static byte ToByte(bool value)
    {
        return value ? (byte)1 : (byte)0;
    }
}
