using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record VisibleDx8D13TrainingVitalityRetreatRecord(
    LegacyChiPowerType PowerType,
    uint ExpireAt,
    IReadOnlyList<int> Attributes);

public sealed record VisibleDx8D13TrainingVitalityProtectInfoPacket(
    IReadOnlyList<VisibleDx8D13TrainingVitalityRetreatRecord> Records)
{
    private const int CountOffset = 0;
    private const int PowerTypeOffset = 0;
    private const int ExpireAtOffset = PowerTypeOffset + sizeof(byte);
    private const int AttributesOffset = ExpireAtOffset + sizeof(uint);

    public const ushort Opcode = 2196;
    public const int RecordsOffset = sizeof(uint);
    public const int RecordWireLength = sizeof(byte) + sizeof(uint) +
        (LegacyChiPowerState.AttributeCount * sizeof(int));
    public const int MaximumRecordCount = 4;

    public static VisibleDx8D13TrainingVitalityProtectInfoPacket FromSnapshot(
        CharacterSnapshot character)
    {
        if (!TryFromSnapshot(character, out var packet, out var reason))
        {
            throw new InvalidDataException($"Invalid legacy Chi retreat state: {reason}.");
        }

        return packet;
    }

    public static bool TryFromSnapshot(
        CharacterSnapshot character,
        out VisibleDx8D13TrainingVitalityProtectInfoPacket packet,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(character);

        var records = new List<VisibleDx8D13TrainingVitalityRetreatRecord>(MaximumRecordCount);
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
                packet = new VisibleDx8D13TrainingVitalityProtectInfoPacket([]);
                reason = descriptor.TimeKey + "Invalid";
                return false;
            }

            if (!TryPackExpireAt(binaryTime, out var expireAt))
            {
                packet = new VisibleDx8D13TrainingVitalityProtectInfoPacket([]);
                reason = descriptor.TimeKey + "Invalid";
                return false;
            }

            if (!character.Attributes.TryGetValue(descriptor.RetreatedKey, out var rawPower) ||
                !TryParseRetreatedAttributes(rawPower, out var attributes))
            {
                packet = new VisibleDx8D13TrainingVitalityProtectInfoPacket([]);
                reason = descriptor.RetreatedKey + "Invalid";
                return false;
            }

            records.Add(new VisibleDx8D13TrainingVitalityRetreatRecord(
                descriptor.PowerType,
                expireAt,
                attributes));
        }

        packet = new VisibleDx8D13TrainingVitalityProtectInfoPacket(records);
        reason = "ready";
        return true;
    }

    public static int PayloadLengthForRecordCount(int recordCount)
    {
        if (recordCount is < 0 or > MaximumRecordCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(recordCount),
                $"D13 Chi retreat info accepts zero through {MaximumRecordCount} records.");
        }

        return checked(RecordsOffset + (recordCount * RecordWireLength));
    }

    public static bool TryParse(
        PacketFrame frame,
        out VisibleDx8D13TrainingVitalityProtectInfoPacket packet)
    {
        packet = default!;
        if (frame.Header.Opcode != Opcode || frame.Payload.Length < RecordsOffset)
        {
            return false;
        }

        var payload = frame.Payload.ToArray();
        var count = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(CountOffset, sizeof(uint)));
        if (count > MaximumRecordCount ||
            frame.Header.Length != PacketHeader.Size + PayloadLengthForRecordCount((int)count) ||
            payload.Length != PayloadLengthForRecordCount((int)count))
        {
            return false;
        }

        var records = new List<VisibleDx8D13TrainingVitalityRetreatRecord>((int)count);
        var seen = new HashSet<LegacyChiPowerType>();
        for (var index = 0; index < count; index++)
        {
            var recordOffset = RecordsOffset + ((int)index * RecordWireLength);
            var powerType = (LegacyChiPowerType)payload[recordOffset + PowerTypeOffset];
            if (powerType is < LegacyChiPowerType.Dragon or > LegacyChiPowerType.Turtle ||
                !seen.Add(powerType))
            {
                return false;
            }

            var attributes = new int[LegacyChiPowerState.AttributeCount];
            for (var attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
            {
                attributes[attributeIndex] = BinaryPrimitives.ReadInt32LittleEndian(
                    payload.AsSpan(
                        recordOffset + AttributesOffset + (attributeIndex * sizeof(int)),
                        sizeof(int)));
            }

            records.Add(new VisibleDx8D13TrainingVitalityRetreatRecord(
                powerType,
                BinaryPrimitives.ReadUInt32LittleEndian(
                    payload.AsSpan(recordOffset + ExpireAtOffset, sizeof(uint))),
                attributes));
        }

        packet = new VisibleDx8D13TrainingVitalityProtectInfoPacket(records);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Records);
        var writer = new PacketWriter(Opcode, PayloadLengthForRecordCount(Records.Count));
        writer.WriteUInt32(CountOffset, checked((uint)Records.Count));

        var seen = new HashSet<LegacyChiPowerType>();
        for (var index = 0; index < Records.Count; index++)
        {
            var record = Records[index];
            if (record.PowerType is < LegacyChiPowerType.Dragon or > LegacyChiPowerType.Turtle ||
                !seen.Add(record.PowerType))
            {
                throw new ArgumentOutOfRangeException(nameof(Records), "D13 Chi retreat modes must be unique values 1 through 4.");
            }

            if (record.Attributes.Count != LegacyChiPowerState.AttributeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(Records), "D13 Chi retreat records require exactly four attributes.");
            }

            var recordOffset = RecordsOffset + (index * RecordWireLength);
            writer.Payload[recordOffset + PowerTypeOffset] = (byte)record.PowerType;
            writer.WriteUInt32(recordOffset + ExpireAtOffset, record.ExpireAt);
            for (var attributeIndex = 0; attributeIndex < record.Attributes.Count; attributeIndex++)
            {
                writer.WriteInt32(
                    recordOffset + AttributesOffset + (attributeIndex * sizeof(int)),
                    record.Attributes[attributeIndex]);
            }
        }

        return writer.ToArray();
    }

    private static bool TryPackExpireAt(long binaryTime, out uint expireAt)
    {
        if (binaryTime == 1)
        {
            expireAt = 0;
            return true;
        }

        try
        {
            var time = DateTime.FromBinary(binaryTime);
            var packed = ((long)(time.Year % 100) * 100_000_000L) +
                ((long)time.Month * 1_000_000L) +
                ((long)time.Day * 10_000L) +
                ((long)time.Hour * 100L) +
                time.Minute;
            expireAt = unchecked((uint)packed);
            return true;
        }
        catch (ArgumentException)
        {
            expireAt = 0;
            return false;
        }
    }

    private static bool TryParseRetreatedAttributes(string? rawPower, out IReadOnlyList<int> attributes)
    {
        if (string.IsNullOrWhiteSpace(rawPower))
        {
            attributes = [];
            return false;
        }

        var entries = rawPower.Split('|', StringSplitOptions.None);
        if (entries.Length != LegacyChiPowerState.AttributeCount)
        {
            attributes = [];
            return false;
        }

        var parsed = new int[LegacyChiPowerState.AttributeCount];
        for (var index = 0; index < entries.Length; index++)
        {
            var fields = entries[index].Split('-', StringSplitOptions.None);
            if (fields.Length != 2 ||
                !byte.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var type) ||
                !ushort.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                attributes = [];
                return false;
            }

            parsed[index] = (type * 10_000) + value;
        }

        attributes = parsed;
        return true;
    }

    private static readonly (LegacyChiPowerType PowerType, string TimeKey, string RetreatedKey)[]
        RetreatDescriptors =
        [
            (LegacyChiPowerType.Dragon, "DragonTime", "RetreatedDragon"),
            (LegacyChiPowerType.Phoenix, "PhoenixTime", "RetreatedPhoenix"),
            (LegacyChiPowerType.Tiger, "TigerTime", "RetreatedTiger"),
            (LegacyChiPowerType.Turtle, "TurtleTime", "RetreatedTurtle"),
        ];
}
