using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct LegacyCollectionStorageActive(uint ItemId, uint Status);

public readonly record struct LegacyCollectionStorageEntry(
    uint ItemId,
    uint UnlockStatus,
    uint StorageStatus,
    uint Timestamp,
    uint Flags);

public sealed record LegacyCollectionStoragePacket(
    uint Type,
    uint CollectionId,
    IReadOnlyList<LegacyCollectionStorageActive> Scraps,
    IReadOnlyList<LegacyCollectionStorageEntry> Entries)
{
    private const int TypeField = 1;
    private const int CollectionIdField = 2;
    private const int ScrapsField = 3;
    private const int EntriesField = 4;
    private const int ItemIdField = 1;
    private const int StatusField = 2;
    private const int StorageStatusField = 3;
    private const int TimestampField = 4;
    private const int FlagsField = 5;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyCollectionStorage;

    public LegacyCollectionStorageActive Active => Scraps.Count > 0 ? Scraps[0] : default;

    public LegacyCollectionStoragePacket(
        uint Type,
        uint CollectionId,
        LegacyCollectionStorageActive Active,
        IReadOnlyList<LegacyCollectionStorageEntry> Entries)
        : this(Type, CollectionId, [Active], Entries)
    {
    }

    public static bool TryCreateFromSnapshot(CharacterSnapshot snapshot, out LegacyCollectionStoragePacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        packet = default!;

        if (!TryReadAttributeUInt32(snapshot.Attributes, "CollectionID", out var collectionId))
        {
            return false;
        }

        if (!snapshot.Attributes.TryGetValue("CollectionStorage", out var rawStorage) ||
            string.IsNullOrWhiteSpace(rawStorage))
        {
            packet = new LegacyCollectionStoragePacket(Type: 0, collectionId, Scraps: [], Entries: []);
            return true;
        }

        var parts = rawStorage.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 ||
            !TryParseInt32(parts[0], out var scrapCount) ||
            scrapCount < 0 ||
            parts.Length < 1 + (scrapCount * 2) + 1)
        {
            return false;
        }

        var scraps = new List<LegacyCollectionStorageActive>(scrapCount);
        var offset = 1;
        for (var i = 0; i < scrapCount; i++)
        {
            if (!TryParseUInt32(parts[offset], out var itemId) ||
                !TryParseUInt32(parts[offset + 1], out var amount))
            {
                return false;
            }

            scraps.Add(new LegacyCollectionStorageActive(itemId, amount));
            offset += 2;
        }

        if (!TryParseInt32(parts[offset], out var itemCount) ||
            itemCount < 0 ||
            parts.Length < offset + 1 + (itemCount * 5))
        {
            return false;
        }

        offset++;
        var entries = new List<LegacyCollectionStorageEntry>(itemCount);
        for (var i = 0; i < itemCount; i++)
        {
            if (!TryParseUInt32(parts[offset], out var itemId) ||
                !TryParseUInt32(parts[offset + 1], out var unlockStatus) ||
                !TryParseUInt32(parts[offset + 2], out var storageStatus) ||
                !TryParseUInt32(parts[offset + 3], out var expirationDate) ||
                !TryParseUInt32(parts[offset + 4], out var flags))
            {
                return false;
            }

            entries.Add(new LegacyCollectionStorageEntry(
                itemId,
                unlockStatus,
                storageStatus,
                ToLegacyRemainingTime(expirationDate),
                flags));
            offset += 5;
        }

        packet = new LegacyCollectionStoragePacket(
            Type: 0,
            collectionId,
            scraps,
            entries);
        return true;
    }

    public static bool TryParse(PacketFrame frame, out LegacyCollectionStoragePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var type = 0u;
        var collectionId = 0u;
        var scraps = new List<LegacyCollectionStorageActive>();
        var entries = new List<LegacyCollectionStorageEntry>();
        var hasType = false;
        var hasCollectionId = false;

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
                    if (wireType != VarintWireType ||
                        !reader.TryReadVarint(out var typeValue) ||
                        typeValue > uint.MaxValue)
                    {
                        return false;
                    }

                    type = (uint)typeValue;
                    hasType = true;
                    break;
                case CollectionIdField:
                    if (wireType != VarintWireType ||
                        !reader.TryReadVarint(out var collectionValue) ||
                        collectionValue > uint.MaxValue)
                    {
                        return false;
                    }

                    collectionId = (uint)collectionValue;
                    hasCollectionId = true;
                    break;
                case ScrapsField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var activeBytes) ||
                        !TryParseActive(activeBytes, out var scrap))
                    {
                        return false;
                    }

                    scraps.Add(scrap);
                    break;
                case EntriesField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var entryBytes) ||
                        !TryParseEntry(entryBytes, out var entry))
                    {
                        return false;
                    }

                    entries.Add(entry);
                    break;
                default:
                    return false;
            }
        }

        if (!hasType || !hasCollectionId)
        {
            return false;
        }

        packet = new LegacyCollectionStoragePacket(type, collectionId, scraps, entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Scraps);
        ArgumentNullException.ThrowIfNull(Entries);

        var payload = new List<byte>();
        WriteVarintField(payload, TypeField, Type);
        WriteVarintField(payload, CollectionIdField, CollectionId);

        foreach (var scrap in Scraps)
        {
            var activePayload = new List<byte>();
            WriteVarintField(activePayload, ItemIdField, scrap.ItemId);
            WriteVarintField(activePayload, StatusField, scrap.Status);
            WriteBytes(payload, ScrapsField, activePayload);
        }

        foreach (var entry in Entries)
        {
            var entryPayload = new List<byte>();
            WriteVarintField(entryPayload, ItemIdField, entry.ItemId);
            WriteVarintField(entryPayload, StatusField, entry.UnlockStatus);
            WriteVarintField(entryPayload, StorageStatusField, entry.StorageStatus);
            WriteVarintField(entryPayload, TimestampField, entry.Timestamp);
            WriteVarintField(entryPayload, FlagsField, entry.Flags);
            WriteBytes(payload, EntriesField, entryPayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseActive(byte[] payload, out LegacyCollectionStorageActive active)
    {
        active = default;
        var reader = new ProtoReader(payload);
        var itemId = 0u;
        var status = 0u;
        var hasItemId = false;
        var hasStatus = false;

        while (!reader.End)
        {
            if (!TryReadNestedUInt32(ref reader, out var fieldNumber, out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case ItemIdField:
                    itemId = value;
                    hasItemId = true;
                    break;
                case StatusField:
                    status = value;
                    hasStatus = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasItemId || !hasStatus)
        {
            return false;
        }

        active = new LegacyCollectionStorageActive(itemId, status);
        return true;
    }

    private static bool TryParseEntry(byte[] payload, out LegacyCollectionStorageEntry entry)
    {
        entry = default;
        var reader = new ProtoReader(payload);
        var itemId = 0u;
        var unlockStatus = 0u;
        var storageStatus = 0u;
        var timestamp = 0u;
        var flags = 0u;
        var hasItemId = false;
        var hasUnlockStatus = false;
        var hasStorageStatus = false;
        var hasTimestamp = false;
        var hasFlags = false;

        while (!reader.End)
        {
            if (!TryReadNestedUInt32(ref reader, out var fieldNumber, out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case ItemIdField:
                    itemId = value;
                    hasItemId = true;
                    break;
                case StatusField:
                    unlockStatus = value;
                    hasUnlockStatus = true;
                    break;
                case StorageStatusField:
                    storageStatus = value;
                    hasStorageStatus = true;
                    break;
                case TimestampField:
                    timestamp = value;
                    hasTimestamp = true;
                    break;
                case FlagsField:
                    flags = value;
                    hasFlags = true;
                    break;
                default:
                    return false;
            }
        }

        if (!hasItemId || !hasUnlockStatus || !hasStorageStatus || !hasTimestamp || !hasFlags)
        {
            return false;
        }

        entry = new LegacyCollectionStorageEntry(itemId, unlockStatus, storageStatus, timestamp, flags);
        return true;
    }

    private static bool TryReadNestedUInt32(
        ref ProtoReader reader,
        out int fieldNumber,
        out uint value)
    {
        fieldNumber = 0;
        value = 0;

        if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
        {
            return false;
        }

        fieldNumber = (int)(tagValue >> 3);
        var wireType = (int)(tagValue & 0x7);
        if (wireType != VarintWireType ||
            !reader.TryReadVarint(out var rawValue) ||
            rawValue > uint.MaxValue)
        {
            return false;
        }

        value = (uint)rawValue;
        return true;
    }

    private static bool TryReadAttributeUInt32(
        IReadOnlyDictionary<string, string> attributes,
        string key,
        out uint value)
    {
        value = 0;
        return attributes.TryGetValue(key, out var raw) && TryParseUInt32(raw, out value);
    }

    private static bool TryParseInt32(string raw, out int value)
    {
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseUInt32(string raw, out uint value)
    {
        return uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static uint ToLegacyRemainingTime(uint expirationDate)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return expirationDate > now ? checked((uint)(expirationDate - now)) : 0;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, uint value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
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
