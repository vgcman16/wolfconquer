using System.Buffers;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public enum LegacyMsgUpdateDataType : uint
{
    Hitpoints = 0,
    MaxHitpoints = 1,
    Mana = 2,
    MaxMana = 3,
    Money = 4,
    Experience = 5,
    Conquer = 6,
    PKPoints = Conquer,
    Class = 7,
    Atributes = 0x0A,
    Stamina = 8,
    Mesh = 0x0B,
    Level = 0xC,
    Spirit = 0x0D,
    Vitality = 0x0E,
    Strength = 0x0F,
    Agility = 0x10,
    WarehouseMoney = 9,
    HeavensBlessing = 0x11,
    StatusFlag = 0x19,
    XPCircle = 0x1B,
    ConquerPoints = 0x1D,
    OnlineTraining = 0x1F,
    Merchant = 0x26,
    VIPLevel = 0x27,
    EnlightPoints = 0x29,
    LegacyLoginFlag = 44,
    BoundConquerPoints = 0x2D,
    RaceShopPoints = 0x2F,
    AzureShield = 0x31,
    Team = 0x34,
    SoulShackle = 0x36,
    Fatigue = 0x37,
    DefensiveStance = 0x38,
    MainFlag = 0x47,
    DragonSwing = 0x4B,
    InnerPowerPotency = 0x4D,
    AppendIcon = 0x4E,
    InventorySash = 0x4F,
    InventorySashMax = 0x50,
    XPList = 0x65,
    FineRain = 0x66,
    Infinity = 0x67,
    XPDuration = 0x68,
    ForbiddenPirate = 0x6B,
    SkillCountdown = 0x6C,
    UndyingImprinting = 0x6F,
    TidalWave = 0x71,
    Quench = 0x72,
    CelestialDance = 0x77,
    ArchiveSkill = 0x78,
    ClassExperience = 0x79,
    SageSpeedModeCounter = 0x88,
    GuildDonation = 125,
    MyDontion = GuildDonation,
    GuildDonationMirror = 126,
    DonationPoints = GuildDonationMirror,
    BloodthirstElan = 0x8A,
}

public readonly record struct LegacyMsgUpdateEntry(
    LegacyMsgUpdateDataType Type,
    ulong Value,
    uint Flag = 0,
    uint Time = 0,
    uint Damage = 0,
    uint SpellLevel = 0,
    IReadOnlyList<ulong>? Values = null);

public sealed record LegacyMsgUpdatePacket(uint Uid, IReadOnlyList<LegacyMsgUpdateEntry> Updates)
{
    private const int UidField = 1;
    private const int UpdatesField = 2;
    private const int UpdateTypeField = 1;
    private const int UpdateValueField = 2;
    private const int UpdateValuesField = 3;
    private const int UpdateFlagField = 4;
    private const int UpdateTimeField = 5;
    private const int UpdateDamageField = 6;
    private const int UpdateSpellLevelField = 7;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;

    public const ushort Opcode = WorldPacketOpcodes.LegacyMsgUpdate;
    public const int RuntimeStatusFlagVectorLength = 18;

