using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyMapStatusPacket(uint Id, uint BaseId, ulong Status, uint Weather = 0)
{
    private const int IdField = 1;
    private const int BaseIdField = 2;
    private const int StatusField = 3;
    private const int WeatherField = 4;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyMapStatus;

    public static LegacyMapStatusPacket FromMap(
        long mapId,
        IReadOnlyDictionary<int, MapMetadata>? mapsById)
    {
        var id = CheckedUInt32(mapId, nameof(mapId));
        var statusId = id;
        if (mapId is >= int.MinValue and <= int.MaxValue)
        {
            var alias = LegacyMapAliasResolver.Resolve((int)mapId);
            if (alias.IsAlias)
            {
                statusId = checked((uint)alias.BaseMapId);
            }
        }

        var status = 0UL;
        var statusLookupId = (int?)null;

        if (mapId is >= int.MinValue and <= int.MaxValue)
        {
            statusLookupId = checked((int)statusId);
        }

        if (statusLookupId is { } mapLookupId &&
            mapsById?.TryGetValue(mapLookupId, out var map) == true)
        {
            status = CheckedLegacyMapType(map.Type, map.Id);
        }

        return new LegacyMapStatusPacket(statusId, statusId, status);
    }

    public static bool TryParse(PacketFrame frame, out LegacyMapStatusPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        uint id = 0;
        uint baseId = 0;
        ulong status = 0;
        uint weather = 0;
        var hasId = false;
        var hasBaseId = false;
        var hasStatus = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case IdField:
                    if (value > uint.MaxValue)
                    {
                        return false;
                    }

                    id = (uint)value;
                    hasId = true;
                    break;
                case BaseIdField:
                    if (value > uint.MaxValue)
                    {
                        return false;
                    }

                    baseId = (uint)value;
                    hasBaseId = true;
                    break;
                case StatusField:
                    status = value;
                    hasStatus = true;
                    break;
                case WeatherField:
                    if (value > uint.MaxValue)
                    {
                        return false;
                    }

                    weather = (uint)value;
                    break;
                default:
                    return false;
            }
        }

        if (!hasId || !hasBaseId || !hasStatus)
        {
            return false;
        }

        packet = new LegacyMapStatusPacket(id, baseId, status, weather);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, IdField, Id);
        WriteVarintField(payload, BaseIdField, BaseId);
        WriteVarintField(payload, StatusField, Status);
        if (Weather != 0)
        {
            WriteVarintField(payload, WeatherField, Weather);
        }

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

    private static uint CheckedUInt32(long value, string name)
    {
        if (value is < 0 or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy map-status id cannot be outside uint range.");
        }

        return (uint)value;
    }

    private static ulong CheckedLegacyMapType(long value, int mapId)
    {
        if (value is < 0 or > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Legacy map {mapId} type cannot be outside uint range.");
        }

        return (ulong)value;
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
