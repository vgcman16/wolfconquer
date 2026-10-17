using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyRuneStoragePacket(byte Type, uint ItemUID, uint HPAdd)
{
    private const int TypeField = 1;
    private const int ItemUIDField = 2;
    private const int HPAddField = 3;
    private const int VarintWireType = 0;

    public const ushort Opcode = WorldPacketOpcodes.LegacyRuneStorage;
    public const byte StartupType = 2;
    public const uint StartupItemUID = 0;

    public static LegacyRuneStoragePacket Startup(uint hpAdd = 0)
    {
        return new LegacyRuneStoragePacket(StartupType, StartupItemUID, hpAdd);
    }

    public static LegacyRuneStoragePacket Startup(
        PlayerItemSnapshot playerItems,
        IReadOnlyList<RuneStorageAttribute>? storageAttributes)
    {
        ArgumentNullException.ThrowIfNull(playerItems);

        return Startup(ComputeHPAdd(playerItems.RuneItems, storageAttributes ?? []));
    }

    public static uint ComputeStorageScore(PlayerItemSnapshot playerItems)
    {
        ArgumentNullException.ThrowIfNull(playerItems);
        return ComputeRuneStorageScore(playerItems.RuneItems);
    }

    public static bool TryParse(PacketFrame frame, out LegacyRuneStoragePacket packet)
    {
        packet = default;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        byte type = 0;
        uint itemUID = 0;
        uint hpAdd = 0;
        var hasType = false;
        var hasItemUID = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType || !reader.TryReadVarint(out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (value > byte.MaxValue)
                    {
                        return false;
                    }

                    type = (byte)value;
                    hasType = true;
                    break;
                case ItemUIDField:
                    if (value > uint.MaxValue)
                    {
                        return false;
                    }

                    itemUID = (uint)value;
                    hasItemUID = true;
                    break;
                case HPAddField:
                    if (value > uint.MaxValue)
                    {
                        return false;
                    }

                    hpAdd = (uint)value;
                    break;
                default:
                    return false;
            }
        }

        if (!hasType || !hasItemUID)
        {
            return false;
        }

        packet = new LegacyRuneStoragePacket(type, itemUID, hpAdd);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, Type);
        WriteVarintField(payload, ItemUIDField, ItemUID);

        if (HPAdd != 0)
        {
            WriteVarintField(payload, HPAddField, HPAdd);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
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

    private static uint ComputeHPAdd(
        IReadOnlyList<PlayerItemSnapshotItem> runeItems,
        IReadOnlyList<RuneStorageAttribute> storageAttributes)
    {
        var score = ComputeRuneStorageScore(runeItems);
        var hpAdd = 0u;

        foreach (var attribute in storageAttributes)
        {
            if (attribute.RequiredStorageScore < 0 || attribute.RequiredStorageScore > score)
            {
                continue;
            }

            foreach (var bonus in attribute.Bonuses)
            {
                if (bonus.AttributeType == 1 && bonus.Value > 0)
                {
                    hpAdd = checked(hpAdd + (uint)bonus.Value);
                }
            }
        }

        return hpAdd;
    }

    private static uint ComputeRuneStorageScore(IReadOnlyList<PlayerItemSnapshotItem> runeItems)
    {
        var score = 0u;
        foreach (var item in runeItems)
        {
            if (item.Position is 0 or 211)
            {
                continue;
            }

            var itemScore = 500u;
            if (item.ItemId / 10000 != 401)
            {
                itemScore = checked(itemScore + (300u * (item.ItemId % 100)));
            }

            score = checked(score + itemScore);
        }

        return score;
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
