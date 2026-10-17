using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public readonly record struct VisibleDx8D13TrainingVitalityExpiryNotifyEntry(
    LegacyChiPowerType PowerType,
    byte State);

public sealed record VisibleDx8D13TrainingVitalityExpiryNotifyPacket(
    IReadOnlyList<VisibleDx8D13TrainingVitalityExpiryNotifyEntry> Entries)
{
    private const int CountOffset = 0;
    private const int PowerTypeOffset = 0;
    private const int StateOffset = PowerTypeOffset + sizeof(byte);

    public const ushort Opcode = 2268;
    public const byte ExpiredState = 1;
    public const int EntriesOffset = sizeof(uint);
    public const int EntryWireLength = sizeof(byte) + sizeof(byte);
    public const int MaximumEntryCount = 4;

    public static bool TryCreateFromSnapshot(
        CharacterSnapshot character,
        DateTime now,
        out VisibleDx8D13TrainingVitalityExpiryNotifyPacket packet,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(character);
        packet = new VisibleDx8D13TrainingVitalityExpiryNotifyPacket([]);

        if (!TryReadVipLevel(character.Attributes, out var vipLevel))
        {
            reason = "vipLevelInvalid";
            return false;
        }

        if (vipLevel < 4)
        {
            reason = "vipLevelBelowFour";
            return false;
        }

        var entries = new List<VisibleDx8D13TrainingVitalityExpiryNotifyEntry>(MaximumEntryCount);
        foreach (var descriptor in RetreatDescriptors)
        {
            if (!character.Attributes.TryGetValue(descriptor.TimeKey, out var rawTime) ||
                string.IsNullOrWhiteSpace(rawTime) ||
                string.Equals(rawTime, "0", StringComparison.Ordinal))
            {
                continue;
            }

            if (!long.TryParse(rawTime, NumberStyles.Integer, CultureInfo.InvariantCulture, out var binaryTime))
            {
                reason = descriptor.TimeKey + "Invalid";
                return false;
            }

            // The source uses binary value 1 for the non-expiring retreat item.
            if (binaryTime == 1)
            {
                continue;
            }

            DateTime expiresAt;
            try
            {
                expiresAt = DateTime.FromBinary(binaryTime);
            }
            catch (ArgumentException)
            {
                reason = descriptor.TimeKey + "Invalid";
                return false;
            }

            if (now > expiresAt)
            {
                entries.Add(new VisibleDx8D13TrainingVitalityExpiryNotifyEntry(
                    descriptor.PowerType,
                    ExpiredState));
            }
        }

        if (entries.Count == 0)
        {
            reason = "noExpiredRetreats";
            return false;
        }

        packet = new VisibleDx8D13TrainingVitalityExpiryNotifyPacket(entries);
        reason = "ready";
        return true;
    }

    public static int PayloadLengthForEntryCount(int entryCount)
    {
        if (entryCount is < 0 or > MaximumEntryCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entryCount),
                $"D13 Chi expiry notify accepts zero through {MaximumEntryCount} entries.");
        }

        return checked(EntriesOffset + (entryCount * EntryWireLength));
    }

    public static bool TryParse(
        PacketFrame frame,
        out VisibleDx8D13TrainingVitalityExpiryNotifyPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode || frame.Payload.Length < EntriesOffset)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var count = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(CountOffset, sizeof(uint)));
        if (count > MaximumEntryCount ||
            frame.Header.Length != PacketHeader.Size + PayloadLengthForEntryCount((int)count) ||
            payload.Length != PayloadLengthForEntryCount((int)count))
        {
            return false;
        }

        var entries = new List<VisibleDx8D13TrainingVitalityExpiryNotifyEntry>((int)count);
        var seen = new HashSet<LegacyChiPowerType>();
        for (var index = 0; index < count; index++)
        {
            var entryOffset = EntriesOffset + ((int)index * EntryWireLength);
            var powerType = (LegacyChiPowerType)payload[entryOffset + PowerTypeOffset];
            var state = payload[entryOffset + StateOffset];
            if (powerType is < LegacyChiPowerType.Dragon or > LegacyChiPowerType.Turtle ||
                state != ExpiredState ||
                !seen.Add(powerType))
            {
                return false;
            }

            entries.Add(new VisibleDx8D13TrainingVitalityExpiryNotifyEntry(powerType, state));
        }

        packet = new VisibleDx8D13TrainingVitalityExpiryNotifyPacket(entries);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Entries);
        var writer = new PacketWriter(Opcode, PayloadLengthForEntryCount(Entries.Count));
        writer.WriteUInt32(CountOffset, checked((uint)Entries.Count));

        var seen = new HashSet<LegacyChiPowerType>();
        for (var index = 0; index < Entries.Count; index++)
        {
            var entry = Entries[index];
            if (entry.PowerType is < LegacyChiPowerType.Dragon or > LegacyChiPowerType.Turtle ||
                entry.State != ExpiredState ||
                !seen.Add(entry.PowerType))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Entries),
                    "D13 Chi expiry entries must be unique powers 1 through 4 in state 1.");
            }

            var entryOffset = EntriesOffset + (index * EntryWireLength);
            writer.Payload[entryOffset + PowerTypeOffset] = (byte)entry.PowerType;
            writer.Payload[entryOffset + StateOffset] = entry.State;
        }

        return writer.ToArray();
    }

    private static bool TryReadVipLevel(
        IReadOnlyDictionary<string, string> attributes,
        out ulong vipLevel)
    {
        foreach (var key in new[] { "VipLevel", "VIPLevel" })
        {
            if (!attributes.TryGetValue(key, out var rawValue))
            {
                continue;
            }

            return ulong.TryParse(
                rawValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out vipLevel);
        }

        vipLevel = 0;
        return true;
    }

    private static readonly (LegacyChiPowerType PowerType, string TimeKey)[] RetreatDescriptors =
    [
        (LegacyChiPowerType.Dragon, "DragonTime"),
        (LegacyChiPowerType.Phoenix, "PhoenixTime"),
        (LegacyChiPowerType.Tiger, "TigerTime"),
        (LegacyChiPowerType.Turtle, "TurtleTime"),
    ];
}
