using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyBroadcastAction : uint
{
    ReleaseSoonMessages = 1,
    MyMessages = 2,
    BroadcastMessage = 3,
    Urgent15ConquerPoints = 4,
    Urgent5ConquerPoints = 5,
}

public sealed record LegacyBroadcastPacket(
    LegacyBroadcastAction Action,
    uint Parameter,
    IReadOnlyList<string> Strings)
{
    private const int ActionOffset = 0;
    private const int ParameterOffset = ActionOffset + sizeof(uint);
    private const int StringCountOffset = ParameterOffset + sizeof(uint);
    private const int StringsOffset = StringCountOffset + sizeof(byte);

    public const ushort Opcode = WorldPacketOpcodes.LegacyBroadcast;

    public static bool TryParse(PacketFrame frame, out LegacyBroadcastPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < StringsOffset)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var action = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ActionOffset, sizeof(uint)));
        if (!Enum.IsDefined(typeof(LegacyBroadcastAction), action))
        {
            return false;
        }

        var parameter = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(ParameterOffset, sizeof(uint)));
        var count = payload[StringCountOffset];
        var offset = StringsOffset;
        var strings = new List<string>(count);
        for (var index = 0; index < count; index++)
        {
            if (offset > payload.Length - sizeof(ushort))
            {
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, sizeof(ushort)));
            offset += sizeof(ushort);
            if (offset > payload.Length - length)
            {
                return false;
            }

            strings.Add(Encoding.UTF8.GetString(payload.AsSpan(offset, length)));
            offset += length;
        }

        if (offset != payload.Length)
        {
            return false;
        }

        packet = new LegacyBroadcastPacket((LegacyBroadcastAction)action, parameter, strings);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        if (Strings.Count > byte.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Strings), "Legacy broadcast packets cannot contain more than 255 strings.");
        }

        var encoded = new byte[Strings.Count][];
        var payloadLength = StringsOffset;
        for (var index = 0; index < Strings.Count; index++)
        {
            encoded[index] = Encoding.UTF8.GetBytes(Strings[index]);
            if (encoded[index].Length > ushort.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(Strings), "Legacy broadcast string entries cannot exceed 65,535 bytes.");
            }

            payloadLength += sizeof(ushort) + encoded[index].Length;
        }

        var writer = new PacketWriter(Opcode, payloadLength);
        writer.WriteUInt32(ActionOffset, (uint)Action);
        writer.WriteUInt32(ParameterOffset, Parameter);
        writer.Payload[StringCountOffset] = (byte)encoded.Length;
        var offset = StringsOffset;
        foreach (var entry in encoded)
        {
            writer.WriteUInt16(offset, (ushort)entry.Length);
            offset += sizeof(ushort);
            entry.CopyTo(writer.Payload[offset..]);
            offset += entry.Length;
        }

        return writer.ToArray();
    }
}

public sealed record LegacyBroadcastListEntry(
    uint Id,
    uint Index,
    uint EntityId,
    string EntityName,
    uint SpentConquerPoints,
    string Message,
    uint UnionRank);

