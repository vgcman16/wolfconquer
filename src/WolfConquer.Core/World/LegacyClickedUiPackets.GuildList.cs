using System.Buffers;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyGuildListPacket(
    uint Type,
    IReadOnlyList<LegacyGuildListEntry> Items)
{
    private const int TypeField = 1;
    private const int ItemsField = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyGuildList;

    public static LegacyGuildListPacket Empty()
    {
        return new LegacyGuildListPacket(Type: 0, Items: []);
    }

    public static bool TryParse(PacketFrame frame, out LegacyGuildListPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new LegacyProtoWire.Reader(frame.Payload.ToArray());
        var type = 0U;
        var items = new List<LegacyGuildListEntry>();
        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadUInt32(out type))
                    {
                        return false;
                    }

                    break;
                case ItemsField:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var itemPayload) ||
                        !TryParseGuildListEntry(itemPayload, out var item))
                    {
                        return false;
                    }

                    items.Add(item);
                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        packet = new LegacyGuildListPacket(type, items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Items);

        var payload = new List<byte>();
        LegacyProtoWire.WriteVarintField(payload, TypeField, Type);
        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            LegacyProtoWire.WriteVarintField(itemPayload, 1, item.Id);
            LegacyProtoWire.WriteVarintField(itemPayload, 2, item.Rank);
            LegacyProtoWire.WriteStringField(itemPayload, 3, item.Name);
            LegacyProtoWire.WriteStringField(itemPayload, 4, item.LeaderName);
            LegacyProtoWire.WriteVarintField(itemPayload, 5, item.Level);
            LegacyProtoWire.WriteVarintField(itemPayload, 6, item.MemberCount);
            LegacyProtoWire.WriteVarintField(itemPayload, 7, item.MemberCountMax);
            LegacyProtoWire.WriteVarintField(itemPayload, 8, item.Prestige);
            LegacyProtoWire.WriteBoolField(itemPayload, 9, item.Join);
            LegacyProtoWire.WriteBytesField(payload, ItemsField, itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseGuildListEntry(ReadOnlySpan<byte> payload, out LegacyGuildListEntry entry)
    {
        entry = default!;
        var reader = new LegacyProtoWire.Reader(payload);
        var id = 0U;
        var rank = 0U;
        var name = string.Empty;
        var leaderName = string.Empty;
        var level = 0U;
        var memberCount = 0U;
        var memberCountMax = 0U;
        var prestige = 0UL;
        var join = false;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case 1:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out id))
                    {
                        return false;
                    }

                    break;
                case 2:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out rank))
                    {
                        return false;
                    }

                    break;
                case 3:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadString(out name))
                    {
                        return false;
                    }

                    break;
                case 4:
                    if (wireType != LegacyProtoWire.LengthDelimitedWireType || !reader.TryReadString(out leaderName))
                    {
                        return false;
                    }

                    break;
                case 5:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out level))
                    {
                        return false;
                    }

                    break;
                case 6:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out memberCount))
                    {
                        return false;
                    }

                    break;
                case 7:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadUInt32(out memberCountMax))
                    {
                        return false;
                    }

                    break;
                case 8:
                    if (wireType != LegacyProtoWire.VarintWireType ||
                        !reader.TryReadVarint(out prestige))
                    {
                        return false;
                    }

                    break;
                case 9:
                    if (wireType != LegacyProtoWire.VarintWireType || !reader.TryReadBool(out join))
                    {
                        return false;
                    }

                    break;
                default:
                    if (!reader.TrySkipField(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        entry = new LegacyGuildListEntry(id, rank, name, leaderName, level, memberCount, memberCountMax, prestige, join);
        return true;
    }
}

public sealed record LegacyGuildListEntry(
    uint Id,
    uint Rank,
    string Name,
    string LeaderName,
    uint Level,
    uint MemberCount,
    uint MemberCountMax,
    ulong Prestige,
    bool Join);
