using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.Accounts;

public static class AccountLoginResponsePacket
{
    public const int PayloadLength = sizeof(byte) + sizeof(long);
    private static readonly Encoding WireEncoding = Encoding.UTF8;

    public static byte[] Build(AccountLoginWireStatus status, long accountId)
    {
        var writer = new PacketWriter(AccountPacketOpcodes.InternalLoginResponse, PayloadLength);
        writer.Payload[0] = (byte)status;
        writer.WriteInt64(1, accountId);
        return writer.ToArray();
    }

    public static byte[] Build(
        AccountLoginWireStatus status,
        long accountId,
        string ticket,
        string worldEndpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticket);
        ArgumentException.ThrowIfNullOrWhiteSpace(worldEndpoint);

        var ticketBytes = GetLengthPrefixedField(ticket, nameof(ticket));
        var endpointBytes = GetLengthPrefixedField(worldEndpoint, nameof(worldEndpoint));
        var writer = new PacketWriter(
            AccountPacketOpcodes.InternalLoginResponse,
            PayloadLength + sizeof(ushort) + ticketBytes.Length + sizeof(ushort) + endpointBytes.Length);

        writer.Payload[0] = (byte)status;
        writer.WriteInt64(1, accountId);
        var offset = PayloadLength;
        writer.WriteUInt16(offset, (ushort)ticketBytes.Length);
        offset += sizeof(ushort);
        ticketBytes.CopyTo(writer.Payload[offset..]);
        offset += ticketBytes.Length;
        writer.WriteUInt16(offset, (ushort)endpointBytes.Length);
        offset += sizeof(ushort);
        endpointBytes.CopyTo(writer.Payload[offset..]);
        return writer.ToArray();
    }

    public static AccountLoginWireStatus ReadStatus(PacketReader reader)
    {
        return (AccountLoginWireStatus)reader.Payload[0];
    }

    public static long ReadAccountId(PacketReader reader)
    {
        return reader.ReadInt64(1);
    }

    public static bool TryReadHandoff(PacketReader reader, out string ticket, out string worldEndpoint)
    {
        ticket = string.Empty;
        worldEndpoint = string.Empty;

        if (reader.Payload.Length == PayloadLength)
        {
            return false;
        }

        var offset = PayloadLength;
        return TryReadLengthPrefixedField(reader.Payload, ref offset, out ticket) &&
            TryReadLengthPrefixedField(reader.Payload, ref offset, out worldEndpoint) &&
            offset == reader.Payload.Length &&
            ticket.Length > 0 &&
            worldEndpoint.Length > 0;
    }

    private static byte[] GetLengthPrefixedField(string value, string parameterName)
    {
        var bytes = WireEncoding.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Length-prefixed field is too large.");
        }

        return bytes;
    }

    private static bool TryReadLengthPrefixedField(
        ReadOnlySpan<byte> payload,
        ref int offset,
        out string value)
    {
        value = string.Empty;

        if (offset > payload.Length - sizeof(ushort))
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt16LittleEndian(payload[offset..(offset + sizeof(ushort))]);
        offset += sizeof(ushort);
        if (offset > payload.Length - length)
        {
            return false;
        }

        value = WireEncoding.GetString(payload[offset..(offset + length)]);
        offset += length;
        return true;
    }
}
