using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyMelterRankListEntry(
    uint Type,
    uint ItemId,
    bool Bound,
    string Name,
    uint Unknown3);

public sealed record LegacyMelterRankListPacket(
    uint CountInPage,
    uint Page,
    uint Count,
    IReadOnlyList<LegacyMelterRankListEntry> Entries)
{
    private const int CountInPageField = 1;
    private const int PageField = 2;
    private const int CountField = 3;
    private const int EntriesField = 4;
    private const int EntryTypeField = 1;
    private const int EntryItemIdField = 2;
    private const int EntryBoundField = 3;
    private const int EntryNameField = 4;
    private const int EntryUnknown3Field = 5;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyMelterRankList;

    public static LegacyMelterRankListPacket Query(uint page)
    {
        return new LegacyMelterRankListPacket(CountInPage: 0, page, Count: 0, Entries: []);
    }

    public static LegacyMelterRankListPacket EmptyResponse(uint page)
    {
        return new LegacyMelterRankListPacket(CountInPage: 0, page, Count: 0, Entries: []);
    }

    public static bool TryParse(PacketFrame frame, out LegacyMelterRankListPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new MelterProtoReader(frame.Payload.ToArray());
        var countInPage = 0U;
        var page = 0U;
        var count = 0U;
        var seenPage = false;
        var entries = new List<LegacyMelterRankListEntry>();

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case CountInPageField:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out countInPage))
                    {
                        return false;
                    }

                    break;

                case PageField:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out page))
                    {
                        return false;
                    }

                    seenPage = true;
                    break;

                case CountField:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out count))
                    {
                        return false;
                    }

                    break;

                case EntriesField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var entryPayload) ||
                        !TryParseEntry(entryPayload, out var entry))
                    {
                        return false;
                    }

                    entries.Add(entry);
                    break;

                default:
                    if (!reader.TrySkip(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (!seenPage)
        {
            return false;
        }

        packet = new LegacyMelterRankListPacket(countInPage, page, count, entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);

        var payload = new List<byte>();
        WriteVarintField(payload, CountInPageField, CountInPage);
        WriteVarintField(payload, PageField, Page);
        if (Count != 0)
        {
            WriteVarintField(payload, CountField, Count);
        }

        foreach (var entry in Entries)
        {
            var entryPayload = new List<byte>();
            WriteVarintField(entryPayload, EntryTypeField, entry.Type);
            WriteVarintField(entryPayload, EntryItemIdField, entry.ItemId);
            WriteBoolField(entryPayload, EntryBoundField, entry.Bound);
            WriteStringField(entryPayload, EntryNameField, entry.Name);
            WriteVarintField(entryPayload, EntryUnknown3Field, entry.Unknown3);
            WriteBytes(payload, EntriesField, entryPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseEntry(ReadOnlySpan<byte> payload, out LegacyMelterRankListEntry entry)
    {
        entry = default!;

        var reader = new MelterProtoReader(payload);
        var type = 0U;
        var itemId = 0U;
        var bound = false;
        var name = string.Empty;
        var unknown3 = 0U;
        var seen = 0;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case EntryTypeField:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out type))
                    {
                        return false;
                    }

                    seen |= 1 << EntryTypeField;
                    break;

                case EntryItemIdField:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out itemId))
                    {
                        return false;
                    }

                    seen |= 1 << EntryItemIdField;
                    break;

                case EntryBoundField:
                    if (wireType != VarintWireType || !reader.TryReadBool(out bound))
                    {
                        return false;
                    }

                    seen |= 1 << EntryBoundField;
                    break;

                case EntryNameField:
                    if (wireType != LengthDelimitedWireType || !reader.TryReadString(out name))
                    {
                        return false;
                    }

                    seen |= 1 << EntryNameField;
                    break;

                case EntryUnknown3Field:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out unknown3))
                    {
                        return false;
                    }

                    seen |= 1 << EntryUnknown3Field;
                    break;

                default:
                    if (!reader.TrySkip(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        var required =
            (1 << EntryTypeField) |
            (1 << EntryItemIdField) |
            (1 << EntryBoundField) |
            (1 << EntryNameField) |
            (1 << EntryUnknown3Field);
        if ((seen & required) != required)
        {
            return false;
        }

        entry = new LegacyMelterRankListEntry(type, itemId, bound, name, unknown3);
        return true;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteBoolField(List<byte> payload, int fieldNumber, bool value)
    {
        WriteVarintField(payload, fieldNumber, value ? 1UL : 0UL);
    }

    private static void WriteStringField(List<byte> payload, int fieldNumber, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteBytes(payload, fieldNumber, bytes);
    }

    private static void WriteBytes(List<byte> payload, int fieldNumber, IReadOnlyCollection<byte> value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
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
}

public sealed record LegacyMelterOptItem(
    uint Type,
    uint Id,
    bool Bound,
    bool AnnounceMessage);

public sealed record LegacyMelterOptPacket(
    uint Type,
    uint EntityId,
    string EntityName,
    IReadOnlyList<LegacyMelterOptItem> Items)
{
    private const int TypeField = 1;
    private const int EntityIdField = 2;
    private const int EntityNameField = 3;
    private const int ItemsField = 4;
    private const int ItemTypeField = 1;
    private const int ItemIdField = 2;
    private const int ItemBoundField = 3;
    private const int ItemAnnounceMessageField = 4;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyMelterOpt;

    public static bool TryParse(PacketFrame frame, out LegacyMelterOptPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new MelterProtoReader(frame.Payload.ToArray());
        var type = 0U;
        var entityId = 0U;
        var entityName = string.Empty;
        var seenType = false;
        var items = new List<LegacyMelterOptItem>();

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case TypeField:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out type))
                    {
                        return false;
                    }

                    seenType = true;
                    break;

                case EntityIdField:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out entityId))
                    {
                        return false;
                    }

                    break;

                case EntityNameField:
                    if (wireType != LengthDelimitedWireType || !reader.TryReadString(out entityName))
                    {
                        return false;
                    }

                    break;

                case ItemsField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadLengthDelimited(out var itemPayload) ||
                        !TryParseItem(itemPayload, out var item))
                    {
                        return false;
                    }

                    items.Add(item);
                    break;

                default:
                    if (!reader.TrySkip(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        if (!seenType)
        {
            return false;
        }

        packet = new LegacyMelterOptPacket(type, entityId, entityName, items);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Items);

        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, Type);
        if (EntityId != 0)
        {
            WriteVarintField(payload, EntityIdField, EntityId);
        }

        if (!string.IsNullOrEmpty(EntityName))
        {
            WriteStringField(payload, EntityNameField, EntityName);
        }

        foreach (var item in Items)
        {
            var itemPayload = new List<byte>();
            WriteVarintField(itemPayload, ItemTypeField, item.Type);
            WriteVarintField(itemPayload, ItemIdField, item.Id);
            WriteBoolField(itemPayload, ItemBoundField, item.Bound);
            WriteBoolField(itemPayload, ItemAnnounceMessageField, item.AnnounceMessage);
            WriteBytes(payload, ItemsField, itemPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseItem(ReadOnlySpan<byte> payload, out LegacyMelterOptItem item)
    {
        item = default!;

        var reader = new MelterProtoReader(payload);
        var type = 0U;
        var id = 0U;
        var bound = false;
        var announceMessage = false;
        var seen = 0;

        while (!reader.End)
        {
            if (!reader.TryReadTag(out var fieldNumber, out var wireType))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case ItemTypeField:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out type))
                    {
                        return false;
                    }

                    seen |= 1 << ItemTypeField;
                    break;

                case ItemIdField:
                    if (wireType != VarintWireType || !reader.TryReadUInt32(out id))
                    {
                        return false;
                    }

                    seen |= 1 << ItemIdField;
                    break;

                case ItemBoundField:
                    if (wireType != VarintWireType || !reader.TryReadBool(out bound))
                    {
                        return false;
                    }

                    seen |= 1 << ItemBoundField;
                    break;

                case ItemAnnounceMessageField:
                    if (wireType != VarintWireType || !reader.TryReadBool(out announceMessage))
                    {
                        return false;
                    }

                    seen |= 1 << ItemAnnounceMessageField;
                    break;

                default:
                    if (!reader.TrySkip(wireType))
                    {
                        return false;
                    }

                    break;
            }
        }

        var required =
            (1 << ItemTypeField) |
            (1 << ItemIdField) |
            (1 << ItemBoundField);
        if ((seen & required) != required)
        {
            return false;
        }

        item = new LegacyMelterOptItem(type, id, bound, announceMessage);
        return true;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteBoolField(List<byte> payload, int fieldNumber, bool value)
    {
        WriteVarintField(payload, fieldNumber, value ? 1UL : 0UL);
    }

    private static void WriteStringField(List<byte> payload, int fieldNumber, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteBytes(payload, fieldNumber, bytes);
    }

    private static void WriteBytes(List<byte> payload, int fieldNumber, IReadOnlyCollection<byte> value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)value.Count);
        payload.AddRange(value);
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
}

