using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyJiangHuInfoPacket(byte Mode, IReadOnlyList<string> Values)
{
    private const int HeaderPayloadLength = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyJiangHuInfo;
    public const byte IconBarMode = 0;

    public static bool TryCreateStartup(CharacterSnapshot snapshot, out LegacyJiangHuInfoPacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Reborn == 2)
        {
            packet = new LegacyJiangHuInfoPacket(IconBarMode, ["0"]);
            return true;
        }

        packet = default!;
        return false;
    }

    public static bool TryParse(PacketFrame frame, out LegacyJiangHuInfoPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode || frame.Payload.Length < HeaderPayloadLength)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var mode = payload[0];
        var count = payload[1];
        var offset = HeaderPayloadLength;
        var values = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            if (offset > payload.Length - sizeof(ushort))
            {
                return false;
            }

            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(
                payload.AsSpan(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (offset > payload.Length - length)
            {
                return false;
            }

            values.Add(Encoding.UTF8.GetString(payload.AsSpan(offset, length)));
            offset += length;
        }

        if (offset != payload.Length)
        {
            return false;
        }

        packet = new LegacyJiangHuInfoPacket(mode, values);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        if (Values.Count > byte.MaxValue)
        {
            throw new InvalidOperationException("Legacy JiangHu string-list count must fit one byte.");
        }

        var encoded = Values.Select(Encoding.UTF8.GetBytes).ToArray();
        var payloadLength = HeaderPayloadLength + encoded.Sum(value => sizeof(ushort) + value.Length);
        var writer = new PacketWriter(Opcode, payloadLength);
        writer.Payload[0] = Mode;
        writer.Payload[1] = (byte)encoded.Length;

        var offset = HeaderPayloadLength;
        foreach (var value in encoded)
        {
            if (value.Length > ushort.MaxValue)
            {
                throw new InvalidOperationException("Legacy JiangHu string must fit UInt16 length.");
            }

            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                writer.Payload.Slice(offset, sizeof(ushort)),
                (ushort)value.Length);
            offset += sizeof(ushort);
            value.CopyTo(writer.Payload.Slice(offset, value.Length));
            offset += value.Length;
        }

        return writer.ToArray();
    }
}
