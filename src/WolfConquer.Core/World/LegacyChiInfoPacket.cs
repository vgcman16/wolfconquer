using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyChiInfoAction : ushort
{
    Send = 0,
    Upgrade = 1,
    InterServerStatus = 2,
}

public enum LegacyChiPowerType : byte
{
    None = 0,
    Dragon = 1,
    Phoenix = 2,
    Tiger = 3,
    Turtle = 4,
}

public enum LegacyChiAttributeType : byte
{
    None = 0,
    CriticalStrike = 1,
    SkillCriticalStrike = 2,
    Immunity = 3,
    Breakthrough = 4,
    Counteraction = 5,
    HPAdd = 6,
    AddAttack = 7,
    AddMagicAttack = 8,
    AddMagicDefense = 9,
    PhysicalDamageIncrease = 10,
    MagicDamageIncrease = 11,
    PhysicalDamageDecrease = 12,
    MagicDamageDecrease = 13,
    LukyStrike = 14,
}

public sealed record LegacyChiState(int ChiPoints, IReadOnlyList<LegacyChiPowerState> Powers)
{
    public static LegacyChiState Empty { get; } = new(0, []);

    public static LegacyChiState FromSnapshot(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return FromAttributes(snapshot.Attributes);
    }

    public static LegacyChiState FromAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        var chiPoints = TryGetInt32(attributes, "ChiPoints", out var parsedPoints) ? parsedPoints : 0;
        var powers = new List<LegacyChiPowerState>(4);
        AddPower(attributes, powers, LegacyChiPowerType.Dragon, "Dragon");
        AddPower(attributes, powers, LegacyChiPowerType.Phoenix, "Pheonix", "Phoenix");
        AddPower(attributes, powers, LegacyChiPowerType.Tiger, "Tiger");
        AddPower(attributes, powers, LegacyChiPowerType.Turtle, "Turtle");

        return new LegacyChiState(chiPoints, powers);
    }

    private static void AddPower(
        IReadOnlyDictionary<string, string> attributes,
        ICollection<LegacyChiPowerState> powers,
        LegacyChiPowerType type,
        params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!attributes.TryGetValue(key, out var rawPower))
            {
                continue;
            }

            var power = LegacyChiPowerState.Parse(type, rawPower);
            if (power.Unlocked)
            {
                powers.Add(power);
            }

            return;
        }
    }

    private static bool TryGetInt32(IReadOnlyDictionary<string, string> attributes, string key, out int value)
    {
        if (attributes.TryGetValue(key, out var rawValue) &&
            int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = 0;
        return false;
    }
}

