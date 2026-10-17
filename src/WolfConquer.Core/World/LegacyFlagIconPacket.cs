using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyFlagIcon : uint
{
    TyrantAura = 1,
    FeandAura = 2,
    MetalAura = 3,
    WoodAura = 4,
    WaterAura = 5,
    FireAura = 6,
    EarthAura = 7,
    MagicDefender = 8,
    NobleSpirit = 9,
    WackeSpirit = 10,
}

public readonly record struct LegacyFlagIconPacket(
    uint Type,
    uint Uid,
    LegacyFlagIcon Icon,
    uint Level,
    uint Mode,
    uint Damage)
{
    private const int TypeField = 1;
    private const int UidField = 2;
    private const int IconField = 3;
    private const int LevelField = 4;
    private const int ModeField = 5;
    private const int DamageField = 6;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyFlagIcon;
    public const uint ResponseType = 3;
    public const uint ResponseMode = 30;

    public static LegacyFlagIconPacket CreateResponse(
        uint uid,
        LegacyFlagIcon icon,
        uint level,
        uint damage)
    {
        return new LegacyFlagIconPacket(ResponseType, uid, icon, level, ResponseMode, damage);
    }

    public static bool TryParse(PacketFrame frame, out LegacyFlagIconPacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var seen = 0;
        var type = 0U;
        var uid = 0U;
        var icon = LegacyFlagIcon.TyrantAura;
        var level = 0U;
        var mode = 0U;
        var damage = 0U;

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
                case TypeField:
                    type = (uint)value;
                    seen |= 1 << TypeField;
                    break;

                case UidField:
                    uid = (uint)value;
                    seen |= 1 << UidField;
                    break;

                case IconField:
                    icon = (LegacyFlagIcon)(uint)value;
                    seen |= 1 << IconField;
                    break;

                case LevelField:
                    level = (uint)value;
                    seen |= 1 << LevelField;
                    break;

                case ModeField:
                    mode = (uint)value;
                    seen |= 1 << ModeField;
                    break;

                case DamageField:
                    damage = (uint)value;
                    seen |= 1 << DamageField;
                    break;

                default:
                    return false;
            }
        }

        var required = (1 << TypeField) |
            (1 << UidField) |
            (1 << IconField) |
            (1 << LevelField) |
            (1 << ModeField) |
            (1 << DamageField);
        if ((seen & required) != required)
        {
            return false;
        }

        packet = new LegacyFlagIconPacket(type, uid, icon, level, mode, damage);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, Type);
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, IconField, (uint)Icon);
        WriteVarintField(payload, LevelField, Level);
        WriteVarintField(payload, ModeField, Mode);
        WriteVarintField(payload, DamageField, Damage);

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
