using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

public enum LegacyMailListType : int
{
    FullPage = 0,
    ClaimAllUpdate = 1,
    NewMail = 2,
}

public sealed record LegacyMailListEntry(
    ulong Id,
    ulong Money,
    ulong EMoney,
    ulong ExpirationDate,
    ulong ItemId,
    ulong EMoneyRecordType,
    ulong Flag,
    string Title,
    string SenderName);

public sealed record LegacyMailListPacket(
    LegacyMailListType Type,
    int Count,
    IReadOnlyList<LegacyMailListEntry> Mails)
{
    private const int TypeField = 1;
    private const int CountField = 2;
    private const int MailField = 3;

    public const ushort Opcode = WorldPacketOpcodes.LegacyMailList;

    public static LegacyMailListEntry FromMailboxEntry(LegacyMailboxEntry entry, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var remaining = entry.ExpiresAt - now;
        var expirationSeconds = remaining <= TimeSpan.Zero
            ? 0UL
            : (ulong)((uint)remaining.TotalMinutes * 60U);

        return new LegacyMailListEntry(
            entry.Id,
            entry.Money,
            entry.EMoney,
            expirationSeconds,
            entry.ItemId,
            entry.EmoneyRecordType,
            entry.Flag,
            entry.Title,
            entry.SenderName);
    }

    public static LegacyMailListPacket NewMail(LegacyMailListEntry mail)
    {
        ArgumentNullException.ThrowIfNull(mail);
        return new LegacyMailListPacket(LegacyMailListType.NewMail, Count: 0, [mail]);
    }

    public static bool TryParse(PacketFrame frame, out LegacyMailListPacket packet) =>
        TryParse(frame, ConquerProtocolProfile.RootD11_7899, out packet);

    public static bool TryParse(PacketFrame frame, ConquerProtocolProfile protocolProfile,
        out LegacyMailListPacket packet)
    {
        packet = default!;
        if (protocolProfile is not (ConquerProtocolProfile.RootD11_7899 or ConquerProtocolProfile.VisibleDx8D13) ||
            frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var type = 0;
        var count = 0;
        var hasType = false;
        var hasCount = false;
        var mails = new List<LegacyMailListEntry>();

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadVarint(out var typeValue) ||
                        typeValue > int.MaxValue ||
                        !Enum.IsDefined(typeof(LegacyMailListType), (int)typeValue))
                    {
                        return false;
                    }

                    type = (int)typeValue;
                    hasType = true;
                    break;
                case CountField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadVarint(out var countValue) ||
                        countValue > int.MaxValue)
                    {
                        return false;
                    }

                    count = (int)countValue;
                    hasCount = true;
                    break;
                case MailField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var mailPayload) ||
                        !TryParseMail(mailPayload, protocolProfile, out var mail))
                    {
                        return false;
                    }

                    mails.Add(mail);
                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (!hasType || !hasCount)
        {
            return false;
        }

        packet = new LegacyMailListPacket((LegacyMailListType)type, count, mails);
        return true;
    }

    public byte[] ToFrameBytes() => ToFrameBytes(ConquerProtocolProfile.RootD11_7899);

    public byte[] ToFrameBytes(ConquerProtocolProfile protocolProfile)
    {
        var (titleField, senderField) = TextFields(protocolProfile);
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, checked((ulong)(int)Type));
        LegacyProtoWire.WriteVarintField(payload, CountField, checked((ulong)Count));

        foreach (var mail in Mails)
        {
            LegacyProtoWire.WriteBytesField(payload, MailField, CreateMailPayload(mail, titleField, senderField));
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static (int Title, int Sender) TextFields(ConquerProtocolProfile protocolProfile) => protocolProfile switch
    {
        ConquerProtocolProfile.RootD11_7899 => (8, 9),
        ConquerProtocolProfile.VisibleDx8D13 => (9, 10),
        _ => throw new ArgumentOutOfRangeException(nameof(protocolProfile)),
    };

    private static byte[] CreateMailPayload(LegacyMailListEntry mail, int titleField, int senderField)
    {
        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, 1, mail.Id);
        LegacyProtoWire.WriteVarintField(payload, 2, mail.Money);
        LegacyProtoWire.WriteVarintField(payload, 3, mail.EMoney);
        LegacyProtoWire.WriteVarintField(payload, 4, mail.ExpirationDate);
        LegacyProtoWire.WriteVarintField(payload, 5, mail.ItemId);
        LegacyProtoWire.WriteVarintField(payload, 6, mail.EMoneyRecordType);
        LegacyProtoWire.WriteVarintField(payload, 7, mail.Flag);
        // D13 numeric field8 is optional and opaque; no value is guessed or emitted.
        LegacyProtoWire.WriteStringField(payload, titleField, mail.Title);
        LegacyProtoWire.WriteStringField(payload, senderField, mail.SenderName);
        return [.. payload];
    }

    private static bool TryParseMail(ReadOnlySpan<byte> payload, ConquerProtocolProfile protocolProfile,
        out LegacyMailListEntry mail)
    {
        mail = default!;
        var (titleField, senderField) = TextFields(protocolProfile);
        var reader = new LegacyProtoWire.Reader(payload);
        ulong id = 0;
        ulong money = 0;
        ulong eMoney = 0;
        ulong expirationDate = 0;
        ulong itemId = 0;
        ulong eMoneyRecordType = 0;
        ulong flag = 0;
        var title = string.Empty;
        var senderName = string.Empty;
        var seen = 0;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            if (fieldNumber == titleField)
            {
                if (wireType != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadString(out title))
                    return false;
                seen |= 1 << 7;
                continue;
            }
            if (fieldNumber == senderField)
            {
                if (wireType != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadString(out senderName))
                    return false;
                seen |= 1 << 8;
                continue;
            }

            switch (fieldNumber)
            {
                case 1:
                    if (!TryReadMailVarint(ref reader, wireType, out id))
                    {
                        return false;
                    }

                    seen |= 1 << 0;
                    break;
                case 2:
                    if (!TryReadMailVarint(ref reader, wireType, out money))
                    {
                        return false;
                    }

                    seen |= 1 << 1;
                    break;
                case 3:
                    if (!TryReadMailVarint(ref reader, wireType, out eMoney))
                    {
                        return false;
                    }

                    seen |= 1 << 2;
                    break;
                case 4:
                    if (!TryReadMailVarint(ref reader, wireType, out expirationDate))
                    {
                        return false;
                    }

                    seen |= 1 << 3;
                    break;
                case 5:
                    if (!TryReadMailVarint(ref reader, wireType, out itemId))
                    {
                        return false;
                    }

                    seen |= 1 << 4;
                    break;
                case 6:
                    if (!TryReadMailVarint(ref reader, wireType, out eMoneyRecordType))
                    {
                        return false;
                    }

                    seen |= 1 << 5;
                    break;
                case 7:
                    if (!TryReadMailVarint(ref reader, wireType, out flag))
                    {
                        return false;
                    }

                    seen |= 1 << 6;
                    break;
                case 8 when protocolProfile == ConquerProtocolProfile.VisibleDx8D13:
                    // Consume the known optional numeric wire field without assigning semantics.
                    if (!TryReadMailVarint(ref reader, wireType, out _)) return false;
                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (seen != 0x1FF)
        {
            return false;
        }

        mail = new LegacyMailListEntry(
            id,
            money,
            eMoney,
            expirationDate,
            itemId,
            eMoneyRecordType,
            flag,
            title,
            senderName);
        return true;
    }

    private static bool TryReadMailVarint(ref LegacyProtoWire.Reader reader, int wireType, out ulong value)
    {
        value = 0;
        return wireType == LegacyProtoWire.VarintWireType && reader.TryReadVarint(out value);
    }
}
