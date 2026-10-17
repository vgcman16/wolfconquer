using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyGenericRankingAction : byte
{
    Ranking = 1,
    QueryCount = 2,
    UpdateScreen = 4,
    InformationRequest = 5,
    PrestigeRanks = 6,
}

public enum LegacyGenericRankingRankType : uint
{
    None = 0,
    Chi = 60000000,
    DragonChi = 60000001,
    PhoenixChi = 60000002,
    TigerChi = 60000003,
    TurtleChi = 60000004,
}

public sealed record LegacyGenericRankingEntry(
    byte Rank,
    long Amount,
    uint Uid,
    uint MirrorUid,
    string Name,
    string SecondaryName);

public sealed record LegacyGenericRankingPacket(
    LegacyGenericRankingAction Action,
    LegacyGenericRankingRankType RankType,
    ushort RegisteredCount,
    byte Page,
    byte Finish,
    int Count,
    uint Reserved,
    IReadOnlyList<LegacyGenericRankingEntry> Entries)
{
    private static readonly Encoding NameEncoding = Encoding.UTF8;

    private const int ActionOffset = 0;
    private const int RankTypeOffset = ActionOffset + sizeof(byte);
    private const int RegisteredCountOffset = RankTypeOffset + sizeof(uint);
    private const int PageOffset = RegisteredCountOffset + sizeof(ushort);
    private const int FinishOffset = PageOffset + sizeof(byte);
    private const int CountOffset = FinishOffset + sizeof(byte);
    private const int InboundHeaderLength = CountOffset + sizeof(int);
    private const int ReservedOffset = CountOffset + sizeof(int);
    private const int EntryRankOffset = 0;
    private const int EntryAmountOffset = EntryRankOffset + sizeof(byte);
    private const int EntryUidOffset = EntryAmountOffset + sizeof(long);
    private const int EntryMirrorUidOffset = EntryUidOffset + sizeof(uint);
    private const int EntryNameOffset = EntryMirrorUidOffset + sizeof(uint);
    private const int EntrySecondaryNameOffset = EntryNameOffset + FixedNameLength;
    private const int EntryReservedOffset = EntrySecondaryNameOffset + FixedNameLength;
    private const int EntryReservedLength = 28;

    public const ushort Opcode = WorldPacketOpcodes.LegacyGenericRanking;
    public const int EntriesOffset = ReservedOffset + sizeof(uint);
    public const int FixedNameLength = 32;
    public const int SimpleEntryWireLength = EntryReservedOffset + EntryReservedLength;

    public static LegacyGenericRankingPacket ChiQueryCount(
        long uid,
        string name,
        LegacyChiPowerState power)
    {
        ArgumentNullException.ThrowIfNull(power);

        var checkedUid = CheckedUInt32(uid, nameof(uid));
        return new LegacyGenericRankingPacket(
            LegacyGenericRankingAction.QueryCount,
            (LegacyGenericRankingRankType)(60000000u + (uint)power.Type),
            RegisteredCount: 0,
            Page: 0,
            Finish: 0,
            Count: 1,
            Reserved: 0,
            Entries:
            [
                new LegacyGenericRankingEntry(
                    Rank: 1,
                    Amount: Score(power),
                    checkedUid,
                    checkedUid,
                    name,
                    name),
            ]);
    }

    public static IReadOnlyList<LegacyGenericRankingPacket> CreateChiQueryCountStartup(
        long uid,
        string name,
        LegacyChiState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Powers
            .Select(power => ChiQueryCount(uid, name, power))
            .ToArray();
    }

    public static LegacyGenericRankingPacket EmptyResponse(
        LegacyGenericRankingAction action,
        LegacyGenericRankingRankType rankType,
        byte page)
    {
        return new LegacyGenericRankingPacket(
            action,
            rankType,
            RegisteredCount: 0,
            Page: page,
            Finish: 1,
            Count: 0,
            Reserved: 0,
            Entries: []);
    }

    public static int PayloadLengthForEntryCount(int entryCount)
    {
        if (entryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(entryCount), "Legacy generic ranking entry count cannot be negative.");
        }

        return checked(EntriesOffset + (entryCount * SimpleEntryWireLength));
    }

    public static bool TryParse(PacketFrame frame, out LegacyGenericRankingPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode ||
            frame.Payload.Length < InboundHeaderLength ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var count = reader.ReadInt32(CountOffset);
        if (count < 0)
        {
            return false;
        }

        IReadOnlyList<LegacyGenericRankingEntry> entries = Array.Empty<LegacyGenericRankingEntry>();
        var expectedPayloadLength = (long)EntriesOffset + ((long)count * SimpleEntryWireLength);
        if (frame.Payload.Length == expectedPayloadLength)
        {
            var parsedEntries = new List<LegacyGenericRankingEntry>(count);
            for (var index = 0; index < count; index++)
            {
                var entryOffset = EntriesOffset + (index * SimpleEntryWireLength);
                parsedEntries.Add(new LegacyGenericRankingEntry(
                    reader.Payload[entryOffset + EntryRankOffset],
                    reader.ReadInt64(entryOffset + EntryAmountOffset),
                    reader.ReadUInt32(entryOffset + EntryUidOffset),
                    reader.ReadUInt32(entryOffset + EntryMirrorUidOffset),
                    reader.ReadCString(entryOffset + EntryNameOffset, FixedNameLength, NameEncoding),
                    reader.ReadCString(entryOffset + EntrySecondaryNameOffset, FixedNameLength, NameEncoding)));
            }

            entries = parsedEntries;
        }

        packet = new LegacyGenericRankingPacket(
            (LegacyGenericRankingAction)reader.Payload[ActionOffset],
            (LegacyGenericRankingRankType)reader.ReadUInt32(RankTypeOffset),
            reader.ReadUInt16(RegisteredCountOffset),
            reader.Payload[PageOffset],
            reader.Payload[FinishOffset],
            count,
            frame.Payload.Length >= EntriesOffset ? reader.ReadUInt32(ReservedOffset) : 0,
            entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);

        if (Count != Entries.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(Count), Count, "Legacy generic ranking count must match the entry count.");
        }

        var writer = new PacketWriter(Opcode, PayloadLengthForEntryCount(Entries.Count));
        writer.Payload[ActionOffset] = (byte)Action;
        writer.WriteUInt32(RankTypeOffset, (uint)RankType);
        writer.WriteUInt16(RegisteredCountOffset, RegisteredCount);
        writer.Payload[PageOffset] = Page;
        writer.Payload[FinishOffset] = Finish;
        writer.WriteInt32(CountOffset, Count);
        writer.WriteUInt32(ReservedOffset, Reserved);

        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            var entryOffset = EntriesOffset + (index * SimpleEntryWireLength);
            writer.Payload[entryOffset + EntryRankOffset] = entry.Rank;
            writer.WriteInt64(entryOffset + EntryAmountOffset, entry.Amount);
            writer.WriteUInt32(entryOffset + EntryUidOffset, entry.Uid);
            writer.WriteUInt32(entryOffset + EntryMirrorUidOffset, entry.MirrorUid);
            WriteFixedString(writer.Payload[(entryOffset + EntryNameOffset)..], entry.Name);
            WriteFixedString(writer.Payload[(entryOffset + EntrySecondaryNameOffset)..], entry.SecondaryName);
        }

        return writer.ToArray();
    }

    private static uint Score(LegacyChiPowerState power)
    {
        if (!power.Unlocked)
        {
            return 0;
        }

        var score = 0u;
        foreach (var attribute in power.Attributes)
        {
            var min = ChiMinValue((LegacyChiAttributeType)attribute.Type);
            var max = ChiMaxValue((LegacyChiAttributeType)attribute.Type);
            if (max <= min || attribute.Value <= min)
            {
                continue;
            }

            score += (uint)((attribute.Value - min) / ((max - min) / 100.0));
        }

        return score;
    }

    private static int ChiMaxValue(LegacyChiAttributeType attribute)
    {
        return attribute switch
        {
            LegacyChiAttributeType.CriticalStrike => 200,
            LegacyChiAttributeType.SkillCriticalStrike => 200,
            LegacyChiAttributeType.Immunity => 200,
            LegacyChiAttributeType.Breakthrough => 200,
            LegacyChiAttributeType.Counteraction => 200,
            LegacyChiAttributeType.HPAdd => 3500,
            LegacyChiAttributeType.AddAttack => 2000,
            LegacyChiAttributeType.AddMagicAttack => 2500,
            LegacyChiAttributeType.AddMagicDefense => 250,
            LegacyChiAttributeType.PhysicalDamageIncrease => 500,
            LegacyChiAttributeType.PhysicalDamageDecrease => 500,
            LegacyChiAttributeType.MagicDamageIncrease => 300,
            LegacyChiAttributeType.MagicDamageDecrease => 300,
            _ => 100,
        };
    }

    private static int ChiMinValue(LegacyChiAttributeType attribute)
    {
        return attribute switch
        {
            LegacyChiAttributeType.CriticalStrike => 10,
            LegacyChiAttributeType.SkillCriticalStrike => 10,
            LegacyChiAttributeType.Immunity => 10,
            LegacyChiAttributeType.Breakthrough => 10,
            LegacyChiAttributeType.Counteraction => 10,
            LegacyChiAttributeType.HPAdd => 1000,
            LegacyChiAttributeType.AddAttack => 500,
            LegacyChiAttributeType.AddMagicAttack => 500,
            LegacyChiAttributeType.AddMagicDefense => 50,
            LegacyChiAttributeType.PhysicalDamageIncrease => 50,
            LegacyChiAttributeType.PhysicalDamageDecrease => 50,
            LegacyChiAttributeType.MagicDamageIncrease => 50,
            LegacyChiAttributeType.MagicDamageDecrease => 50,
            _ => 10,
        };
    }

    private static void WriteFixedString(Span<byte> destination, string value)
    {
        destination[..FixedNameLength].Clear();
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var bytes = NameEncoding.GetBytes(value);
        var byteCount = Math.Min(Math.Min(value.Length, FixedNameLength), bytes.Length);
        bytes.AsSpan(0, byteCount).CopyTo(destination);
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy generic ranking UID must fit in uint.");
        }

        return (uint)value;
    }
}
