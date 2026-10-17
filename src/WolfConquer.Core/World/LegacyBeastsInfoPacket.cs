using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyBeastsInfoPacket(
    uint Uid,
    byte Level,
    uint Points,
    uint FixedLevel,
    uint Unknown2,
    bool Activated,
    uint Flag)
{
    private const int UidField = 1;
    private const int LevelField = 2;
    private const int PointsField = 3;
    private const int FixedLevelField = 4;
    private const int Unknown2Field = 5;
    private const int ActivatedField = 6;
    private const int FlagField = 7;
    private const int VarintWireType = 0;
    private const int LegacyBeastAttributeCount = 100;
    private const uint LegacyDefaultFixedLevel = 1;
    private const uint LegacyUnknown2 = 1;
    private const uint LegacyLastRequiredPoints = 48_000;

    public const ushort Opcode = WorldPacketOpcodes.LegacyBeastsInfo;

    public static bool TryCreateStartup(CharacterSnapshot snapshot, out LegacyBeastsInfoPacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var outboundActivated = TryReadBool(snapshot.Attributes, "TailedBeastActivated", out var activated) && activated;
        return TryCreateStartup(snapshot, outboundActivated, out packet);
    }

    public static bool TryCreateStartup(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        out LegacyBeastsInfoPacket packet)
    {
        return TryCreateStartup(snapshot, playerItems, alternateEquipmentActive: false, out packet);
    }

    public static bool TryCreateStartup(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive,
        out LegacyBeastsInfoPacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(playerItems);

        return TryCreateStartup(snapshot, HasActivatedRuneSlots(playerItems, alternateEquipmentActive), out packet);
    }

    public static bool TryParse(PacketFrame frame, out LegacyBeastsInfoPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        uint uid = 0;
        byte level = 0;
        uint points = 0;
        uint fixedLevel = 0;
        uint unknown2 = 0;
        var activated = false;
        uint flag = 0;
        var hasUid = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case UidField:
                    if (!TryToUInt32(value, out uid))
                    {
                        return false;
                    }

                    hasUid = true;
                    break;
                case LevelField:
                    if (!TryToByte(value, out level))
                    {
                        return false;
                    }

                    break;
                case PointsField:
                    if (!TryToUInt32(value, out points))
                    {
                        return false;
                    }

                    break;
                case FixedLevelField:
                    if (!TryToUInt32(value, out fixedLevel))
                    {
                        return false;
                    }

                    break;
                case Unknown2Field:
                    if (!TryToUInt32(value, out unknown2))
                    {
                        return false;
                    }

                    break;
                case ActivatedField:
                    if (!TryToBool(value, out activated))
                    {
                        return false;
                    }

                    break;
                case FlagField:
                    if (!TryToUInt32(value, out flag))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        if (!hasUid)
        {
            return false;
        }

        packet = new LegacyBeastsInfoPacket(uid, level, points, fixedLevel, unknown2, activated, flag);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, UidField, Uid);
        WriteOptionalVarintField(payload, LevelField, Level);
        WriteOptionalVarintField(payload, PointsField, Points);
        WriteOptionalVarintField(payload, FixedLevelField, FixedLevel);
        WriteOptionalVarintField(payload, Unknown2Field, Unknown2);
        WriteOptionalBoolField(payload, ActivatedField, Activated);
        WriteOptionalVarintField(payload, FlagField, Flag);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryCreateStartup(
        CharacterSnapshot snapshot,
        bool outboundActivated,
        out LegacyBeastsInfoPacket packet)
    {
        packet = default!;

        var hasTotalPoints = TryReadUInt32(snapshot.Attributes, "TailedBeastTotalPoints", out var totalPoints);
        TryReadUInt32(snapshot.Attributes, "TailedBeastFlag", out var flag);
        var persistedActivated = TryReadBool(snapshot.Attributes, "TailedBeastActivated", out var parsedActivated) &&
            parsedActivated;
        var progress = hasTotalPoints
            ? ComputeProgress(totalPoints, persistedActivated)
            : DefaultProgress(snapshot, persistedActivated || outboundActivated);

        packet = new LegacyBeastsInfoPacket(
            CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            progress.Level,
            progress.Points,
            progress.FixedLevel,
            LegacyUnknown2,
            outboundActivated,
            flag);
        return true;
    }

    private static LegacyBeastProgress ComputeProgress(uint totalPoints, bool activated)
    {
        var level = activated ? 1 : 0;
        var fixedLevel = LegacyDefaultFixedLevel;
        var remainingPoints = totalPoints;

        for (var index = 0; index < LegacyBeastAttributeCount; index++)
        {
            var requiredPoints = GetRequiredPoints((byte)(index + 1));
            if (remainingPoints < requiredPoints)
            {
                break;
            }

            remainingPoints -= requiredPoints;
            level++;
            while (level > LegacyBeastAttributeCount)
            {
                level--;
                fixedLevel++;
            }
        }

        while (remainingPoints >= LegacyLastRequiredPoints)
        {
            remainingPoints -= LegacyLastRequiredPoints;
            fixedLevel++;
        }

        return new LegacyBeastProgress((byte)level, remainingPoints, fixedLevel);
    }

    private static LegacyBeastProgress DefaultProgress(CharacterSnapshot snapshot, bool activated)
    {
        var level = TryReadByte(
            snapshot.Attributes,
            out var parsedLevel,
            "TailedBeastLevel",
            "BeastLevel",
            "BeastsLevel")
            ? parsedLevel
            : (byte)(activated ? 1 : 0);

        return new LegacyBeastProgress(level, 0, LegacyDefaultFixedLevel);
    }

    private static uint GetRequiredPoints(byte level)
    {
        return level switch
        {
            >= 1 and <= 5 => 2_400,
            >= 6 and <= 10 => 4_800,
            >= 11 and <= 19 => 7_200,
            >= 20 and <= 29 => 12_000,
            >= 30 and <= 39 => 19_200,
            >= 40 and <= 50 => 24_000,
            >= 51 and <= 60 => 28_800,
            >= 61 and <= 80 => 33_600,
            >= 81 and <= 90 => 36_000,
            >= 91 and <= 100 => 48_000,
            _ => 0,
        };
    }

    private static bool HasActivatedRuneSlots(PlayerItemSnapshot playerItems, bool alternateEquipmentActive)
    {
        var start = alternateEquipmentActive ? 121 : 101;
        var end = alternateEquipmentActive ? 135 : 115;
        return playerItems.RuneItems.Count(item => item.Position >= start && item.Position <= end) >= 5;
    }

    private static bool TryReadUInt32(IReadOnlyDictionary<string, string> attributes, string key, out uint value)
    {
        value = 0;
        return attributes.TryGetValue(key, out var raw) &&
            uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadByte(
        IReadOnlyDictionary<string, string> attributes,
        out byte value,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var raw) &&
                byte.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0;
        return false;
    }

    private static bool TryReadBool(IReadOnlyDictionary<string, string> attributes, string key, out bool value)
    {
        value = false;
        if (!attributes.TryGetValue(key, out var raw))
        {
            return false;
        }

        if (bool.TryParse(raw, out value))
        {
            return true;
        }

        if (uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            value = number != 0;
            return true;
        }

        return false;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Value must fit UInt32.");
        }

        return (uint)value;
    }

    private static bool TryToUInt32(ulong value, out uint result)
    {
        if (value > uint.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }

    private static bool TryToByte(ulong value, out byte result)
    {
        if (value > byte.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (byte)value;
        return true;
    }

    private static bool TryToBool(ulong value, out bool result)
    {
        if (value > 1)
        {
            result = false;
            return false;
        }

        result = value != 0;
        return true;
    }

    private static void WriteOptionalVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        if (value != 0)
        {
            WriteVarintField(payload, fieldNumber, value);
        }
    }

    private static void WriteOptionalBoolField(List<byte> payload, int fieldNumber, bool value)
    {
        if (value)
        {
            WriteVarintField(payload, fieldNumber, 1);
        }
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

    private readonly record struct LegacyBeastProgress(byte Level, uint Points, uint FixedLevel);

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
