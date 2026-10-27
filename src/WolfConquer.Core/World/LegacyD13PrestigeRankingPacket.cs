using System.Buffers;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Core.World;

/// <summary>
/// Complete raw action-6 frames only. No query provider, text conversion or
/// continuation policy is implied by this VisibleDx8D13 layout codec.
/// </summary>
public sealed partial record LegacyD13PrestigeRankingPacket(
    uint RankType,
    ushort RegisteredCount,
    byte Page,
    byte Finish,
    uint HeaderValue,
    IReadOnlyList<LegacyD13PrestigeRankingEntry> Entries)
{
    public const ConquerProtocolProfile Profile = ConquerProtocolProfile.VisibleDx8D13;
    public const ushort Opcode = WorldPacketOpcodes.LegacyGenericRanking;
    public const int EntriesOffset = 17;
    public const int EntryWireLength = 113;

    public static int PayloadLengthForEntryCount(int entryCount)
    {
        if (entryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entryCount));
        }

        return checked(EntriesOffset + (entryCount * EntryWireLength));
    }

    public static bool TryParse(PacketFrame frame, out LegacyD13PrestigeRankingPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode ||
            frame.Frame.Length != frame.Header.Length ||
            frame.Payload.Length < EntriesOffset ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader) ||
            reader.Header != frame.Header ||
            reader.Payload.Length != frame.Payload.Length ||
            reader.Payload[0] != (byte)LegacyGenericRankingAction.PrestigeRanks)
        {
            return false;
        }

        var count = reader.ReadInt32(9);
        if (count < 0 || reader.Payload.Length != EntriesOffset + ((long)count * EntryWireLength))
        {
            return false;
        }

        var entries = new LegacyD13PrestigeRankingEntry[count];
        for (var index = 0; index < count; index++)
        {
            entries[index] = ReadEntry(reader.Payload.Slice(EntriesOffset + (index * EntryWireLength), EntryWireLength));
        }

        packet = new LegacyD13PrestigeRankingPacket(
            reader.ReadUInt32(1), reader.ReadUInt16(5), reader.Payload[7],
            reader.Payload[8], reader.ReadUInt32(13), Array.AsReadOnly(entries));
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var entries = SnapshotEntries(Entries);
        var writer = new PacketWriter(Opcode, PayloadLengthForEntryCount(entries.Length));
        writer.Payload[0] = (byte)LegacyGenericRankingAction.PrestigeRanks;
        writer.WriteUInt32(1, RankType);
        writer.WriteUInt16(5, RegisteredCount);
        writer.Payload[7] = Page;
        writer.Payload[8] = Finish;
        writer.WriteInt32(9, entries.Length);
        writer.WriteUInt32(13, HeaderValue);

        for (var index = 0; index < entries.Length; index++)
        {
            WriteEntry(writer.Payload.Slice(EntriesOffset + (index * EntryWireLength), EntryWireLength), entries[index]);
        }

        return writer.ToArray();
    }
}
