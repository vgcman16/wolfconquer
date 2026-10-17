using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyApprenticeInformationAction : uint
{
    Mentor = 1,
    Apprentice = 2,
}

public sealed record LegacyApprenticeAssociationMemberState(
    uint Uid,
    uint Timer,
    ulong ExpBalls,
    uint Stone,
    uint Blessing,
    uint Mesh,
    uint Level,
    uint Class,
    string Signature,
    string Name)
{
    private const int TimerValueIndex = 3;
    private const int ExpBallsValueIndex = 4;
    private const int StoneValueIndex = 5;
    private const int BlessingValueIndex = 6;
    private const int MeshValueIndex = 11;
    private const int LevelValueIndex = 12;

    public static LegacyApprenticeAssociationMemberState FromRecord(SocialAssociationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!TryFromRecord(record, out var member))
        {
            throw new ArgumentException("Association record does not contain the stored apprentice fields.", nameof(record));
        }

        return member;
    }

    public static bool TryFromRecord(
        SocialAssociationRecord record,
        out LegacyApprenticeAssociationMemberState member)
    {
        ArgumentNullException.ThrowIfNull(record);
        member = default!;

        if (record.Values is null ||
            record.Values.Count <= LevelValueIndex ||
            string.IsNullOrWhiteSpace(record.TargetName) ||
            record.Note is null)
        {
            return false;
        }

        if (!TryCheckedUInt32(record.TargetId, out var uid) ||
            !TryCheckedUInt32(record.Values[TimerValueIndex], out var timer) ||
            !TryCheckedUInt32(record.Values[ExpBallsValueIndex], out var expBalls) ||
            !TryCheckedUInt32(record.Values[StoneValueIndex], out var stone) ||
            !TryCheckedUInt32(record.Values[BlessingValueIndex], out var blessing) ||
            !TryCheckedUInt32(record.Values[MeshValueIndex], out var mesh) ||
            !TryCheckedUInt32(record.Values[LevelValueIndex], out var level) ||
            !TryCheckedUInt32(record.TailValue, out var @class))
        {
            return false;
        }

        member = new LegacyApprenticeAssociationMemberState(
            uid,
            timer,
            expBalls,
            stone,
            blessing,
            mesh,
            level,
            @class,
            record.Note,
            record.TargetName);
        return true;
    }

    private static bool TryCheckedUInt32(int value, out uint result)
    {
        if (value < 0)
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }
}

