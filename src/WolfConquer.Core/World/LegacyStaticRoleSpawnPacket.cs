using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed record LegacyStaticRoleSpawnPacket
{
    private const int MeshField = 1;
    private const int UidField = 2;
    private const int GuildIdField = 3;
    private const int GuildRankField = 4;
    private const int UnknownField = 5;
    private const int StatusFlagsField = 6;
    private const int HeadField = 8;
    private const int GarmentField = 9;
    private const int ArmorField = 10;
    private const int LeftWeaponField = 11;
    private const int RightWeaponField = 12;
    private const int MonsterLevelField = 26;
    private const int XField = 27;
    private const int YField = 28;
    private const int HairStyleField = 29;
    private const int FacingField = 30;
    private const int ActionField = 31;
    private const int RebornField = 34;
    private const int LevelField = 35;
    private const int WindowSpawnField = 36;
    private const int AwayField = 37;
    private const int ExtraBattlePowerField = 38;
    private const int FlowerIconField = 42;
    private const int NobilityRankField = 43;
    private const int BossField = 63;
    private const int HeadSoulField = 64;
    private const int ArmorSoulField = 65;
    private const int LeftWeaponSoulField = 66;
    private const int RightWeaponSoulField = 67;
    private const int SubClassField = 68;
    private const int ActiveSubClassesField = 69;
    private const int ServerIdField = 79;
    private const int NamesField = 107;
    private const int UnknownBoolField = 108;
    private const int PrestigeRankField = 110;
    private const int MonsterIdField = 111;
    private const int Test1Field = 112;
    private const int Test2Field = 113;
    private const int CollectionIdField = 121;
    private const int StatusFlagWords = 10;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    private static readonly int[] RequiredZeroFields =
    [
        GuildIdField,
        GuildRankField,
        UnknownField,
        HeadField,
        GarmentField,
        ArmorField,
        LeftWeaponField,
        RightWeaponField,
        HairStyleField,
        FacingField,
        RebornField,
        WindowSpawnField,
        AwayField,
        ExtraBattlePowerField,
        FlowerIconField,
        NobilityRankField,
        BossField,
        HeadSoulField,
        ArmorSoulField,
        LeftWeaponSoulField,
        RightWeaponSoulField,
        SubClassField,
        ActiveSubClassesField,
        ServerIdField,
        UnknownBoolField,
        PrestigeRankField,
        MonsterIdField,
        Test1Field,
        Test2Field,
        CollectionIdField,
    ];

    public const ushort Opcode = WorldPacketOpcodes.LegacyRoleSpawn;

    public LegacyStaticRoleSpawnPacket(
        uint uid,
        uint mesh,
        byte monsterLevel,
        ushort x,
        ushort y,
        ushort action,
        ushort level,
        string name,
        IReadOnlyList<ulong>? statusFlags = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        Uid = uid;
        Mesh = mesh;
        MonsterLevel = monsterLevel;
        X = x;
        Y = y;
        Action = action;
        Level = level;
        Name = name;
        StatusFlags = CopyStatusFlags(statusFlags);
    }

    public uint Uid { get; }

    public uint Mesh { get; }

    public byte MonsterLevel { get; }

    public ushort X { get; }

    public ushort Y { get; }

    public ushort Action { get; }

    public ushort Level { get; }

    public string Name { get; }

    public IReadOnlyList<ulong> StatusFlags { get; }

    public static LegacyStaticRoleSpawnPacket FromStaticRole(
        ushort x,
        ushort y,
        string name = "Flag",
        uint mesh = 8910,
        byte level = 1,
        ushort action = 0,
        IReadOnlyList<ulong>? statusFlags = null)
    {
        return new LegacyStaticRoleSpawnPacket(
            (uint)(x * 1000 + y),
            mesh,
            level,
            x,
            y,
            action,
            level,
            name,
            statusFlags);
    }

    public static bool TryParse(PacketFrame frame, out LegacyStaticRoleSpawnPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        uint uid = 0;
        uint mesh = 0;
        byte monsterLevel = 0;
        ushort x = 0;
        ushort y = 0;
        ushort action = 0;
        ushort level = 0;
        var statusFlags = new List<ulong>();
        var names = new List<string>();
        var seenRequiredZeroFields = new HashSet<int>();
        var hasUid = false;
        var hasMesh = false;
        var hasMonsterLevel = false;
        var hasX = false;
        var hasY = false;
        var hasAction = false;
        var hasLevel = false;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (fieldNumber == NamesField)
            {
                if (wireType != LengthDelimitedWireType ||
                    !reader.TryReadString(out var name))
                {
                    return false;
                }

                names.Add(name);
                continue;
            }

            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var value))
            {
                return false;
            }

            switch (fieldNumber)
            {
                case MeshField:
                    if (!TryToUInt32(value, out mesh))
                    {
                        return false;
                    }

                    hasMesh = true;
                    break;
                case UidField:
                    if (!TryToUInt32(value, out uid))
                    {
                        return false;
                    }

                    hasUid = true;
                    break;
                case StatusFlagsField:
                    statusFlags.Add(value);
                    break;
                case MonsterLevelField:
                    if (!TryToByte(value, out monsterLevel))
                    {
                        return false;
                    }

                    hasMonsterLevel = true;
                    break;
                case XField:
                    if (!TryToUInt16(value, out x))
                    {
                        return false;
                    }

                    hasX = true;
                    break;
                case YField:
                    if (!TryToUInt16(value, out y))
                    {
                        return false;
                    }

                    hasY = true;
                    break;
                case ActionField:
                    if (!TryToUInt16(value, out action))
                    {
                        return false;
                    }

                    hasAction = true;
                    break;
                case LevelField:
                    if (!TryToUInt16(value, out level))
                    {
                        return false;
                    }

                    hasLevel = true;
                    break;
                default:
                    if (!IsRequiredZeroField(fieldNumber) || value != 0)
                    {
                        return false;
                    }

                    seenRequiredZeroFields.Add(fieldNumber);
                    break;
            }
        }

        if (!hasUid ||
            !hasMesh ||
            !hasMonsterLevel ||
            !hasX ||
            !hasY ||
            !hasAction ||
            !hasLevel ||
            statusFlags.Count != StatusFlagWords ||
            names.Count != 2 ||
            names[1].Length != 0)
        {
            return false;
        }

        foreach (var fieldNumber in RequiredZeroFields)
        {
            if (!seenRequiredZeroFields.Contains(fieldNumber))
            {
                return false;
            }
        }

        packet = new LegacyStaticRoleSpawnPacket(
            uid,
            mesh,
            monsterLevel,
            x,
            y,
            action,
            level,
            names[0],
            statusFlags);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, MeshField, Mesh);
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, GuildIdField, 0);
        WriteVarintField(payload, GuildRankField, 0);
        WriteVarintField(payload, UnknownField, 0);

        foreach (var statusFlag in StatusFlags)
        {
            WriteVarintField(payload, StatusFlagsField, statusFlag);
        }

        WriteVarintField(payload, HeadField, 0);
        WriteVarintField(payload, GarmentField, 0);
        WriteVarintField(payload, ArmorField, 0);
        WriteVarintField(payload, LeftWeaponField, 0);
        WriteVarintField(payload, RightWeaponField, 0);
        WriteVarintField(payload, MonsterLevelField, MonsterLevel);
        WriteVarintField(payload, XField, X);
        WriteVarintField(payload, YField, Y);
        WriteVarintField(payload, HairStyleField, 0);
        WriteVarintField(payload, FacingField, 0);
        WriteVarintField(payload, ActionField, Action);
        WriteVarintField(payload, RebornField, 0);
        WriteVarintField(payload, LevelField, Level);
        WriteVarintField(payload, WindowSpawnField, 0);
        WriteVarintField(payload, AwayField, 0);
        WriteVarintField(payload, ExtraBattlePowerField, 0);
        WriteVarintField(payload, FlowerIconField, 0);
        WriteVarintField(payload, NobilityRankField, 0);
        WriteVarintField(payload, BossField, 0);
        WriteVarintField(payload, HeadSoulField, 0);
        WriteVarintField(payload, ArmorSoulField, 0);
        WriteVarintField(payload, LeftWeaponSoulField, 0);
        WriteVarintField(payload, RightWeaponSoulField, 0);
        WriteVarintField(payload, SubClassField, 0);
        WriteVarintField(payload, ActiveSubClassesField, 0);
        WriteVarintField(payload, ServerIdField, 0);
        WriteString(payload, NamesField, Name);
        WriteString(payload, NamesField, string.Empty);
        WriteVarintField(payload, UnknownBoolField, 0);
        WriteVarintField(payload, PrestigeRankField, 0);
        WriteVarintField(payload, MonsterIdField, 0);
        WriteVarintField(payload, Test1Field, 0);
        WriteVarintField(payload, Test2Field, 0);
        WriteVarintField(payload, CollectionIdField, 0);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static ulong[] CopyStatusFlags(IReadOnlyList<ulong>? statusFlags)
    {
        if (statusFlags is null)
        {
            return new ulong[StatusFlagWords];
        }

        if (statusFlags.Count != StatusFlagWords)
        {
            throw new ArgumentException("Legacy static role spawn expects exactly ten status flag words.", nameof(statusFlags));
        }

        var copy = new ulong[StatusFlagWords];
        for (var i = 0; i < copy.Length; i++)
        {
            copy[i] = statusFlags[i];
        }

        return copy;
    }

    private static bool IsRequiredZeroField(int fieldNumber)
    {
        foreach (var requiredZeroField in RequiredZeroFields)
        {
            if (fieldNumber == requiredZeroField)
            {
                return true;
            }
        }

        return false;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        WriteVarint(payload, ((ulong)fieldNumber << 3) | VarintWireType);
        WriteVarint(payload, value);
    }

    private static void WriteString(List<byte> payload, int fieldNumber, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        WriteVarint(payload, ((ulong)fieldNumber << 3) | LengthDelimitedWireType);
        WriteVarint(payload, (ulong)bytes.Length);
        payload.AddRange(bytes);
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

    private static bool TryToUInt16(ulong value, out ushort result)
    {
        if (value > ushort.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (ushort)value;
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

        public bool TryReadString(out string value)
        {
            value = string.Empty;

            if (!TryReadVarint(out var length) ||
                length > int.MaxValue ||
                _offset > _payload.Length - (int)length)
            {
                return false;
            }

            value = Encoding.UTF8.GetString(_payload.Slice(_offset, (int)length));
            _offset += (int)length;
            return true;
        }
    }
}
