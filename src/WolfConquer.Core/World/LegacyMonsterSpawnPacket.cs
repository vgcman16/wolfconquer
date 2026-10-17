using System.Buffers;
using System.Text;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed class LegacyMonsterSpawnPacket
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
    private const int HitpointsField = 21;
    private const int MaxLifeField = 23;
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
    private const int OwnerPetField = 81;
    private const int OwnerPet1Field = 82;
    private const int OwnerUidField = 83;
    private const int NamesField = 107;
    private const int UnknownBoolField = 108;
    private const int PrestigeRankField = 110;
    private const int MonsterIdField = 111;
    private const int Test1Field = 112;
    private const int Test2Field = 113;
    private const int CollectionIdField = 121;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;
    private const int MonsterStatusFlagCount = 10;

    public const int StatusFlagVectorLength = MonsterStatusFlagCount;
    private const int MonsterNameCount = 4;

    private static readonly int[] RequiredNumericFields =
    [
        MeshField,
        UidField,
        GuildIdField,
        GuildRankField,
        UnknownField,
        HeadField,
        GarmentField,
        ArmorField,
        LeftWeaponField,
        RightWeaponField,
        MonsterLevelField,
        XField,
        YField,
        HairStyleField,
        FacingField,
        ActionField,
        RebornField,
        LevelField,
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

    private static readonly int[] RequiredZeroFields =
    [
        GuildRankField,
        UnknownField,
        HeadField,
        GarmentField,
        ArmorField,
        LeftWeaponField,
        RightWeaponField,
        HairStyleField,
        RebornField,
        WindowSpawnField,
        AwayField,
        ExtraBattlePowerField,
        FlowerIconField,
        NobilityRankField,
        HeadSoulField,
        ArmorSoulField,
        LeftWeaponSoulField,
        RightWeaponSoulField,
        SubClassField,
        ActiveSubClassesField,
        ServerIdField,
        UnknownBoolField,
        PrestigeRankField,
        Test1Field,
        Test2Field,
        CollectionIdField,
    ];

    private readonly string[] _names;
    private readonly ulong[] _statusFlags;

    private LegacyMonsterSpawnPacket(
        uint mesh,
        uint uid,
        uint hitpoints,
        uint maxLife,
        byte monsterLevel,
        ushort x,
        ushort y,
        ushort action,
        ushort level,
        bool boss,
        uint monsterId,
        uint guildId,
        ushort facing,
        uint ownerPet,
        uint ownerPet1,
        uint ownerUid,
        IReadOnlyList<string> names,
        IReadOnlyList<ulong> statusFlags)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(statusFlags);

        if (names.Count != MonsterNameCount)
        {
            throw new ArgumentOutOfRangeException(nameof(names), names.Count, "Legacy monster spawn writes four name slots.");
        }

        if (statusFlags.Count != MonsterStatusFlagCount)
        {
            throw new ArgumentOutOfRangeException(nameof(statusFlags), statusFlags.Count, "Legacy monster spawn writes ten status flag slots.");
        }

        Mesh = mesh;
        Uid = uid;
        Hitpoints = hitpoints;
        MaxLife = maxLife;
        MonsterLevel = monsterLevel;
        X = x;
        Y = y;
        Action = action;
        Level = level;
        Boss = boss;
        MonsterId = monsterId;
        GuildId = guildId;
        Facing = facing;
        OwnerPet = ownerPet;
        OwnerPet1 = ownerPet1;
        OwnerUid = ownerUid;
        _names = names.ToArray();
        _statusFlags = statusFlags.ToArray();
    }

    public const ushort Opcode = WorldPacketOpcodes.LegacyRoleSpawn;

    public uint Mesh { get; }

    public uint Uid { get; }

    public uint Hitpoints { get; }

    public uint MaxLife { get; }

    public byte MonsterLevel { get; }

    public ushort X { get; }

    public ushort Y { get; }

    public ushort Action { get; }

    public ushort Level { get; }

    public bool Boss { get; }

    public uint MonsterId { get; }

    public uint GuildId { get; }

    public ushort Facing { get; }

    public uint OwnerPet { get; }

    public uint OwnerPet1 { get; }

    public uint OwnerUid { get; }

    public IReadOnlyList<string> Names => _names;

    public IReadOnlyList<ulong> StatusFlags => _statusFlags;

    public static LegacyMonsterSpawnPacket FromMonsterRole(
        uint uid,
        uint mesh,
        byte level,
        uint hitpoints,
        uint maxLife,
        ushort x,
        ushort y,
        ushort action,
        uint monsterId,
        string name,
        bool boss = false,
        IReadOnlyList<ulong>? statusFlags = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new LegacyMonsterSpawnPacket(
            mesh,
            uid,
            ScaleBossHitpoints(hitpoints, maxLife, boss),
            maxLife,
            level,
            x,
            y,
            action,
            level,
            boss,
            monsterId,
            guildId: 0,
            facing: 0,
            ownerPet: 0,
            ownerPet1: 0,
            ownerUid: 0,
            [name, string.Empty, string.Empty, string.Empty],
            statusFlags ?? new ulong[MonsterStatusFlagCount]);
    }

    public static LegacyMonsterSpawnPacket FromOwnerPet(
        uint uid,
        uint mesh,
        byte level,
        uint hitpoints,
        uint maxLife,
        ushort x,
        ushort y,
        uint ownerUid,
        ushort ownerFacing,
        uint ownerGuildId,
        uint ownerPet,
        uint ownerPet1,
        uint monsterId,
        string name,
        ushort action = 0,
        IReadOnlyList<ulong>? statusFlags = null,
        LegacyClientCompatibilityOptions? clientCompatibility = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (mesh == 980)
        {
            ownerPet = 3;
            monsterId = 4264;
        }

        var resolvedMesh = LegacyClientAppearanceCompatibility.ResolveMonsterMesh(mesh, name, clientCompatibility);
        return new LegacyMonsterSpawnPacket(
            resolvedMesh,
            uid,
            hitpoints,
            maxLife,
            level,
            x,
            y,
            action,
            level,
            boss: false,
            monsterId,
            ownerGuildId,
            ownerFacing,
            ownerPet,
            ownerPet1,
            ownerUid,
            [name, string.Empty, string.Empty, string.Empty],
            statusFlags ?? new ulong[MonsterStatusFlagCount]);
    }

    public static bool TryParse(PacketFrame frame, out LegacyMonsterSpawnPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var seenFields = new HashSet<int>();
        var names = new List<string>(MonsterNameCount);
        var statusFlags = new List<ulong>(MonsterStatusFlagCount);
        uint mesh = 0;
        uint uid = 0;
        uint hitpoints = 0;
        uint maxLife = 0;
        byte monsterLevel = 0;
        ushort x = 0;
        ushort y = 0;
        ushort action = 0;
        ushort level = 0;
        var boss = false;
        uint monsterId = 0;
        uint guildId = 0;
        ushort facing = 0;
        uint ownerPet = 0;
        uint ownerPet1 = 0;
        uint ownerUid = 0;

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

            if (fieldNumber == StatusFlagsField)
            {
                statusFlags.Add(value);
                continue;
            }

            if (IsRequiredZeroField(fieldNumber))
            {
                if (value != 0)
                {
                    return false;
                }

                seenFields.Add(fieldNumber);
                continue;
            }

            switch (fieldNumber)
            {
                case MeshField:
                    if (!TryToUInt32(value, out mesh))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case UidField:
                    if (!TryToUInt32(value, out uid))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case GuildIdField:
                    if (!TryToUInt32(value, out guildId))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case HitpointsField:
                    if (!TryToUInt32(value, out hitpoints))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case MaxLifeField:
                    if (!TryToUInt32(value, out maxLife))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case MonsterLevelField:
                    if (!TryToByte(value, out monsterLevel))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case XField:
                    if (!TryToUInt16(value, out x))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case YField:
                    if (!TryToUInt16(value, out y))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case FacingField:
                    if (!TryToUInt16(value, out facing))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case ActionField:
                    if (!TryToUInt16(value, out action))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case LevelField:
                    if (!TryToUInt16(value, out level))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case BossField:
                    if (!TryToBool(value, out boss))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case MonsterIdField:
                    if (!TryToUInt32(value, out monsterId))
                    {
                        return false;
                    }

                    seenFields.Add(fieldNumber);
                    break;
                case OwnerPetField:
                    if (!TryToUInt32(value, out ownerPet))
                    {
                        return false;
                    }

                    break;
                case OwnerPet1Field:
                    if (!TryToUInt32(value, out ownerPet1))
                    {
                        return false;
                    }

                    break;
                case OwnerUidField:
                    if (!TryToUInt32(value, out ownerUid))
                    {
                        return false;
                    }

                    break;
                default:
                    return false;
            }
        }

        if (!HasRequiredFields(seenFields) ||
            statusFlags.Count != MonsterStatusFlagCount ||
            names.Count != MonsterNameCount)
        {
            return false;
        }

        packet = new LegacyMonsterSpawnPacket(
            mesh,
            uid,
            hitpoints,
            maxLife,
            monsterLevel,
            x,
            y,
            action,
            level,
            boss,
            monsterId,
            guildId,
            facing,
            ownerPet,
            ownerPet1,
            ownerUid,
            names,
            statusFlags);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        WriteVarintField(payload, MeshField, Mesh);
        WriteVarintField(payload, UidField, Uid);
        WriteVarintField(payload, GuildIdField, GuildId);
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
        WriteOptionalVarintField(payload, HitpointsField, Hitpoints);
        WriteOptionalVarintField(payload, MaxLifeField, MaxLife);
        WriteVarintField(payload, MonsterLevelField, MonsterLevel);
        WriteVarintField(payload, XField, X);
        WriteVarintField(payload, YField, Y);
        WriteVarintField(payload, HairStyleField, 0);
        WriteVarintField(payload, FacingField, Facing);
        WriteVarintField(payload, ActionField, Action);
        WriteVarintField(payload, RebornField, 0);
        WriteVarintField(payload, LevelField, Level);
        WriteVarintField(payload, WindowSpawnField, 0);
        WriteVarintField(payload, AwayField, 0);
        WriteVarintField(payload, ExtraBattlePowerField, 0);
        WriteVarintField(payload, FlowerIconField, 0);
        WriteVarintField(payload, NobilityRankField, 0);
        WriteVarintField(payload, BossField, Boss ? 1UL : 0);
        WriteVarintField(payload, HeadSoulField, 0);
        WriteVarintField(payload, ArmorSoulField, 0);
        WriteVarintField(payload, LeftWeaponSoulField, 0);
        WriteVarintField(payload, RightWeaponSoulField, 0);
        WriteVarintField(payload, SubClassField, 0);
        WriteVarintField(payload, ActiveSubClassesField, 0);
        WriteVarintField(payload, ServerIdField, 0);
        WriteOptionalVarintField(payload, OwnerPetField, OwnerPet);
        WriteOptionalVarintField(payload, OwnerPet1Field, OwnerPet1);
        WriteOptionalVarintField(payload, OwnerUidField, OwnerUid);
        foreach (var name in Names)
        {
            WriteString(payload, NamesField, name);
        }

        WriteVarintField(payload, UnknownBoolField, 0);
        WriteVarintField(payload, PrestigeRankField, 0);
        WriteVarintField(payload, MonsterIdField, MonsterId);
        WriteVarintField(payload, Test1Field, 0);
        WriteVarintField(payload, Test2Field, 0);
        WriteVarintField(payload, CollectionIdField, 0);

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static uint ScaleBossHitpoints(uint hitpoints, uint maxLife, bool boss)
    {
        if (!boss)
        {
            return hitpoints;
        }

        var divisor = maxLife / 10_000;
        return divisor != 0
            ? hitpoints / divisor
            : unchecked(hitpoints * maxLife);
    }

    private static bool HasRequiredFields(IReadOnlySet<int> seenFields)
    {
        foreach (var fieldNumber in RequiredNumericFields)
        {
            if (!seenFields.Contains(fieldNumber))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsRequiredZeroField(int fieldNumber)
    {
        foreach (var zeroField in RequiredZeroFields)
        {
            if (fieldNumber == zeroField)
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

    private static void WriteOptionalVarintField(List<byte> payload, int fieldNumber, ulong value)
    {
        if (value != 0)
        {
            WriteVarintField(payload, fieldNumber, value);
        }
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
