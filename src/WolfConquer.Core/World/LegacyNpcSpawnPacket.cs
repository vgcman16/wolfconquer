using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyNpcSpawnPacket(
    uint Uid,
    uint Uid2,
    uint Unknown,
    ushort X,
    ushort Y,
    ushort Mesh,
    ushort Type,
    ushort Action)
{
    private const int UidField = 1;
    private const int Uid2Field = 2;
    private const int UnknownField = 3;
    private const int XField = 4;
    private const int YField = 5;
    private const int MeshField = 6;
    private const int TypeField = 7;
    private const int ActionField = 8;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyNpcSpawn;

    public static LegacyNpcSpawnPacket FromNpc(Npc npc, ushort action = 0, ushort? meshOverride = null)
    {
        var uid = CheckedUInt32(npc.Id, nameof(npc.Id));
        return new LegacyNpcSpawnPacket(
            uid,
            uid,
            Unknown: 0,
            CheckedUInt16(npc.X, nameof(npc.X)),
            CheckedUInt16(npc.Y, nameof(npc.Y)),
            meshOverride ?? CheckedUInt16(npc.Lookface, nameof(npc.Lookface)),
            CheckedUInt16(npc.Kind, nameof(npc.Kind)),
            action);
    }

    public static bool TryParse(PacketFrame frame, out LegacyNpcSpawnPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        uint uid = 0;
        uint uid2 = 0;
        uint unknown = 0;
        ushort x = 0;
        ushort y = 0;
        ushort mesh = 0;
        ushort type = 0;
        ushort action = 0;
        var hasUid = false;
        var hasUid2 = false;
        var hasUnknown = false;
        var hasX = false;
        var hasY = false;
        var hasMesh = false;
        var hasType = false;
        var hasAction = false;

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
                case UidField:
                    if (!TryToUInt32(value, out uid))
                    {
                        return false;
                    }

                    hasUid = true;
                    break;
                case Uid2Field:
                    if (!TryToUInt32(value, out uid2))
                    {
                        return false;
                    }

                    hasUid2 = true;
                    break;
                case UnknownField:
                    if (!TryToUInt32(value, out unknown))
                    {
                        return false;
                    }

                    hasUnknown = true;
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
                    if (!TryToUInt16(value, out type))
                    {
                        return false;
                    }

                    hasType = true;
                    break;
                case ActionField:
                    if (!TryToUInt16(value, out action))
                    {
                        return false;
                    }

                    hasAction = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasUid ||
            !hasUid2 ||
            !hasUnknown ||
            !hasX ||
            !hasY ||
            !hasMesh ||
            !hasType ||
            !hasAction)
        {
            return false;
        }

        packet = new LegacyNpcSpawnPacket(uid, uid2, unknown, x, y, mesh, type, action);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, Uid2Field, Uid2);
        WriteVarintField(payload, UnknownField, Unknown);
        WriteVarintField(payload, XField, X);
        WriteVarintField(payload, YField, Y);
        WriteVarintField(payload, MeshField, Mesh);
        WriteVarintField(payload, TypeField, Type);
        WriteVarintField(payload, ActionField, Action);

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

    private static uint CheckedUInt32(int value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy NPC spawn value cannot be outside uint range.");
        }

        return (uint)value;
    }

    private static ushort CheckedUInt16(int value, string name)
    {
        if (value is < 0 or > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy NPC spawn value cannot be outside ushort range.");
        }

        return (ushort)value;
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
