using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyMailContentPacket(uint MailId, string Content)
{
    private const int MailIdOffset = 0;
    private const int ContentOffset = MailIdOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyMailContent;
    public const int ContentLength = 512;
    public const int PayloadLength = ContentOffset + ContentLength;

    public static bool TryParse(PacketFrame frame, out LegacyMailContentPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length != PayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var content = payload.AsSpan(ContentOffset, ContentLength);
        var terminator = content.IndexOf((byte)0);
        if (terminator >= 0)
        {
            content = content[..terminator];
        }

        packet = new LegacyMailContentPacket(
            BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(MailIdOffset, sizeof(uint))),
            Encoding.UTF8.GetString(content));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var contentBytes = Encoding.UTF8.GetBytes(Content);
        if (contentBytes.Length > ContentLength)
        {
            throw new ArgumentOutOfRangeException(nameof(Content), "Legacy mail content cannot exceed 512 encoded bytes.");
        }

        var writer = new PacketWriter(Opcode, PayloadLength);
        writer.WriteUInt32(MailIdOffset, MailId);
        contentBytes.CopyTo(writer.Payload[ContentOffset..]);
        return writer.ToArray();
    }
}