public sealed record LegacyBroadcastListPacket(
    uint Page,
    ushort Segment,
    ushort Count,
    IReadOnlyList<LegacyBroadcastListEntry> Entries)
{
    private const int PageOffset = 0;
    private const int SegmentOffset = PageOffset + sizeof(uint);
    private const int CountOffset = SegmentOffset + sizeof(ushort);
    private const int EntriesOffset = CountOffset + sizeof(ushort);
    private const int EntryLength = 156;
    private const int EntryIdOffset = 0;
    private const int EntryIndexOffset = EntryIdOffset + sizeof(uint);
    private const int EntryEntityIdOffset = EntryIndexOffset + sizeof(uint);
    private const int EntryEntityNameOffset = EntryEntityIdOffset + sizeof(uint);
    private const int EntryEntityNameLength = 32;
    private const int EntrySpentConquerPointsOffset = EntryEntityNameOffset + EntryEntityNameLength;
    private const int EntryMessageOffset = EntrySpentConquerPointsOffset + sizeof(uint);
    private const int EntryMessageLength = 80;
    private const int EntryReservedOffset = EntryMessageOffset + EntryMessageLength;
    private const int EntryReservedLength = 20;
    private const int EntryUnionRankOffset = EntryReservedOffset + EntryReservedLength;
    private const int EntryTailReservedOffset = EntryUnionRankOffset + sizeof(uint);

    public const ushort Opcode = WorldPacketOpcodes.LegacyBroadcastList;

    public static bool TryParse(PacketFrame frame, out LegacyBroadcastListPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < EntriesOffset ||
            (frame.Payload.Length - EntriesOffset) % EntryLength != 0)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var count = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(CountOffset, sizeof(ushort)));
        var actualCount = (payload.Length - EntriesOffset) / EntryLength;
        if (count != actualCount)
        {
            return false;
        }

        var entries = new List<LegacyBroadcastListEntry>(count);
        for (var index = 0; index < count; index++)
        {
            var offset = EntriesOffset + (index * EntryLength);
            entries.Add(new LegacyBroadcastListEntry(
                ReadUInt32(payload, offset + EntryIdOffset),
                ReadUInt32(payload, offset + EntryIndexOffset),
                ReadUInt32(payload, offset + EntryEntityIdOffset),
                ReadFixedString(payload.AsSpan(offset + EntryEntityNameOffset, EntryEntityNameLength)),
                ReadUInt32(payload, offset + EntrySpentConquerPointsOffset),
                ReadFixedString(payload.AsSpan(offset + EntryMessageOffset, EntryMessageLength)),
                ReadUInt32(payload, offset + EntryUnionRankOffset)));
        }

        packet = new LegacyBroadcastListPacket(
            ReadUInt32(payload, PageOffset),
            BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(SegmentOffset, sizeof(ushort))),
            count,
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        if (Entries.Count > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(Entries), "Legacy broadcast list packets cannot contain more than 65,535 entries.");
        }

        var writer = new PacketWriter(Opcode, EntriesOffset + (Entries.Count * EntryLength));
        writer.WriteUInt32(PageOffset, Page);
        writer.WriteUInt16(SegmentOffset, Segment);
        writer.WriteUInt16(CountOffset, checked((ushort)Entries.Count));
        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var offset = EntriesOffset + (index * EntryLength);
            writer.WriteUInt32(offset + EntryIdOffset, entry.Id);
            writer.WriteUInt32(offset + EntryIndexOffset, entry.Index);
            writer.WriteUInt32(offset + EntryEntityIdOffset, entry.EntityId);
            WriteFixedString(writer.Payload.Slice(offset + EntryEntityNameOffset, EntryEntityNameLength), entry.EntityName);
            writer.WriteUInt32(offset + EntrySpentConquerPointsOffset, entry.SpentConquerPoints);
            WriteFixedString(writer.Payload.Slice(offset + EntryMessageOffset, EntryMessageLength), entry.Message);
            writer.WriteUInt32(offset + EntryUnionRankOffset, entry.UnionRank);
            writer.WriteUInt32(offset + EntryTailReservedOffset, 0);
        }

        return writer.ToArray();
    }

    private static uint ReadUInt32(byte[] payload, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, sizeof(uint)));
    }

    private static string ReadFixedString(ReadOnlySpan<byte> bytes)
    {
        var length = bytes.IndexOf((byte)0);
        if (length < 0)
        {
            length = bytes.Length;
        }

        return Encoding.UTF8.GetString(bytes[..length]);
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        bytes.AsSpan(0, Math.Min(bytes.Length, destination.Length)).CopyTo(destination);
    }
}

internal static class LegacyBroadcastRuntime
{
    private const int MaxBroadcasts = 50;
    private static readonly object s_lock = new();
    private static readonly List<LegacyBroadcastListEntry> s_broadcasts = [];
    private static uint s_nextId = 1;

    public static bool TryAdd(uint entityId, string entityName, string message, uint unionRank, out LegacyBroadcastListEntry entry)
    {
        lock (s_lock)
        {
            if (IsFullNoLock())
            {
                entry = default!;
                return false;
            }

            entry = new LegacyBroadcastListEntry(
                s_nextId++,
                checked((uint)s_broadcasts.Count),
                entityId,
                entityName,
                0,
                message,
                unionRank);
            s_broadcasts.Add(entry);
            return true;
        }
    }

    public static IReadOnlyList<LegacyBroadcastListEntry> Snapshot(bool onlyEntity, uint entityId)
    {
        lock (s_lock)
        {
            return s_broadcasts
                .Where(entry => !onlyEntity || entry.EntityId == entityId)
                .Select((entry, index) => entry with { Index = checked((uint)index) })
                .ToArray();
        }
    }

    public static bool IsFull()
    {
        lock (s_lock)
        {
            return IsFullNoLock();
        }
    }

    public static bool TrySpendToUrgent(uint id, uint amount, bool moveToTop)
    {
        lock (s_lock)
        {
            var index = s_broadcasts.FindIndex(entry => entry.Id == id);
            if (index < 0)
            {
                return false;
            }

            var updated = s_broadcasts[index] with
            {
                SpentConquerPoints = s_broadcasts[index].SpentConquerPoints + amount,
            };

            if (moveToTop)
            {
                s_broadcasts.RemoveAt(index);
                s_broadcasts.Insert(0, updated);
                return true;
            }

            s_broadcasts[index] = updated;
            while (index > 0 && s_broadcasts[index - 1].SpentConquerPoints <= updated.SpentConquerPoints)
            {
                s_broadcasts[index] = s_broadcasts[index - 1];
                index--;
                s_broadcasts[index] = updated;
            }

            return true;
        }
    }

    public static void Clear()
    {
        lock (s_lock)
        {
            s_broadcasts.Clear();
            s_nextId = 1;
        }
    }

    private static bool IsFullNoLock()
    {
        return s_broadcasts.Count >= MaxBroadcasts;
    }
}