public sealed record LegacyApprenticeInformationPacket(
    LegacyApprenticeInformationAction Mode,
    uint MentorId,
    uint ApprenticeId,
    uint Mesh,
    uint SharedBattlePower,
    uint EnroleDate,
    uint Level,
    uint Class,
    ushort PkPoints,
    uint Online,
    uint Unknown2,
    ulong ApprenticeExperience,
    ushort ApprenticeBlessing,
    ushort ApprenticeComposing,
    string MentorName,
    string ApprenticeName,
    string ApprenticeSpouse,
    string PersonalSignature)
{
    private const uint Fixed999999 = 999_999;
    private const int ModeOffset = 0;
    private const int MentorIdOffset = ModeOffset + sizeof(uint);
    private const int ApprenticeIdOffset = MentorIdOffset + sizeof(uint);
    private const int MeshOffset = ApprenticeIdOffset + sizeof(uint);
    private const int SharedBattlePowerOffset = MeshOffset + sizeof(uint);
    private const int Fixed999999Offset = SharedBattlePowerOffset + sizeof(uint);
    private const int EnroleDateOffset = Fixed999999Offset + sizeof(uint);
    private const int LevelOffset = EnroleDateOffset + sizeof(uint);
    private const int ClassOffset = LevelOffset + sizeof(uint);
    private const int PkPointsOffset = ClassOffset + sizeof(uint);
    private const int ReservedOffset = PkPointsOffset + sizeof(ushort);
    private const int ReservedLength = 22;
    private const int OnlineOffset = ReservedOffset + ReservedLength;
    private const int Unknown2Offset = OnlineOffset + sizeof(uint);
    private const int ApprenticeExperienceOffset = Unknown2Offset + sizeof(uint);
    private const int ApprenticeBlessingOffset = ApprenticeExperienceOffset + sizeof(ulong);
    private const int ApprenticeComposingOffset = ApprenticeBlessingOffset + sizeof(ushort);
    private const int StringsOffset = ApprenticeComposingOffset + sizeof(ushort);
    private const int StringCount = 4;

    public const int MinimumPayloadLength = StringsOffset + 1 + (StringCount * sizeof(ushort));
    public const ushort Opcode = WorldPacketOpcodes.LegacyApprenticeInformation;

    public static LegacyApprenticeInformationPacket CreateOfflineApprenticeStartup(
        uint ownerId,
        LegacyApprenticeAssociationMemberState member)
    {
        ArgumentNullException.ThrowIfNull(member);

        return new LegacyApprenticeInformationPacket(
            LegacyApprenticeInformationAction.Apprentice,
            ownerId,
            member.Uid,
            member.Mesh,
            SharedBattlePower: 0,
            member.Timer,
            member.Level,
            member.Class,
            PkPoints: 0,
            Online: 0,
            Unknown2: 0,
            ApprenticeExperience: 0,
            ApprenticeBlessing: 0,
            ApprenticeComposing: 0,
            "NULL",
            member.Name,
            "NULL",
            member.Signature);
    }

    public static bool TryParse(PacketFrame frame, out LegacyApprenticeInformationPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode || frame.Payload.Length < MinimumPayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var mode = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ModeOffset, sizeof(uint)));
        if (!Enum.IsDefined(typeof(LegacyApprenticeInformationAction), mode) ||
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(Fixed999999Offset, sizeof(uint))) != Fixed999999 ||
            payload.AsSpan(ReservedOffset, ReservedLength).IndexOfAnyExcept((byte)0) >= 0)
        {
            return false;
        }

        if (!TryReadStrings(payload.AsSpan(StringsOffset), out var strings))
        {
            return false;
        }

        packet = new LegacyApprenticeInformationPacket(
            (LegacyApprenticeInformationAction)mode,
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(MentorIdOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ApprenticeIdOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(MeshOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(SharedBattlePowerOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(EnroleDateOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(LevelOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ClassOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(PkPointsOffset, sizeof(ushort))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(OnlineOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(Unknown2Offset, sizeof(uint))),
            BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(ApprenticeExperienceOffset, sizeof(ulong))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(ApprenticeBlessingOffset, sizeof(ushort))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(ApprenticeComposingOffset, sizeof(ushort))),
            strings[0],
            strings[1],
            strings[2],
            strings[3]);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payloadLength = checked(StringsOffset + GetStringSectionLength(MentorName, ApprenticeName, ApprenticeSpouse, PersonalSignature));
        var writer = new PacketWriter(Opcode, payloadLength);
        writer.WriteUInt32(ModeOffset, (uint)Mode);
        writer.WriteUInt32(MentorIdOffset, MentorId);
        writer.WriteUInt32(ApprenticeIdOffset, ApprenticeId);
        writer.WriteUInt32(MeshOffset, Mesh);
        writer.WriteUInt32(SharedBattlePowerOffset, SharedBattlePower);
        writer.WriteUInt32(Fixed999999Offset, Fixed999999);
        writer.WriteUInt32(EnroleDateOffset, EnroleDate);
        writer.WriteUInt32(LevelOffset, Level);
        writer.WriteUInt32(ClassOffset, Class);
        writer.WriteUInt16(PkPointsOffset, PkPoints);
        writer.WriteUInt32(OnlineOffset, Online);
        writer.WriteUInt32(Unknown2Offset, Unknown2);
        writer.WriteUInt64(ApprenticeExperienceOffset, ApprenticeExperience);
        writer.WriteUInt16(ApprenticeBlessingOffset, ApprenticeBlessing);
        writer.WriteUInt16(ApprenticeComposingOffset, ApprenticeComposing);
        WriteStrings(writer.Payload[StringsOffset..], MentorName, ApprenticeName, ApprenticeSpouse, PersonalSignature);
        return writer.ToArray();
    }

    private static int GetStringSectionLength(params string[] values)
    {
        var length = 1;
        foreach (var value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            var byteCount = Encoding.UTF8.GetByteCount(value);
            if (byteCount > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(values), "Legacy apprentice string exceeds UInt16 byte length.");
            }

            length = checked(length + sizeof(ushort) + byteCount);
        }

        return length;
    }

    private static void WriteStrings(Span<byte> destination, params string[] values)
    {
        destination[0] = checked((byte)values.Length);
        var offset = 1;
        foreach (var value in values)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), checked((ushort)bytes.Length));
            offset += sizeof(ushort);
            bytes.CopyTo(destination[offset..]);
            offset += bytes.Length;
        }
    }

    private static bool TryReadStrings(ReadOnlySpan<byte> payload, out string[] values)
    {
        values = [];
        if (payload.Length == 0 || payload[0] != StringCount)
        {
            return false;
        }

        var parsed = new string[StringCount];
        var offset = 1;
        for (var index = 0; index < parsed.Length; index++)
        {
            if (payload.Length - offset < sizeof(ushort))
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (payload.Length - offset < length)
            {
                return false;
            }

            parsed[index] = Encoding.UTF8.GetString(payload.Slice(offset, length));
            offset += length;
        }

        if (offset != payload.Length)
        {
            return false;
        }

        values = parsed;
        return true;
    }
}