    public static IReadOnlyList<LegacyMsgUpdatePacket> CreateLegacyWorldLoginPreAnswerUpdates(
        CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var uid = CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        var conquer = ReadLegacyAttributeUInt64(snapshot, "Conquer");
        var guildDonation = ReadLegacyAttributeUInt64(snapshot, "GuildDontion", "GuildDonation");

        return
        [
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Money, CheckedUInt64(snapshot.Money, nameof(snapshot.Money))),
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Conquer, conquer),
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.LegacyLoginFlag, 15),
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.GuildDonation, guildDonation),
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.GuildDonationMirror, guildDonation),
        ];
    }

    public static LegacyMsgUpdatePacket CreateLegacyInnerPowerPotencyStartupUpdate(
        CharacterSnapshot snapshot,
        string? usersRoot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var uid = CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        return CreateSingleUpdate(
            uid,
            LegacyMsgUpdateDataType.InnerPowerPotency,
            ReadLegacyInnerPowerPotency(uid, usersRoot));
    }

    public static LegacyMsgUpdatePacket CreateLegacyVipLevelStartupUpdate(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return CreateSingleUpdate(
            CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            LegacyMsgUpdateDataType.VIPLevel,
            ReadLegacyAttributeUInt64(snapshot, "VipLevel", "VIPLevel"));
    }

    public static LegacyMsgUpdatePacket CreateLegacyMerchantStartupUpdate(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return CreateSingleUpdate(
            CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            LegacyMsgUpdateDataType.Merchant,
            255);
    }

    public static bool TryCreateLegacyDailyResetEnlightPointsUpdate(
        CharacterSnapshot snapshot,
        out LegacyMsgUpdatePacket packet)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        packet = default!;
        if (snapshot.Level < 90 ||
            !TryReadLegacyAttributeUInt64(snapshot, out var enlighten, "Enilghten", "Enlighten"))
        {
            return false;
        }

        packet = CreateSingleUpdate(
            CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            LegacyMsgUpdateDataType.EnlightPoints,
            enlighten);
        return true;
    }

    public static LegacyMsgUpdatePacket CreateLegacyPKPointsStartupUpdate(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return CreateSingleUpdate(
            CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            LegacyMsgUpdateDataType.PKPoints,
            CheckedUInt64(snapshot.PkPoints, nameof(snapshot.PkPoints)));
    }

    public static LegacyMsgUpdatePacket CreateLegacyClassExperienceStartupUpdate(
        CharacterSnapshot snapshot,
        LegacyNativePlayerRuntime? nativePlayer = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return CreateSingleUpdate(
            CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid)),
            LegacyMsgUpdateDataType.ClassExperience,
            nativePlayer is null
                ? ReadLegacyAttributeUInt64(snapshot, "ClassExperience")
                : nativePlayer.ClassExperience);
    }

    public static IReadOnlyList<LegacyMsgUpdatePacket> CreateLegacyDonationStartupUpdates(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var uid = CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        var donation = ReadLegacyAttributeUInt64(snapshot, "GuildDontion", "GuildDonation", "MyDontion");
        return
        [
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.MyDontion, donation),
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.DonationPoints, donation),
        ];
    }

    public static IReadOnlyList<LegacyMsgUpdatePacket> CreateLegacyInventorySashStartupUpdates(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var uid = CheckedUInt32(snapshot.Uid, nameof(snapshot.Uid));
        return
        [
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.InventorySashMax, 300),
            CreateSingleUpdate(
                uid,
                LegacyMsgUpdateDataType.InventorySash,
                ReadLegacyAttributeUInt64(snapshot, "InventorySashCount")),
        ];
    }

    public static LegacyMsgUpdatePacket CreateLegacyRuntimeXPCircleTickUpdate(uint uid, ulong count)
    {
        return CreateSingleUpdate(uid, LegacyMsgUpdateDataType.XPCircle, count);
    }

    public static LegacyMsgUpdatePacket CreateLegacyRuntimeXPListUpdate(uint uid, ulong value = 1)
    {
        return CreateSingleUpdate(uid, LegacyMsgUpdateDataType.XPList, value);
    }

    public static LegacyMsgUpdatePacket CreateLegacyRuntimeStaminaTickUpdate(uint uid, ulong stamina)
    {
        return CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Stamina, stamina);
    }

    public static LegacyMsgUpdatePacket CreateLegacyRuntimeExperienceUpdate(uint uid, ulong experience)
    {
        return CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Experience, experience);
    }

    public static LegacyMsgUpdatePacket CreateLegacyRuntimeLevelUpdate(uint uid, ulong level)
    {
        return CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Level, level);
    }

    public static IReadOnlyList<LegacyMsgUpdatePacket> CreateLegacyRuntimeSourceStatUpdates(
        uint uid,
        CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return
        [
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Strength, CheckedUInt64(snapshot.Strength, nameof(snapshot.Strength))),
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Agility, CheckedUInt64(snapshot.Agility, nameof(snapshot.Agility))),
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Spirit, CheckedUInt64(snapshot.Spirit, nameof(snapshot.Spirit))),
            CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Vitality, CheckedUInt64(snapshot.Vitality, nameof(snapshot.Vitality))),
        ];
    }

    public static LegacyMsgUpdatePacket CreateLegacyRuntimeAttributePointsUpdate(uint uid, ulong attributePoints)
    {
        return CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Atributes, attributePoints);
    }

    public static IReadOnlyList<LegacyMsgUpdatePacket> CreateLegacyPlayerManaUpdatePair(
        uint uid,
        ulong maxMana,
        ulong mana)
    {
        var maxManaUpdate = new LegacyMsgUpdateEntry(
            LegacyMsgUpdateDataType.MaxMana,
            maxMana);
        var manaUpdate = new LegacyMsgUpdateEntry(
            LegacyMsgUpdateDataType.Mana,
            mana);

        return
        [
            new LegacyMsgUpdatePacket(uid, [maxManaUpdate]),
            new LegacyMsgUpdatePacket(uid, [maxManaUpdate, manaUpdate]),
        ];
    }

    public static LegacyMsgUpdatePacket CreateLegacyRuntimeMeshUpdate(uint uid, ulong mesh)
    {
        return CreateSingleUpdate(uid, LegacyMsgUpdateDataType.Mesh, mesh);
    }

    public static LegacyMsgUpdatePacket CreateLegacyRuntimeStatusFlagVectorUpdate(
        uint uid,
        IReadOnlyList<ulong> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count != RuntimeStatusFlagVectorLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                values.Count,
                "Legacy runtime StatusFlag updates require 18 UInt64 words.");
        }

        var copy = new ulong[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            copy[index] = values[index];
        }

        return new LegacyMsgUpdatePacket(
            uid,
            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.StatusFlag, Value: 0, Values: copy)]);
    }

    public static IReadOnlyList<LegacyMsgUpdatePacket> CreateMonsterHitpointUpdateSequence(
        LegacyMonsterInstance monster)
    {
        ArgumentNullException.ThrowIfNull(monster);

        if (!ShouldSendMonsterHitpointUpdates(monster))
        {
            return [];
        }

        var maxHitpoints = new LegacyMsgUpdateEntry(
            LegacyMsgUpdateDataType.MaxHitpoints,
            monster.MaxLife);
        var hitpoints = new LegacyMsgUpdateEntry(
            LegacyMsgUpdateDataType.Hitpoints,
            monster.Hitpoints);

        return
        [
            new LegacyMsgUpdatePacket(monster.Uid, [maxHitpoints]),
            new LegacyMsgUpdatePacket(monster.Uid, [maxHitpoints, hitpoints]),
        ];
    }

    public static IReadOnlyList<LegacyMsgUpdatePacket> CreateLegacyPlayerHitpointUpdatePair(
        uint uid,
        uint maxHitpoints,
        uint hitpoints)
    {
        var maxHitpointUpdate = new LegacyMsgUpdateEntry(
            LegacyMsgUpdateDataType.MaxHitpoints,
            maxHitpoints);
        var hitpointUpdate = new LegacyMsgUpdateEntry(
            LegacyMsgUpdateDataType.Hitpoints,
            hitpoints);

        return
        [
            new LegacyMsgUpdatePacket(uid, [maxHitpointUpdate]),
            new LegacyMsgUpdatePacket(uid, [maxHitpointUpdate, hitpointUpdate]),
        ];
    }

    public static bool ShouldSendMonsterHitpointUpdates(LegacyMonsterInstance monster)
    {
        ArgumentNullException.ThrowIfNull(monster);

        return monster.Hitpoints > ushort.MaxValue || monster.Boss;
    }

    public static bool TryParse(PacketFrame frame, out LegacyMsgUpdatePacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var uidSeen = false;
        var uid = 0u;
        var updates = new List<LegacyMsgUpdateEntry>();

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
                case UidField:
                    if (wireType != VarintWireType ||
                        !reader.TryReadVarint(out var uidValue) ||
                        uidValue > uint.MaxValue)
                    {
                        return false;
                    }

                    uid = (uint)uidValue;
                    uidSeen = true;
                    break;

                case UpdatesField:
                    if (wireType != LengthDelimitedWireType ||
                        !reader.TryReadBytes(out var updateBytes) ||
                        !TryParseUpdate(updateBytes, out var update))
                    {
                        return false;
                    }

                    updates.Add(update);
                    break;

                default:
                    return false;
            }
        }

        if (!uidSeen || updates.Count == 0)
        {
            return false;
        }

        packet = new LegacyMsgUpdatePacket(uid, updates);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        ArgumentNullException.ThrowIfNull(Updates);
        if (Updates.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Updates), "Legacy MsgUpdate requires at least one update entry.");
        }

        var payload = new List<byte>();
        WriteVarintField(payload, UidField, Uid);
        foreach (var update in Updates)
        {
            var updatePayload = new List<byte>();
            if (update.Type != LegacyMsgUpdateDataType.Hitpoints)
            {
                WriteVarintField(updatePayload, UpdateTypeField, (ulong)update.Type);
            }

            if (update.Value != 0)
            {
                WriteVarintField(updatePayload, UpdateValueField, update.Value);
            }

            if (update.Values is not null)
            {
                foreach (var value in update.Values)
                {
                    WriteVarintField(updatePayload, UpdateValuesField, value);
                }
            }

            if (update.Flag != 0)
            {
                WriteVarintField(updatePayload, UpdateFlagField, update.Flag);
            }

            if (update.Time != 0)
            {
                WriteVarintField(updatePayload, UpdateTimeField, update.Time);
            }

            if (update.Damage != 0)
            {
                WriteVarintField(updatePayload, UpdateDamageField, update.Damage);
            }

            if (update.SpellLevel != 0)
            {
                WriteVarintField(updatePayload, UpdateSpellLevelField, update.SpellLevel);
            }

            WriteBytes(payload, UpdatesField, updatePayload);
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static bool TryParseUpdate(byte[] payload, out LegacyMsgUpdateEntry update)
    {
        var reader = new ProtoReader(payload);
        var type = LegacyMsgUpdateDataType.Hitpoints;
        var value = 0UL;
        var flag = 0u;
        var time = 0u;
        var damage = 0u;
        var spellLevel = 0u;
        List<ulong>? values = null;

        while (!reader.End)
        {
            if (!reader.TryReadVarint(out var tagValue) || tagValue == 0)
            {
                update = default;
                return false;
            }

            var fieldNumber = (int)(tagValue >> 3);
            var wireType = (int)(tagValue & 0x7);
            if (wireType != VarintWireType ||
                !reader.TryReadVarint(out var fieldValue))
            {
                update = default;
                return false;
            }

            switch (fieldNumber)
            {
                case UpdateTypeField:
                    if (fieldValue > uint.MaxValue)
                    {
                        update = default;
                        return false;
                    }

                    type = (LegacyMsgUpdateDataType)(uint)fieldValue;
                    break;

                case UpdateValueField:
                    value = fieldValue;
                    break;

                case UpdateValuesField:
                    values ??= [];
                    values.Add(fieldValue);
                    break;

                case UpdateFlagField:
                    if (fieldValue > uint.MaxValue)
                    {
                        update = default;
                        return false;
                    }

                    flag = (uint)fieldValue;
                    break;

                case UpdateTimeField:
                    if (fieldValue > uint.MaxValue)
                    {
                        update = default;
                        return false;
                    }

                    time = (uint)fieldValue;
                    break;

                case UpdateDamageField:
                    if (fieldValue > uint.MaxValue)
                    {
                        update = default;
                        return false;
                    }

                    damage = (uint)fieldValue;
                    break;

                case UpdateSpellLevelField:
                    if (fieldValue > uint.MaxValue)
                    {
                        update = default;
                        return false;
                    }

                    spellLevel = (uint)fieldValue;
                    break;

                default:
                    update = default;
                    return false;
            }
        }

        update = new LegacyMsgUpdateEntry(type, value, flag, time, damage, spellLevel, values);
        return true;
    }

    private static LegacyMsgUpdatePacket CreateSingleUpdate(
        uint uid,
        LegacyMsgUpdateDataType type,
        ulong value)
    {
        return new LegacyMsgUpdatePacket(uid, [new LegacyMsgUpdateEntry(type, value)]);
    }

    private static ulong ReadLegacyAttributeUInt64(CharacterSnapshot snapshot, params string[] keys)
    {
        if (TryReadLegacyAttributeUInt64(snapshot, out var value, keys))
        {
            return value;
        }

        return 0;
    }

    private static bool TryReadLegacyAttributeUInt64(
        CharacterSnapshot snapshot,
        out ulong value,
        params string[] keys)
    {
        value = 0;
        foreach (var key in keys)
        {
            if (!snapshot.Attributes.TryGetValue(key, out var raw))
            {
                continue;
            }

            if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            {
                throw new FormatException($"Invalid character snapshot integer '{key}': '{raw}'.");
            }

            value = CheckedUInt64(parsed, key);
            return true;
        }

        return false;
    }

    private static ulong ReadLegacyInnerPowerPotency(uint uid, string? usersRoot)
    {
        var dbRoot = TryDeriveDbRoot(usersRoot);
        if (dbRoot is null)
        {
            return 0;
        }

        var path = Path.Combine(dbRoot, "InnerPower.txt");
        if (!File.Exists(path))
        {
            return 0;
        }

        try
        {
            foreach (var line in File.ReadLines(path))
            {
                if (line.StartsWith("Count=", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var fields = line.Split('/', StringSplitOptions.None);
                if (fields.Length < 3 ||
                    !uint.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentUid) ||
                    currentUid != uid)
                {
                    continue;
                }

                if (!long.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var potency))
                {
                    throw new FormatException($"Invalid InnerPower potency for UID {uid}: '{fields[2]}'.");
                }

                return CheckedUInt64(potency, "InnerPower.Potency");
            }
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }

        return 0;
    }

    private static string? TryDeriveDbRoot(string? usersRoot)
    {
        if (string.IsNullOrWhiteSpace(usersRoot))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(usersRoot);
        var trimmed = Path.TrimEndingDirectorySeparator(fullPath);
        return string.Equals(Path.GetFileName(trimmed), "Users", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(trimmed)
            : null;
    }

    private static uint CheckedUInt32(long value, string name)
    {
        if (value < 0 || value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy MsgUpdate field must fit UInt32.");
        }

        return (uint)value;
    }

    private static ulong CheckedUInt64(long value, string name)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(name, value, "Legacy MsgUpdate field cannot be negative.");
        }

        return (ulong)value;
    }

    private static void WriteVarintField(List<byte> payload, int fieldNumber, ulong value)
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
