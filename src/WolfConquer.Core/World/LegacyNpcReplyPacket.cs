using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyNpcReplyInteractType : byte
{
    Dialog = 1,
    Option = 2,
    Input = 3,
    Avatar = 4,
    MessageBox = 6,
    StaticGui = 112,
    Finish = 100,
    Page = 113,
}

public enum LegacyStaticGuiMode : byte
{
    Header = 0,
    Body = 1,
    Footer = 2,
}

public readonly record struct LegacyNpcReplyPacket(
    uint Unknown,
    ushort InputMaxLength,
    byte OptionId,
    LegacyNpcReplyInteractType InteractType,
    string Text)
{
    private const int UnknownOffset = 0;
    private const int InputMaxLengthOffset = UnknownOffset + sizeof(uint);
    private const int OptionIdOffset = InputMaxLengthOffset + sizeof(ushort);
    private const int InteractTypeOffset = OptionIdOffset + sizeof(byte);
    private const int StringCountOffset = InteractTypeOffset + sizeof(byte);
    private const int StringLengthOffset = StringCountOffset + sizeof(byte);
    private const int TextOffset = StringLengthOffset + sizeof(ushort);

    public const ushort Opcode = WorldPacketOpcodes.LegacyNpcReply;
    public const byte ClosedOptionId = byte.MaxValue;

    public static LegacyNpcReplyPacket ForMessageBox(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LegacyNpcReplyPacket(0, ushort.MaxValue, 0, LegacyNpcReplyInteractType.MessageBox, text);
    }

    public static LegacyNpcReplyPacket ForDialog(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LegacyNpcReplyPacket(0, 0, 0, LegacyNpcReplyInteractType.Dialog, text);
    }

    public static LegacyNpcReplyPacket ForOption(string text, byte optionId)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LegacyNpcReplyPacket(0, 0, optionId, LegacyNpcReplyInteractType.Option, text);
    }

    public static LegacyNpcReplyPacket ForClosedOption(string text)
    {
        return ForOption(text, ClosedOptionId);
    }

    public static LegacyNpcReplyPacket ForInput(string text, byte optionId, ushort maxLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LegacyNpcReplyPacket(0, maxLength, optionId, LegacyNpcReplyInteractType.Input, text);
    }

    public static LegacyNpcReplyPacket ForAvatar(ushort avatarId)
    {
        return new LegacyNpcReplyPacket(0, avatarId, 0, LegacyNpcReplyInteractType.Avatar, "");
    }

    public static LegacyNpcReplyPacket ForFinish()
    {
        return new LegacyNpcReplyPacket(0, 0, 0, LegacyNpcReplyInteractType.Finish, "");
    }

    public static LegacyNpcReplyPacket ForStaticGui(LegacyStaticGuiMode mode, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LegacyNpcReplyPacket(0, 0, (byte)mode, LegacyNpcReplyInteractType.StaticGui, text);
    }

    public static LegacyNpcReplyPacket ForPage(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new LegacyNpcReplyPacket(0, 0, ClosedOptionId, LegacyNpcReplyInteractType.Page, text);
    }

    public static bool TryParse(PacketFrame frame, out LegacyNpcReplyPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < TextOffset)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        if (payload[StringCountOffset] != 1)
        {
            return false;
        }

        if (!Enum.IsDefined(typeof(LegacyNpcReplyInteractType), payload[InteractTypeOffset]))
        {
            return false;
        }

        if (!TryReadString(payload, out var text))
        {
            return false;
        }

        packet = new LegacyNpcReplyPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(UnknownOffset, sizeof(uint))),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(InputMaxLengthOffset, sizeof(ushort))),
            payload[OptionIdOffset],
            (LegacyNpcReplyInteractType)payload[InteractTypeOffset],
            text);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var textBytes = Encoding.UTF8.GetBytes(Text);
        if (textBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Text), "Legacy NPC reply text cannot exceed 65,535 bytes.");
        }

        var writer = new PacketWriter(Opcode, TextOffset + textBytes.Length);
        writer.WriteUInt32(UnknownOffset, Unknown);
        writer.WriteUInt16(InputMaxLengthOffset, InputMaxLength);
        writer.Payload[OptionIdOffset] = OptionId;
        writer.Payload[InteractTypeOffset] = (byte)InteractType;
        writer.Payload[StringCountOffset] = 1;
        writer.WriteUInt16(StringLengthOffset, (ushort)textBytes.Length);
        textBytes.CopyTo(writer.Payload[TextOffset..]);
        return writer.ToArray();
    }

    private static bool TryReadString(ReadOnlySpan<byte> payload, out string text)
    {
        text = "";
        var length = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(StringLengthOffset, sizeof(ushort)));
        if (payload.Length != TextOffset + length)
        {
            return false;
        }

        text = Encoding.UTF8.GetString(payload[TextOffset..]);
        return true;
    }
}