public static class LegacyApprenticeInformationStartupPackets
{
    private const int ApprenticeRelationType = 5;

    public static bool TryCreateOfflineApprentice(
        SocialAssociationRecord record,
        out LegacyApprenticeInformationPacket packet,
        bool emitClientCaughtStartupFrame = false)
    {
        ArgumentNullException.ThrowIfNull(record);
        packet = default!;

        if (record.RelationType != ApprenticeRelationType)
        {
            return false;
        }

        // The decompiled source-shaped offline 2200 frame is kept for capture parity,
        // but this staged client catches it during login.
        if (!emitClientCaughtStartupFrame)
        {
            return false;
        }

        if (!LegacyApprenticeAssociationMemberState.TryFromRecord(record, out var member) ||
            !TryCheckedUInt32(record.OwnerId, out var ownerId))
        {
            return false;
        }

        packet = LegacyApprenticeInformationPacket.CreateOfflineApprenticeStartup(
            ownerId,
            member);
        return true;
    }

    public static IReadOnlyList<LegacyApprenticeInformationPacket> CreateOfflineApprenticeStartup(
        IEnumerable<SocialAssociationRecord> records,
        uint ownerId,
        IReadOnlySet<uint>? onlineTargetIds = null,
        bool emitClientCaughtStartupFrame = false)
    {
        ArgumentNullException.ThrowIfNull(records);

        var packets = new List<LegacyApprenticeInformationPacket>();
        foreach (var record in records)
        {
            if (record.OwnerId < 0 || (uint)record.OwnerId != ownerId)
            {
                continue;
            }

            if (record.RelationType != ApprenticeRelationType)
            {
                continue;
            }

            if (!TryCreateOfflineApprentice(record, out var packet, emitClientCaughtStartupFrame))
            {
                continue;
            }

            if (onlineTargetIds?.Contains(packet.ApprenticeId) == true)
            {
                continue;
            }

            packets.Add(packet);
        }

        return packets;
    }

    private static bool TryCheckedUInt32(int value, out uint result)
    {
        if (value < 0)
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }
}
