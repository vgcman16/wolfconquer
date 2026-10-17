using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacySobNpcSpawnPacket(
    uint Uid,
    uint OwnerUid,
    uint Unknown2,
    uint MaxHitpoints,
    uint Hitpoints,
    ushort X,
    ushort Y,
    ushort Mesh,
    byte Type,
    ushort Sort,
    string Name)
{
    private const int UidField = 1;
    private const int OwnerUidField = 2;
    private const int Unknown2Field = 3;
    private const int MaxHitpointsField = 4;
    private const int HitpointsField = 5;
    private const int XField = 6;
    private const int YField = 7;
    private const int MeshField = 8;
    private const int TypeField = 9;
    private const int SortField = 10;
    private const int NameField = 12;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacySobNpcSpawn;

    public static LegacySobNpcSpawnPacket FromSobNpc(SobNpc sobNpc, bool useLocalizedName = false)
    {
        var name = LegacySobNpcEnglishName.Resolve(sobNpc, useLocalizedName);
        var hitpoints = sobNpc.MapId == 1039 ? 10_000 : sobNpc.Life;
        var maxHitpoints = sobNpc.MapId == 1039 ? 10_000 : sobNpc.MaxLife;

        return new LegacySobNpcSpawnPacket(
            CheckedUInt32(sobNpc.Id, nameof(sobNpc.Id)),
            OwnerUid: 0,
            Unknown2: 0,
            CheckedUInt32(maxHitpoints, nameof(sobNpc.MaxLife)),
            CheckedUInt32(hitpoints, nameof(sobNpc.Life)),
            CheckedUInt16(sobNpc.X, nameof(sobNpc.X)),
            CheckedUInt16(sobNpc.Y, nameof(sobNpc.Y)),
            CheckedUInt16(sobNpc.Lookface, nameof(sobNpc.Lookface)),
            CheckedByte(sobNpc.Kind, nameof(sobNpc.Kind)),
            CheckedUInt16(sobNpc.Unknown9, nameof(sobNpc.Unknown9)),
            name);
    }

    public static bool TryParse(PacketFrame frame, out LegacySobNpcSpawnPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        uint uid = 0;
        uint ownerUid = 0;
        uint unknown2 = 0;
        uint maxHitpoints = 0;
        uint hitpoints = 0;
        ushort x = 0;
        ushort y = 0;
        ushort mesh = 0;
        byte type = 0;
        ushort sort = 0;
        var name = string.Empty;
        var hasUid = false;
        var hasOwnerUid = false;
        var hasUnknown2 = false;
        var hasMaxHitpoints = false;
        var hasHitpoints = false;
        var hasX = false;
        var hasY = false;
        var hasMesh = false;
        var hasType = false;
        var hasSort = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (fieldNumber == NameField)
            {
                if (wireType != LengthDelimitedWireType ||
                    !reader.TryReadString(out name))
                {
                    return false;
                }

                continue;
            }

            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case UidField:
                    if (!TryToUInt32(value, out uid))
                    {
                        return false;
                    }

                    hasUid = true;
                    break;
                case OwnerUidField:
                    if (!TryToUInt32(value, out ownerUid))
                    {
                        return false;
                    }

                    hasOwnerUid = true;
                    break;
                case Unknown2Field:
                    if (!TryToUInt32(value, out unknown2))
                    {
                        return false;
                    }

                    hasUnknown2 = true;
                    break;
                case MaxHitpointsField:
                    if (!TryToUInt32(value, out maxHitpoints))
                    {
                        return false;
                    }

                    hasMaxHitpoints = true;
                    break;
                case HitpointsField:
                    if (!TryToUInt32(value, out hitpoints))
                    {
                        return false;
                    }

                    hasHitpoints = true;
                    break;
                case XField:
                    if (!TryToUInt16(value, out x))
                    {
                        return false;
                    }

                    hasX = true;
                    break;
                case YField:
                    if (!TryToUInt16(value, out y))
                    {
                        return false;
                    }

                    hasY = true;
                    break;
                case MeshField:
                    if (!TryToUInt16(value, out mesh))
                    {
                        return false;
                    }

                    hasMesh = true;
                    break;
                case TypeField:
                    if (!TryToByte(value, out type))
                    {
                        return false;
                    }

                    hasType = true;
                    break;
                case SortField:
                    if (!TryToUInt16(value, out sort))
                    {
                        return false;
                    }

                    hasSort = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasUid ||
            !hasOwnerUid ||
            !hasUnknown2 ||
            !hasMaxHitpoints ||
            !hasHitpoints ||
            !hasX ||
            !hasY ||
            !hasMesh ||
            !hasType ||
            !hasSort)
        {
            return false;
        }

        packet = new LegacySobNpcSpawnPacket(
            uid,
            ownerUid,
            unknown2,
            maxHitpoints,
            hitpoints,
            x,
            y,
            mesh,
            type,
            sort,
            name);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, OwnerUidField, OwnerUid);
        WriteVarintField(payload, Unknown2Field, Unknown2);
        WriteVarintField(payload, MaxHitpointsField, MaxHitpoints);
        WriteVarintField(payload, HitpointsField, Hitpoints);
        WriteVarintField(payload, XField, X);
        WriteVarintField(payload, YField, Y);
        WriteVarintField(payload, MeshField, Mesh);
        WriteVarintField(payload, TypeField, Type);
        WriteVarintField(payload, SortField, Sort);
        WriteString(payload, NameField, Name);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
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

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy SobNpc spawn value cannot be outside uint range.");
        }

        return (uint)value;
    }

    private static ushort CheckedUInt16(int value, string name)
    {
        if (value is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy SobNpc spawn value cannot be outside ushort range.");
        }

        return (ushort)value;
    }

    private static byte CheckedByte(int value, string name)
    {
        if (value is < 0 or > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy SobNpc spawn value cannot be outside byte range.");
        }

        return (byte)value;
    }

    private static bool TryToUInt32(ulong value, out uint result)
    {
        if (value > uint.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }

    private static bool TryToUInt16(ulong value, out ushort result)
    {
        if (value > ushort.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (ushort)value;
        return true;
    }

    private static bool TryToByte(ulong value, out byte result)
    {
        if (value > byte.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (byte)value;
        return true;
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

        public bool TryReadString(out string value)
        {
            value = string.Empty;

            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset > _payload.Length - (int)length)
            {
                return false;
            }

            value = Encoding.UTF8.GetString(_payload.Slice(_offset, (int)length));
            _offset += (int)length;
            return true;
        }
    }
}
