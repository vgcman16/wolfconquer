using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyKnownPersonAction : byte
{
    RequestFriendship = 10,
    AcceptRequest = 11,
    AddOnline = 12,
    AddOffline = 13,
    RemovePerson = 14,
    AddFriend = 15,
    RemoveEnemy = 18,
    AddEnemy = 19,
}

public sealed record LegacyAssociationMemberState(
    uint Uid,
    uint Mesh,
    uint Level,
    uint NobilityRank,
    string Signature,
    string Name)
{
    private const int MeshValueIndex = 11;
    private const int LevelValueIndex = 12;
    private const int NobilityRankValueIndex = 13;

    public static LegacyAssociationMemberState FromRecord(SocialAssociationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.Values.Count <= NobilityRankValueIndex)
        {
            throw new ArgumentException("Association record does not contain the stored member fields.", nameof(record));
        }

        return new LegacyAssociationMemberState(
            CheckedUInt32(record.TargetId, nameof(record.TargetId)),
            CheckedUInt32(record.Values[MeshValueIndex], "association mesh"),
            CheckedUInt32(record.Values[LevelValueIndex], "association level"),
            CheckedUInt32(record.Values[NobilityRankValueIndex], "association nobility rank"),
            record.Note,
            record.TargetName);
    }

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Association member value must fit UInt32.");
        }

        return (uint)value;
    }
}

public sealed record LegacyKnownPersonPacket(
    LegacyKnownPersonAction Action,
    uint Uid,
    bool Online,
    uint NobilityRank,
    uint UserInfo,
    uint LookFace,
    uint Level,
    string PersonalSignature,
    string Name)
{
    private const int UidOffset = 0;
    private const int ActionOffset = UidOffset + sizeof(uint);
    private const int OnlineOffset = ActionOffset + sizeof(byte);
    private const int ReservedOffset = OnlineOffset + sizeof(byte);
    private const int NobilityRankOffset = ReservedOffset + sizeof(ushort);
    private const int UserInfoOffset = NobilityRankOffset + sizeof(uint);
    private const int LookFaceOffset = UserInfoOffset + sizeof(uint);
    private const int LevelOffset = LookFaceOffset + sizeof(uint);
    private const int PersonalSignatureOffset = LevelOffset + sizeof(uint);
    private const int PersonalSignatureLength = 64;
    private const int FixedOneOffset = PersonalSignatureOffset + PersonalSignatureLength;
    private const int NameOffset = FixedOneOffset + sizeof(uint);
    private const int NameLength = 32;

    public const int PayloadLength = NameOffset + NameLength;
    public const ushort Opcode = WorldPacketOpcodes.LegacyKnownPerson;

    public static LegacyKnownPersonPacket CreateStartup(
        LegacyAssociationMemberState member,
        LegacyKnownPersonAction action,
        bool online)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (action is not (LegacyKnownPersonAction.AddFriend or LegacyKnownPersonAction.AddEnemy))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Startup known-person action must add a friend or enemy.");
        }

        return new LegacyKnownPersonPacket(
            action,
            member.Uid,
            online,
            online ? member.NobilityRank : 0,
            UserInfo: 0,
            online ? member.Mesh : 0,
            online ? member.Level : 0,
            online ? member.Signature : string.Empty,
            member.Name);
    }

    public static bool TryParse(PacketFrame frame, out LegacyKnownPersonPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode || frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        if (payload[OnlineOffset] > 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(ReservedOffset, sizeof(ushort))) != 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(FixedOneOffset, sizeof(uint))) != 1 ||
            !Enum.IsDefined(typeof(LegacyKnownPersonAction), payload[ActionOffset]))
        {
            return false;
        }

        packet = new LegacyKnownPersonPacket(
            (LegacyKnownPersonAction)payload[ActionOffset],
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UidOffset, sizeof(uint))),
            payload[OnlineOffset] == 1,
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(NobilityRankOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UserInfoOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(LookFaceOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(LevelOffset, sizeof(uint))),
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
        writer.WriteUInt16(ReservedOffset, 0);
        writer.WriteUInt32(NobilityRankOffset, NobilityRank);
        writer.WriteUInt32(UserInfoOffset, UserInfo);
        writer.WriteUInt32(LookFaceOffset, LookFace);
        writer.WriteUInt32(LevelOffset, Level);
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

public static class LegacyAssociationStartupPackets
{
    private const int FriendRelationType = 1;
    private const int EnemyRelationType = 2;

    public static bool TryCreateKnownPerson(
        SocialAssociationRecord record,
        bool targetOnline,
        out LegacyKnownPersonPacket packet)
    {
        ArgumentNullException.ThrowIfNull(record);
        packet = default!;

        var action = record.RelationType switch
        {
            FriendRelationType => LegacyKnownPersonAction.AddFriend,
            EnemyRelationType => LegacyKnownPersonAction.AddEnemy,
            _ => (LegacyKnownPersonAction?)null,
        };

        if (action is null)
        {
            return false;
        }

        packet = LegacyKnownPersonPacket.CreateStartup(
            LegacyAssociationMemberState.FromRecord(record),
            action.Value,
            targetOnline);
        return true;
    }

    public static IReadOnlyList<LegacyKnownPersonPacket> CreateKnownPersonStartup(
        IEnumerable<SocialAssociationRecord> records,
        uint ownerId,
        IReadOnlySet<uint>? onlineTargetIds = null)
    {
        ArgumentNullException.ThrowIfNull(records);

        var packets = new List<LegacyKnownPersonPacket>();
        foreach (var record in records)
        {
            if (record.OwnerId < 0 || (uint)record.OwnerId != ownerId)
            {
                continue;
            }

            if (!IsKnownPersonRelation(record.RelationType))
            {
                continue;
            }

            if (TryCreateKnownPerson(
                record,
                onlineTargetIds?.Contains(CheckedUInt32(record.TargetId, nameof(record.TargetId))) == true,
                out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static bool IsKnownPersonRelation(int relationType)
    {
        return relationType is FriendRelationType or EnemyRelationType;
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
