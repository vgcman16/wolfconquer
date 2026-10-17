using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record InternalWorldEnterRequestPacket(string Ticket)
{
    private static readonly Encoding WireEncoding = Encoding.UTF8;

    public static bool TryParse(PacketFrame frame, out InternalWorldEnterRequestPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != WorldPacketOpcodes.InternalWorldEnterRequest ||
            frame.Payload.Length < sizeof(ushort) ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var length = reader.ReadUInt16(0);
        if (length == 0 ||
            frame.Payload.Length != sizeof(ushort) + length)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        packet = new InternalWorldEnterRequestPacket(
            WireEncoding.GetString(payload.AsSpan(sizeof(ushort), length)));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Ticket);

        var ticketBytes = WireEncoding.GetBytes(Ticket);
        if (ticketBytes.Length > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Ticket), "World enter ticket is too large.");
        }

        var writer = new PacketWriter(
            WorldPacketOpcodes.InternalWorldEnterRequest,
            sizeof(ushort) + ticketBytes.Length);
        writer.WriteUInt16(0, (ushort)ticketBytes.Length);
        ticketBytes.CopyTo(writer.Payload[sizeof(ushort)..]);
        return writer.ToArray();
    }
}
