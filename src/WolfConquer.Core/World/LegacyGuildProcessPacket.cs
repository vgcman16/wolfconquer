using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyGuildProcessAction : uint
{
    Accept = 1,
    Invite = 2,
    Quit = 3,
    InfoName = 6,
    Allied = 7,
    RemoveAlly = 8,
    Enemy = 9,
    RemoveEnemy = 10,
    SilverDonate = 11,
    Show = 12,
    Disband = 19,
    CpDonate = 20,
    RequestAllied = 23,
    Requirements = 24,
    Bulletin = 27,
    Promote = 28,
    ConfirmPromote = 29,
    Discharge = 30,
    Resign = 32,
    RequestPromote = 37,
    UpdatePromote = 38,
    Create = 51,
    Appoint = 53,
    Retire = 54,
    Join = 55,
}

public sealed record LegacyGuildProcessPacket(
    LegacyGuildProcessAction Action,
    uint Uid,
    int Argument0,
    int Argument1,
    int Argument2,
    int Argument3,
    IReadOnlyList<string> Strings)
{
    private const int ActionOffset = 0;
    private const int UidOffset = ActionOffset + sizeof(uint);
    private const int Argument0Offset = UidOffset + sizeof(uint);
    private const int Argument1Offset = Argument0Offset + sizeof(int);
    private const int Argument2Offset = Argument1Offset + sizeof(int);
    private const int Argument3Offset = Argument2Offset + sizeof(int);
    private const int StringCountOffset = Argument3Offset + sizeof(int);
    private const int StringsOffset = StringCountOffset + sizeof(byte);
    private const int TrailerLength = 3;

    public const ushort Opcode = WorldPacketOpcodes.LegacyGuildProcess;
    public const int MinimumPayloadLength = StringsOffset + TrailerLength;

    public static LegacyGuildProcessPacket Create(
        LegacyGuildProcessAction action,
        uint uid,
        IReadOnlyList<int>? args = null,
        IReadOnlyList<string>? strings = null)
    {
        var actualArgs = args ?? [];
        if (actualArgs.Count > 4)
        {
            throw new ArgumentOutOfRangeException(nameof(args), "Legacy guild process packets support exactly four argument slots.");
        }

        return new LegacyGuildProcessPacket(
            action,
            uid,
            actualArgs.Count > 0 ? actualArgs[0] : 0,
            actualArgs.Count > 1 ? actualArgs[1] : 0,
            actualArgs.Count > 2 ? actualArgs[2] : 0,
            actualArgs.Count > 3 ? actualArgs[3] : 0,
            strings ?? []);
    }

    public static bool TryParse(PacketFrame frame, out LegacyGuildProcessPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < MinimumPayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var action = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ActionOffset, sizeof(uint)));
        if (!TryReadStringList(payload.AsSpan(StringCountOffset, payload.Length - StringCountOffset - TrailerLength), out var strings))
        {
            return false;
        }

        packet = new LegacyGuildProcessPacket(
            (LegacyGuildProcessAction)action,
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UidOffset, sizeof(uint))),
            BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(Argument0Offset, sizeof(int))),
            BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(Argument1Offset, sizeof(int))),
            BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(Argument2Offset, sizeof(int))),
            BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(Argument3Offset, sizeof(int))),
            strings);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Strings);
        if (!Enum.IsDefined(typeof(LegacyGuildProcessAction), Action))
        {
            throw new ArgumentOutOfRangeException(nameof(Action), "Legacy guild process action is not defined.");
        }

        if (Strings.Count > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Strings), "Legacy guild process packets cannot contain more than 255 strings.");
        }

        var encoded = new byte[Strings.Count][];
        var payloadLength = MinimumPayloadLength;
        for (var index = 0; index < Strings.Count; index++)
        {
            var value = Strings[index] ?? string.Empty;
            encoded[index] = Encoding.UTF8.GetBytes(value);
            if (encoded[index].Length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(Strings), "Legacy guild process string entries cannot exceed 65,535 bytes.");
            }

            payloadLength += sizeof(ushort) + encoded[index].Length;
        }

        var writer = new PacketWriter(Opcode, payloadLength);
        writer.WriteUInt32(ActionOffset, (uint)Action);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteInt32(Argument0Offset, Argument0);
        writer.WriteInt32(Argument1Offset, Argument1);
        writer.WriteInt32(Argument2Offset, Argument2);
        writer.WriteInt32(Argument3Offset, Argument3);
        writer.Payload[StringCountOffset] = (byte)encoded.Length;

        var offset = StringsOffset;
        foreach (var entry in encoded)
        {
            writer.WriteUInt16(offset, (ushort)entry.Length);
            offset += sizeof(ushort);
            entry.CopyTo(writer.Payload[offset..]);
            offset += entry.Length;
        }

        return writer.ToArray();
    }

    private static bool TryReadStringList(ReadOnlySpan<byte> payload, out string[] strings)
    {
        strings = [];
        if (payload.Length < sizeof(byte))
        {
            return false;
        }

        var count = payload[0];
        var offset = sizeof(byte);
        var parsed = new string[count];
        for (var index = 0; index < count; index++)
        {
            if (offset > payload.Length - sizeof(ushort))
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (offset > payload.Length - length)
            {
                return false;
            }

            parsed[index] = ReadLegacyString(payload.Slice(offset, length));
            offset += length;
        }

        if (offset != payload.Length)
        {
            return false;
        }

        strings = parsed;
        return true;
    }

    private static string ReadLegacyString(ReadOnlySpan<byte> bytes)
    {
        var length = bytes.IndexOf((byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return Encoding.UTF8.GetString(bytes[..length]);
    }
}

public sealed record LegacyGuildInformationPacket(
    uint GuildId,
    long SilverFund,
    uint ConquerPointFund,
    int MemberCount,
    uint MyRank,
    string LeaderName,
    ushort Level,
    uint CreateTime,
    uint LeaderId,
    ulong Material,
    bool RecruitmentOff,
    uint RecruitmentBattlePower,
    bool RecruitmentOn,
    ulong Prestige,
    uint RecruitmentFlag)
{
    private const int GuildIdOffset = 0;
    private const int SilverFundOffset = GuildIdOffset + sizeof(uint);
    private const int ConquerPointFundOffset = SilverFundOffset + sizeof(long);
    private const int MemberCountOffset = ConquerPointFundOffset + sizeof(uint);
    private const int MyRankOffset = MemberCountOffset + sizeof(int);
    private const int LeaderNameOffset = MyRankOffset + sizeof(uint);
    private const int LeaderNameLength = 32;
    private const int LevelOffset = LeaderNameOffset + LeaderNameLength;
    private const int Reserved58Offset = LevelOffset + sizeof(ushort);
    private const int CreateTimeOffset = Reserved58Offset + sizeof(byte);
    private const int LeaderIdOffset = CreateTimeOffset + sizeof(uint);
    private const int MaterialOffset = LeaderIdOffset + sizeof(uint);
    private const int RecruitmentOffOffset = MaterialOffset + sizeof(ulong);
    private const int RecruitmentBattlePowerOffset = RecruitmentOffOffset + sizeof(byte);
    private const int RecruitmentOnOffset = RecruitmentBattlePowerOffset + sizeof(uint);
    private const int PrestigeOffset = RecruitmentOnOffset + sizeof(byte);
    private const int RecruitmentFlagOffset = PrestigeOffset + sizeof(ulong);

    public const ushort Opcode = 2172;
    public const int PayloadLength = RecruitmentFlagOffset + sizeof(uint);

    public static bool TryParse(PacketFrame frame, out LegacyGuildInformationPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new LegacyGuildInformationPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(GuildIdOffset, sizeof(uint))),
            BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(SilverFundOffset, sizeof(long))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ConquerPointFundOffset, sizeof(uint))),
            BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(MemberCountOffset, sizeof(int))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(MyRankOffset, sizeof(uint))),
            ReadLegacyString(payload.AsSpan(LeaderNameOffset, LeaderNameLength)),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(LevelOffset, sizeof(ushort))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(CreateTimeOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(LeaderIdOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(MaterialOffset, sizeof(ulong))),
            payload[RecruitmentOffOffset] != 0,
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(RecruitmentBattlePowerOffset, sizeof(uint))),
            payload[RecruitmentOnOffset] != 0,
            BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(PrestigeOffset, sizeof(ulong))),
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(RecruitmentFlagOffset, sizeof(uint))));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(GuildIdOffset, GuildId);
        writer.WriteInt64(SilverFundOffset, SilverFund);
        writer.WriteUInt32(ConquerPointFundOffset, ConquerPointFund);
        writer.WriteInt32(MemberCountOffset, MemberCount);
        writer.WriteUInt32(MyRankOffset, MyRank);
        WriteFixedLegacyString(writer.Payload.Slice(LeaderNameOffset, LeaderNameLength), LeaderName);
        writer.WriteUInt16(LevelOffset, Level);
        writer.Payload[Reserved58Offset] = 0;
        writer.WriteUInt32(CreateTimeOffset, CreateTime);
        writer.WriteUInt32(LeaderIdOffset, LeaderId);
        writer.WriteUInt64(MaterialOffset, Material);
        writer.Payload[RecruitmentOffOffset] = RecruitmentOff ? (byte)1 : (byte)0;
        writer.WriteUInt32(RecruitmentBattlePowerOffset, RecruitmentBattlePower);
        writer.Payload[RecruitmentOnOffset] = RecruitmentOn ? (byte)1 : (byte)0;
        writer.WriteUInt64(PrestigeOffset, Prestige);
        writer.WriteUInt32(RecruitmentFlagOffset, RecruitmentFlag);
        return writer.ToArray();
    }

    private static void WriteFixedLegacyString(Span<byte> destination, string? value)
    {
        destination.Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var encoded = Encoding.UTF8.GetBytes(value);
        encoded.AsSpan(0, Math.Min(encoded.Length, destination.Length)).CopyTo(destination);
    }

    private static string ReadLegacyString(ReadOnlySpan<byte> bytes)
    {
        var length = bytes.IndexOf((byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return Encoding.UTF8.GetString(bytes[..length]);
    }
}
