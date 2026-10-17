using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyMsgMessagePacket(
    uint Color,
    uint ChatType,
    uint MessageUid1,
    uint MessageUid2,
    uint ServerId,
    uint Mesh,
    uint UnionType,
    uint Count,
    uint New1,
    uint New2,
    IReadOnlyList<string> Content)
{
    private const int ColorField = 1;
    private const int ChatTypeField = 2;
    private const int MessageUid1Field = 3;
    private const int MessageUid2Field = 4;
    private const int ServerIdField = 5;
    private const int MeshField = 6;
    private const int UnionTypeField = 7;
    private const int CountField = 8;
    private const int New1Field = 9;
    private const int New2Field = 10;
    private const int ContentField = 14;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyMsgMessage;
    public const uint WhiteColor = 16_777_215;
    public const uint RedColor = 16_711_680;
    public const uint YellowColor = 16_776_960;
    public const uint TalkChatType = 2_000;
    public const uint WhisperChatType = 2_001;
    public const uint RuneRankChatType = 2_005;
    public const uint TopLeftSystemChatType = RuneRankChatType;
    public const uint CenterChatType = 2_011;
    public const uint QualifierChatType = 2_022;
    public const uint WorldChatType = 2_021;
    public const uint BroadcastChatType = 2_500;
    public const uint DialogChatType = 2_101;
    public const uint WebsiteChatType = 2_105;

    public static LegacyMsgMessagePacket SystemTalk(string message)
    {
        return SystemMessage(message, TalkChatType);
    }

    public static LegacyMsgMessagePacket SystemWhiteTalk(string message)
    {
        return SystemMessage(message, TalkChatType, WhiteColor);
    }

    public static LegacyMsgMessagePacket SystemBroadcast(string message)
    {
        return SystemMessage(message, BroadcastChatType);
    }

    public static LegacyMsgMessagePacket SystemRuneRank(string message)
    {
        return SystemMessage(message, RuneRankChatType);
    }

    public static LegacyMsgMessagePacket SystemTopLeft(string message)
    {
        return SystemMessage(message, TopLeftSystemChatType);
    }

    public static LegacyMsgMessagePacket SystemQualifier(string message)
    {
        return SystemMessage(message, QualifierChatType);
    }

    public static LegacyMsgMessagePacket SystemWebsite(string url)
    {
        return SystemMessage(url, WebsiteChatType, WhiteColor);
    }

    public static LegacyMsgMessagePacket PlayerBroadcast(string senderName, string message, uint unionType = 0)
    {
        ArgumentNullException.ThrowIfNull(senderName);
        ArgumentNullException.ThrowIfNull(message);

        return new LegacyMsgMessagePacket(
            RedColor,
            BroadcastChatType,
            MessageUid1: 0,
            MessageUid2: 0,
            ServerId: 0,
            Mesh: 0,
            UnionType: unionType,
            Count: 1,
            New1: 0,
            New2: 0,
            [senderName, "ALLUSERS", string.Empty, message, string.Empty, string.Empty, string.Empty]);
    }

    public static LegacyMsgMessagePacket SystemCenter(string message)
    {
        return SystemMessage(message, CenterChatType, YellowColor);
    }

    public static LegacyMsgMessagePacket SystemRedCenter(string message)
    {
        return SystemMessage(message, CenterChatType, RedColor);
    }

    public static LegacyMsgMessagePacket BoxDialog(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new LegacyMsgMessagePacket(
            RedColor,
            DialogChatType,
            MessageUid1: 0,
            MessageUid2: 0,
            ServerId: 0,
            Mesh: 0,
            UnionType: 0,
            Count: 1,
            New1: 0,
            New2: 0,
            ["SYSTEM", "ALLUSERS", string.Empty, message, string.Empty, string.Empty, string.Empty]);
    }

    public static LegacyMsgMessagePacket TopInHoursInvitation(int minute)
    {
        if (minute is < 0 or > 59)
        {
            throw new ArgumentOutOfRangeException(nameof(minute), minute, "Legacy TopInHours minute must be between 0 and 59.");
        }

        if (!LegacyTopInHoursInvitationScheduler.TryGetInvitationForMinute(minute, out var invitation))
        {
            throw new ArgumentException($"No legacy TopInHours xx-minute invitation exists for minute {minute}.", nameof(minute));
        }

        return LegacyTopInHoursInvitationScheduler.CreateMessage(invitation);
    }

    private static LegacyMsgMessagePacket SystemMessage(
        string message,
        uint chatType,
        uint color = RedColor)
    {
        ArgumentNullException.ThrowIfNull(message);

        return new LegacyMsgMessagePacket(
            color,
            chatType,
            MessageUid1: 0,
            MessageUid2: 0,
            ServerId: 0,
            Mesh: 0,
            UnionType: 0,
            Count: 1,
            New1: 0,
            New2: 0,
            ["SYSTEM", "ALL", string.Empty, message, string.Empty, string.Empty, string.Empty]);
    }

    public static LegacyMsgMessagePacket AnswerOk()
    {
        return new LegacyMsgMessagePacket(
            RedColor,
            DialogChatType,
            MessageUid1: 0,
            MessageUid2: 0,
            ServerId: 0,
            Mesh: 0,
            UnionType: 0,
            Count: 1,
            New1: 0,
            New2: 0,
            ["SYSTEM", "ALLUSERS", string.Empty, "ANSWER_OK", string.Empty, string.Empty, string.Empty]);
    }

    public static LegacyMsgMessagePacket NewRole()
    {
        return new LegacyMsgMessagePacket(
            RedColor,
            DialogChatType,
            MessageUid1: 0,
            MessageUid2: 0,
            ServerId: 0,
            Mesh: 0,
            UnionType: 0,
            Count: 1,
            New1: 0,
            New2: 0,
            ["SYSTEM", "ALLUSERS", string.Empty, "NEW_ROLE", string.Empty, string.Empty, string.Empty]);
    }

    public static bool TryParse(PacketFrame frame, out LegacyMsgMessagePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var fieldsSeen = 0;
        uint color = 0;
        uint chatType = 0;
        uint messageUid1 = 0;
        uint messageUid2 = 0;
        uint serverId = 0;
        uint mesh = 0;
        uint unionType = 0;
        uint count = 0;
        uint new1 = 0;
        uint new2 = 0;
        var content = new List<string>();

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (fieldNumber == ContentField)
            {
                if (wireType != LengthDelimitedWireType ||
                    !reader.TryReadString(out var value))
                {
                    return false;
                }

                content.Add(value);
                continue;
            }

            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var numericValue) ||
                numericValue > uint.MaxValue)
            {
                return false;
            }

            var numeric = (uint)numericValue;
            fieldsSeen |= 1 << fieldNumber;
            switch (fieldNumber)
            {
                case ColorField:
                    color = numeric;
                    break;
                case ChatTypeField:
                    chatType = numeric;
                    break;
                case MessageUid1Field:
                    messageUid1 = numeric;
                    break;
                case MessageUid2Field:
                    messageUid2 = numeric;
                    break;
                case ServerIdField:
                    serverId = numeric;
                    break;
                case MeshField:
                    mesh = numeric;
                    break;
                case UnionTypeField:
                    unionType = numeric;
                    break;
                case CountField:
                    count = numeric;
                    break;
                case New1Field:
                    new1 = numeric;
                    break;
                case New2Field:
                    new2 = numeric;
                    break;
                default:
                    return false;
            }
        }

        const int requiredFields =
            (1 << ColorField) |
            (1 << ChatTypeField) |
            (1 << MessageUid1Field) |
            (1 << MessageUid2Field) |
            (1 << ServerIdField) |
            (1 << MeshField) |
            (1 << UnionTypeField) |
            (1 << CountField) |
            (1 << New1Field) |
            (1 << New2Field);

        if ((fieldsSeen & requiredFields) != requiredFields ||
            content.Count != 7)
        {
            return false;
        }

        packet = new LegacyMsgMessagePacket(
            color,
            chatType,
            messageUid1,
            messageUid2,
            serverId,
            mesh,
            unionType,
            count,
            new1,
            new2,
            content);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, ColorField, Color);
        WriteVarintField(payload, ChatTypeField, ChatType);
        WriteVarintField(payload, MessageUid1Field, MessageUid1);
        WriteVarintField(payload, MessageUid2Field, MessageUid2);
        WriteVarintField(payload, ServerIdField, ServerId);
        WriteVarintField(payload, MeshField, Mesh);
        WriteVarintField(payload, UnionTypeField, UnionType);
        WriteVarintField(payload, CountField, Count);
        WriteVarintField(payload, New1Field, New1);
        WriteVarintField(payload, New2Field, New2);
        foreach (var value in Content)
        {
            WriteString(payload, ContentField, value);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteString(List<byte> payload, int fieldNumber, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)bytes.Length);
        payload.AddRange(bytes);
    }

    private static void WriteVarint(List<byte> payload, ulong value)
    {
        do
        {
            var next = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0)
            {
                next |= 0x80;
            }

            payload.Add(next);
        }
        while (value != 0);
    }

    private ref struct ProtoReader(ReadOnlySpan<byte> payload)
    {
        private readonly ReadOnlySpan<byte> _payload = payload;
        private int _offset;

        public readonly bool End => _offset >= _payload.Length;

        public bool TryReadVarint(out ulong value)
        {
            value = 0;

            for (var shift = 0; shift < 64; shift += 7)
            {
                if (_offset >= _payload.Length)
                {
                    return false;
                }

                var current = _payload[_offset++];
                value |= (ulong)(current & 0x7F) << shift;
                if ((current & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public bool TryReadBytes(out ReadOnlySpan<byte> value)
        {
            value = default;
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset + (int)length > _payload.Length)
            {
                return false;
            }

            value = _payload.Slice(_offset, (int)length);
            _offset += (int)length;
            return true;
        }

        public bool TryReadString(out string value)
        {
            value = string.Empty;
            if (!TryReadBytes(out var bytes))
            {
                return false;
            }

            value = Encoding.UTF8.GetString(bytes);
            return true;
        }
    }
}