public sealed record LegacyChiPowerState(
    LegacyChiPowerType Type,
    bool Unlocked,
    uint Experience,
    IReadOnlyList<LegacyChiAttributeState> Attributes)
{
    public const int AttributeCount = 4;

    public static LegacyChiPowerState Parse(LegacyChiPowerType type, string? rawPower)
    {
        if (string.IsNullOrEmpty(rawPower) ||
            !rawPower.Contains('/', StringComparison.Ordinal))
        {
            return Locked(type);
        }

        var fields = rawPower.Split('/');
        var offset = 0;
        var unlocked = ReadByte(fields, ref offset) == 1;
        if (!unlocked)
        {
            return Locked(type);
        }

        var experience = ReadUInt32(fields, ref offset);
        var attributes = new LegacyChiAttributeState[AttributeCount];
        for (var index = 0; index < attributes.Length; index++)
        {
            attributes[index] = new LegacyChiAttributeState(
                ReadByte(fields, ref offset),
                ReadUInt16(fields, ref offset),
                Locked: ReadByte(fields, ref offset) > 0);
        }

        return new LegacyChiPowerState(type, Unlocked: true, experience, attributes);
    }

    private static LegacyChiPowerState Locked(LegacyChiPowerType type)
    {
        return new LegacyChiPowerState(type, Unlocked: false, 0, CreateZeroAttributes());
    }

    private static LegacyChiAttributeState[] CreateZeroAttributes()
    {
        var attributes = new LegacyChiAttributeState[AttributeCount];
        for (var index = 0; index < attributes.Length; index++)
        {
            attributes[index] = new LegacyChiAttributeState(0, 0, Locked: false);
        }

        return attributes;
    }

    private static byte ReadByte(IReadOnlyList<string> fields, ref int offset)
    {
        if (offset < fields.Count &&
            byte.TryParse(fields[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            offset++;
            return value;
        }

        offset++;
        return 0;
    }

    private static ushort ReadUInt16(IReadOnlyList<string> fields, ref int offset)
    {
        if (offset < fields.Count &&
            ushort.TryParse(fields[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            offset++;
            return value;
        }

        offset++;
        return 0;
    }

    private static uint ReadUInt32(IReadOnlyList<string> fields, ref int offset)
    {
        if (offset < fields.Count &&
            uint.TryParse(fields[offset], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            offset++;
            return value;
        }

        offset++;
        return 0;
    }
}

public readonly record struct LegacyChiAttributeState(byte Type, ushort Value, bool Locked)
{
    public int ToLegacyWireValue()
    {
        return (Type * 10000) + Value;
    }
}

public sealed record LegacyChiInfoPacket(
    LegacyChiInfoAction Action,
    uint Uid,
    int ChiPoints,
    uint LockedFlags,
    IReadOnlyList<LegacyChiPowerState> Powers)
{
    private const int ActionOffset = 0;
    private const int UidOffset = ActionOffset + sizeof(ushort);
    private const int ChiPointsOffset = UidOffset + sizeof(uint);
    private const int LockedFlagsOffset = ChiPointsOffset + sizeof(int);
    private const int CountOffset = LockedFlagsOffset + sizeof(uint);
    private const int PowerTypeLength = sizeof(byte);
    private const int PowerAttributesOffset = PowerTypeLength;
    private const int PowerExperienceOffset = PowerAttributesOffset + (LegacyChiPowerState.AttributeCount * sizeof(int));
    private const int PowerReservedOffset = PowerExperienceOffset + sizeof(ulong);

    public const ushort Opcode = WorldPacketOpcodes.LegacyChiInfo;
    public const int PowersOffset = CountOffset + sizeof(uint);
    public const int PowerWireLength = PowerReservedOffset + sizeof(byte);

    public static LegacyChiInfoPacket Startup(long uid, LegacyChiState state)
    {
        return Create(uid, state, LegacyChiInfoAction.Upgrade);
    }

    public static LegacyChiInfoPacket Create(
        long uid,
        LegacyChiState state,
        LegacyChiInfoAction action)
    {
        ArgumentNullException.ThrowIfNull(state);

        var powers = state.Powers.Where(power => power.Unlocked).ToArray();
        return new LegacyChiInfoPacket(
            action,
            CheckedUInt32(uid, nameof(uid)),
            state.ChiPoints,
            ComputeLockedFlags(powers),
            powers);
    }

    public static LegacyChiInfoPacket FromSnapshot(CharacterSnapshot snapshot)
    {
        return FromSnapshot(snapshot, LegacyChiInfoAction.Upgrade);
    }

    public static LegacyChiInfoPacket FromSnapshot(
        CharacterSnapshot snapshot,
        LegacyChiInfoAction action)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return Create(snapshot.Uid, LegacyChiState.FromSnapshot(snapshot), action);
    }

    public static int PayloadLengthForPowerCount(int powerCount)
    {
        if (powerCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(powerCount), "Legacy Chi power count cannot be negative.");
        }

        return checked(PowersOffset + (powerCount * PowerWireLength));
    }

    public static bool TryParse(PacketFrame frame, out LegacyChiInfoPacket packet)
    {
        return TryParse(frame, Opcode, out packet);
    }

    internal static bool TryParse(
        PacketFrame frame,
        ushort expectedOpcode,
        out LegacyChiInfoPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != expectedOpcode ||
            frame.Payload.Length < PowersOffset ||
            !PacketReader.TryRead(frame.Frame.ToArray(), out var reader))
        {
            return false;
        }

        var count = reader.ReadUInt32(CountOffset);
        if (count > int.MaxValue ||
            frame.Payload.Length != PayloadLengthForPowerCount((int)count))
        {
            return false;
        }

        var lockedFlags = reader.ReadUInt32(LockedFlagsOffset);
        var powers = new List<LegacyChiPowerState>((int)count);
        for (var index = 0; index < count; index++)
        {
            var powerOffset = PowersOffset + ((int)index * PowerWireLength);
            var type = (LegacyChiPowerType)reader.Payload[powerOffset];
            var attributes = new LegacyChiAttributeState[LegacyChiPowerState.AttributeCount];
            for (var attributeIndex = 0; attributeIndex < attributes.Length; attributeIndex++)
            {
                var wireValue = reader.ReadInt32(powerOffset + PowerAttributesOffset + (attributeIndex * sizeof(int)));
                attributes[attributeIndex] = new LegacyChiAttributeState(
                    (byte)(wireValue / 10000),
                    checked((ushort)(wireValue % 10000)),
                    Locked: (lockedFlags & (1u << (attributeIndex + ((int)index * LegacyChiPowerState.AttributeCount)))) != 0);
            }

            var experience = reader.ReadUInt64(powerOffset + PowerExperienceOffset);
            if (experience > uint.MaxValue || reader.Payload[powerOffset + PowerReservedOffset] != 0)
            {
                return false;
            }

            powers.Add(new LegacyChiPowerState(type, Unlocked: true, (uint)experience, attributes));
        }

        packet = new LegacyChiInfoPacket(
            (LegacyChiInfoAction)reader.ReadUInt16(ActionOffset),
            reader.ReadUInt32(UidOffset),
            reader.ReadInt32(ChiPointsOffset),
            lockedFlags,
            powers);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        return ToFrameBytes(Opcode);
    }

    internal byte[] ToFrameBytes(ushort opcode)
    {
        ArgumentNullException.ThrowIfNull(Powers);

        var writer = new PacketWriter(opcode, PayloadLengthForPowerCount(Powers.Count));
        writer.WriteUInt16(ActionOffset, (ushort)Action);
        writer.WriteUInt32(UidOffset, Uid);
        writer.WriteInt32(ChiPointsOffset, ChiPoints);
        writer.WriteUInt32(LockedFlagsOffset, LockedFlags);
        writer.WriteUInt32(CountOffset, checked((uint)Powers.Count));

        for (var index = 0; index < Powers.Count; index++)
        {
            var power = Powers[index];
            if (power.Attributes.Count != LegacyChiPowerState.AttributeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(Powers), "Legacy Chi powers require exactly four attributes.");
            }

            var powerOffset = PowersOffset + (index * PowerWireLength);
            writer.Payload[powerOffset] = (byte)power.Type;
            for (var attributeIndex = 0; attributeIndex < power.Attributes.Count; attributeIndex++)
            {
                writer.WriteInt32(
                    powerOffset + PowerAttributesOffset + (attributeIndex * sizeof(int)),
                    power.Attributes[attributeIndex].ToLegacyWireValue());
            }

            writer.WriteUInt64(powerOffset + PowerExperienceOffset, power.Experience);
            writer.Payload[powerOffset + PowerReservedOffset] = 0;
        }

        return writer.ToArray();
    }

    private static uint ComputeLockedFlags(IReadOnlyList<LegacyChiPowerState> powers)
    {
        var flags = 0u;
        for (var powerIndex = 0; powerIndex < powers.Count; powerIndex++)
        {
            var power = powers[powerIndex];
            for (var attributeIndex = 0; attributeIndex < power.Attributes.Count; attributeIndex++)
            {
                if (power.Attributes[attributeIndex].Locked)
                {
                    flags |= 1u << (attributeIndex + (powerIndex * LegacyChiPowerState.AttributeCount));
                }
            }
        }

        return flags;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy Chi UID must fit in uint.");
        }

        return (uint)value;
    }
}
