using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyOverheadLeagueInfoPacket(
    uint Type,
    uint Uid,
    uint DwParam1,
    uint DwParam2,
    uint DwParam3,
    uint Id,
    string Text)
{
    private const int TypeField = 1;
    private const int UidField = 2;
    private const int DwParam1Field = 3;
    private const int DwParam2Field = 4;
    private const int DwParam3Field = 5;
    private const int IdField = 6;
    private const int TextField = 7;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;
    private const uint LegacyDefaultServerId = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyOverheadLeagueInfo;

    public static bool TryCreateStartup(
        CharacterSnapshot snapshot,
        out LegacyOverheadLeagueInfoPacket packet,
        uint serverId = LegacyDefaultServerId)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        packet = default!;
        if (!TryReadAttribute(snapshot, out var unionId, "UnionUID", "UnionID") || unionId == 0)
        {
            return false;
        }

        packet = new LegacyOverheadLeagueInfoPacket(
            Type: 0,
            Uid: CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            DwParam1: 0,
            DwParam2: 0,
            DwParam3: 0,
            Id: serverId,
            Text: string.Empty);
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyOverheadLeagueInfoPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var type = 0u;
        var uid = 0u;
        var dwParam1 = 0u;
        var dwParam2 = 0u;
        var dwParam3 = 0u;
        var id = 0u;
        var text = string.Empty;
        var uidSeen = false;
        var idSeen = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            switch (fieldNumber)
            {
                case TypeField:
                    if (!TryReadUInt32(ref reader, wireType, out type))
                    {
                        return false;
                    }
                    break;

                case UidField:
                    if (!TryReadUInt32(ref reader, wireType, out uid))
                    {
                        return false;
                    }

                    uidSeen = true;
                    break;

                case DwParam1Field:
                    if (!TryReadUInt32(ref reader, wireType, out dwParam1))
                    {
                        return false;
                    }
                    break;

                case DwParam2Field:
                    if (!TryReadUInt32(ref reader, wireType, out dwParam2))
                    {
                        return false;
                    }
                    break;

                case DwParam3Field:
                    if (!TryReadUInt32(ref reader, wireType, out dwParam3))
                    {
                        return false;
                    }
                    break;

                case IdField:
                    if (!TryReadUInt32(ref reader, wireType, out id))
                    {
                        return false;
                    }

                    idSeen = true;
                    break;

                case TextField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var textBytes))
                    {
                        return false;
                    }

                    text = Encoding.UTF8.GetString(textBytes);
                    break;

                default:
                    return false;
            }
        }

        if (!uidSeen || !idSeen)
        {
            return false;
        }

        packet = new LegacyOverheadLeagueInfoPacket(type, uid, dwParam1, dwParam2, dwParam3, id, text);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, Type);
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, DwParam1Field, DwParam1);
        WriteVarintField(payload, DwParam2Field, DwParam2);
        WriteVarintField(payload, DwParam3Field, DwParam3);
        WriteVarintField(payload, IdField, Id);
        if (!string.IsNullOrEmpty(Text))
        {
            WriteStringField(payload, TextField, Text);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryReadUInt32(ref ProtoReader reader, int wireType, out uint value)
    {
        value = 0;
        if (wireType != VarintWireType ||
            !reader.TryReadVarint(out var raw) ||
            raw > uint.MaxValue)
        {
            return false;
        }

        value = (uint)raw;
        return true;
    }

    private static bool TryReadAttribute(CharacterSnapshot snapshot, out uint value, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (snapshot.Attributes.TryGetValue(key, out var raw) &&
                uint.TryParse(raw, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must fit UInt32.");
        }

        return (uint)value;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteStringField(List<byte> payload, int fieldNumber, string value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        var bytes = Encoding.UTF8.GetBytes(value);
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

        public bool TryReadBytes(out byte[] value)
        {
            value = [];
            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset > _payload.Length - (int)length)
            {
                return false;
            }

            value = _payload.Slice(_offset, (int)length).ToArray();
            _offset += (int)length;
            return true;
        }
    }
}