file ref struct MelterProtoReader(ReadOnlySpan<byte> payload)
{
    private const int VarintWireType = 0;
    private const int Fixed64WireType = 1;
    private const int LengthDelimitedWireType = 2;
    private const int Fixed32WireType = 5;

    private readonly ReadOnlySpan<byte> _payload = payload;
    private int _offset;

    public readonly bool End => _offset >= _payload.Length;

    public bool TryReadTag(out int fieldNumber, out int wireType)
    {
        fieldNumber = 0;
        wireType = 0;
        if (!TryReadVarint(out var tagValue) || tagValue == 0)
        {
            return false;
        }

        fieldNumber = (int)(tagValue >> 3);
        wireType = (int)(tagValue & 0x7);
        return fieldNumber > 0;
    }

    public bool TryReadUInt32(out uint value)
    {
        value = 0;
        if (!TryReadVarint(out var raw) || raw > uint.MaxValue)
        {
            return false;
        }

        value = (uint)raw;
        return true;
    }

    public bool TryReadBool(out bool value)
    {
        value = false;
        if (!TryReadVarint(out var raw) || raw > 1)
        {
            return false;
        }

        value = raw != 0;
        return true;
    }

    public bool TryReadString(out string value)
    {
        value = string.Empty;
        if (!TryReadLengthDelimited(out var bytes))
        {
            return false;
        }

        value = Encoding.UTF8.GetString(bytes);
        return true;
    }

    public bool TryReadLengthDelimited(out ReadOnlySpan<byte> value)
    {
        value = default;
        if (!TryReadVarint(out var length) ||
            length > int.MaxValue ||
            _payload.Length - _offset < (int)length)
        {
            return false;
        }

        value = _payload.Slice(_offset, (int)length);
        _offset += (int)length;
        return true;
    }

    public bool TrySkip(int wireType)
    {
        return wireType switch
        {
            VarintWireType => TryReadVarint(out _),
            Fixed64WireType => TryAdvance(sizeof(ulong)),
            LengthDelimitedWireType => TrySkipLengthDelimited(),
            Fixed32WireType => TryAdvance(sizeof(uint)),
            _ => false,
        };
    }

    private bool TryReadVarint(out ulong value)
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

    private bool TrySkipLengthDelimited()
    {
        if (!TryReadVarint(out var length) ||
            length > int.MaxValue)
        {
            return false;
        }

        return TryAdvance((int)length);
    }

    private bool TryAdvance(int count)
    {
        if (count < 0 ||
            _offset > _payload.Length - count)
        {
            return false;
        }

        _offset += count;
        return true;
    }
}
