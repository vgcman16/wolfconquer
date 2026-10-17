using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct MsgGoldLeaguePointPacket(uint Points, uint HistoryPoints)
{
    private const int PointsField = 1;
    private const int HistoryPointsField = 2;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyGoldLeaguePoint;

    public static bool TryParse(PacketFrame frame, out MsgGoldLeaguePointPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var pointsSeen = false;
        var historySeen = false;
        var points = 0U;
        var historyPoints = 0U;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var value) ||
                value > uint.MaxValue)
            {
                return false;
            }

            switch (fieldNumber)
            {
                case PointsField:
                    points = (uint)value;
                    pointsSeen = true;
                    break;

                case HistoryPointsField:
                    historyPoints = (uint)value;
                    historySeen = true;
                    break;

                default:
                    return false;
            }
        }

        if (!pointsSeen || !historySeen)
        {
            return false;
        }

        packet = new MsgGoldLeaguePointPacket(points, historyPoints);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, PointsField, Points);
        WriteVarintField(payload, HistoryPointsField, HistoryPoints);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
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
    }
}
