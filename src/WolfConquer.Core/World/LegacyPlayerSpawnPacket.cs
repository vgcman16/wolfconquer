using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class LegacyPlayerSpawnPacket
{
    private const int MeshField = 1;
    private const int UidField = 2;
    private const int GuildIdField = 3;
    private const int GuildRankField = 4;
    private const int UnknownField = 5;
    private const int StatusFlagsField = 6;
    private const int AppearanceTypeField = 7;
    private const int HeadField = 8;
    private const int GarmentField = 9;
    private const int ArmorField = 10;
    private const int LeftWeaponField = 11;
    private const int RightWeaponField = 12;
    private const int LeftWeaponAccessoryField = 13;
    private const int RightWeaponAccessoryField = 14;
    private const int SteedField = 15;
    private const int MountArmorField = 16;
    private const int WingField = 17;
    private const int WingPlusField = 18;
    private const int WingProgressField = 19;
    private const int BottleField = 20;
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
    private const int QuizPointsField = 44;
    private const int SteedPlusField = 45;
    private const int SteedColorField = 46;
    private const int EnlightenField = 48;
    private const int InnerStrengthScoreField = 49;
    private const int ClanUidField = 53;
    private const int ClanRankField = 54;
    private const int TitleField = 56;
    private const int PokerSeatField = 57;
    private const int PokerTableIdField = 58;
    private const int GuildBattlePowerField = 59;
    private const int RaceItemField = 61;
    private const int BossField = 63;
    private const int HeadSoulField = 64;
    private const int ArmorSoulField = 65;
    private const int LeftWeaponSoulField = 66;
    private const int RightWeaponSoulField = 67;
    private const int SubClassField = 68;
    private const int ActiveSubClassesField = 69;
    private const int FirstRebornClassField = 70;
    private const int SecondRebornClassField = 71;
    private const int ClassField = 72;
    private const int CountryCodeField = 73;
    private const int TeamField = 74;
    private const int BattlePowerField = 75;
    private const int JiangHuTalentField = 76;
    private const int JiangHuActiveField = 77;
    private const int ServerIdField = 79;
    private const int OwnerPetField = 81;
    private const int OwnerPet1Field = 82;
    private const int OwnerUidField = 83;
    private const int UnionIdField = 84;
    private const int UnionExploitsField = 85;
    private const int OfficialHaremGuardsField = 86;
    private const int UnionRankField = 87;
    private const int UnionTypeField = 88;
    private const int MyTitleField = 89;
    private const int MyTitleScoreField = 90;
    private const int MyWingField = 91;
    private const int MainFlagField = 92;
    private const int RelicField = 96;
    private const int NamesField = 107;
    private const int UnknownBoolField = 108;
    private const int HundredWeaponsField = 109;
    private const int PrestigeRankField = 110;
    private const int MonsterIdField = 111;
    private const int Test1Field = 112;
    private const int Test2Field = 113;
    private const int CollectionIdField = 121;
    private const int VarintWireType = 0;
    private const int LengthDelimitedWireType = 2;
    private const int StatusFlagWords = 10;
    private const int PlayerNameSlots = 7;
    private const int HundredWeaponSlots = 5;
    private const ushort HeadPosition = 1;
    private const ushort ArmorPosition = 3;
    private const ushort RightWeaponPosition = 4;
    private const ushort LeftWeaponPosition = 5;
    private const ushort BottlePosition = 7;
    private const ushort GarmentPosition = 9;
    private const ushort SteedPosition = 12;
    private const ushort RelicsPosition = 13;
    private const ushort RightWeaponAccessoryPosition = 15;
    private const ushort LeftWeaponAccessoryPosition = 16;
    private const ushort SteedMountPosition = 17;
    private const ushort WingPosition = 19;
    private const ushort AlternateHeadPosition = 51;
    private const ushort AlternateArmorPosition = 53;
    private const ushort AlternateRightWeaponPosition = 54;
    private const ushort AlternateLeftWeaponPosition = 55;
    private const ushort AlternateBottlePosition = 57;
    private const ushort AlternateGarmentPosition = 59;

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

    private static readonly int[] OptionalNumericFields =
    [
        AppearanceTypeField,
        LeftWeaponAccessoryField,
        RightWeaponAccessoryField,
        SteedField,
        MountArmorField,
        WingField,
        WingPlusField,
        WingProgressField,
        BottleField,
        HitpointsField,
        MaxLifeField,
        QuizPointsField,
        SteedPlusField,
        SteedColorField,
        EnlightenField,
        InnerStrengthScoreField,
        ClanUidField,
        ClanRankField,
        TitleField,
        PokerSeatField,
        PokerTableIdField,
        GuildBattlePowerField,
        RaceItemField,
        FirstRebornClassField,
        SecondRebornClassField,
        ClassField,
        CountryCodeField,
        TeamField,
        BattlePowerField,
        JiangHuTalentField,
        JiangHuActiveField,
        OwnerPetField,
        OwnerPet1Field,
        OwnerUidField,
        UnionIdField,
        UnionExploitsField,
        OfficialHaremGuardsField,
        UnionRankField,
        UnionTypeField,
        MyTitleField,
        MyTitleScoreField,
        MyWingField,
        MainFlagField,
        RelicField,
    ];

    private static readonly int[] BoolFields =
    [
        WindowSpawnField,
        AwayField,
        RaceItemField,
        BossField,
        JiangHuActiveField,
        UnknownBoolField,
    ];

    private readonly Dictionary<int, ulong> _numericFields;
    private readonly string[] _names;
    private readonly ulong[] _statusFlags;
    private readonly uint[] _hundredWeapons;

    private LegacyPlayerSpawnPacket(
        Dictionary<int, ulong> numericFields,
        IReadOnlyList<string> names,
        IReadOnlyList<ulong> statusFlags,
        IReadOnlyList<uint> hundredWeapons)
    {
        ArgumentNullException.ThrowIfNull(numericFields);
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(statusFlags);
        ArgumentNullException.ThrowIfNull(hundredWeapons);

        if (names.Count != PlayerNameSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(names), names.Count, "Legacy player spawn writes seven name slots.");
        }

        if (statusFlags.Count != StatusFlagWords)
        {
            throw new ArgumentOutOfRangeException(nameof(statusFlags), statusFlags.Count, "Legacy player spawn writes ten status flag words.");
        }

        if (hundredWeapons.Count != HundredWeaponSlots)
        {
            throw new ArgumentOutOfRangeException(nameof(hundredWeapons), hundredWeapons.Count, "Legacy player spawn writes five HundredWeapons values.");
        }

        _numericFields = new Dictionary<int, ulong>(numericFields);
        _names = names.ToArray();
        _statusFlags = statusFlags.ToArray();
        _hundredWeapons = hundredWeapons.ToArray();
    }

    public const ushort Opcode = WorldPacketOpcodes.LegacyRoleSpawn;

    public IReadOnlyList<string> Names => _names;

    public IReadOnlyList<ulong> StatusFlags => _statusFlags;

    public IReadOnlyList<uint> HundredWeapons => _hundredWeapons;

    public ulong GetNumericField(int fieldNumber)
    {
        return _numericFields.TryGetValue(fieldNumber, out var value) ? value : 0;
    }

    public static LegacyPlayerSpawnPacket FromSnapshot(
        CharacterSnapshot snapshot,
        ushort serverId = 2,
        bool windowSpawn = false,
        uint? maxLife = null)
    {
        return FromSnapshot(snapshot, PlayerItemSnapshot.Empty, serverId, windowSpawn, maxLife);
    }

    public static LegacyPlayerSpawnPacket FromSnapshot(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        ushort serverId = 2,
        bool windowSpawn = false,
        uint? maxLife = null)
    {
        return FromSnapshot(snapshot, playerItems, alternateEquipmentActive: false, serverId, windowSpawn, maxLife);
    }

    public static LegacyPlayerSpawnPacket FromShadowClone(
        CharacterSnapshot owner,
        uint cloneUid,
        ushort cloneFlag,
        PlayerItemSnapshot? playerItems = null,
        bool alternateEquipmentActive = false,
        ushort serverId = 0,
        uint? maxLife = null)
    {
        ArgumentNullException.ThrowIfNull(owner);

        var ownerProjection = FromSnapshot(
            owner,
            playerItems ?? PlayerItemSnapshot.Empty,
            alternateEquipmentActive,
            serverId,
            maxLife: maxLife);
        var numeric = new Dictionary<int, ulong>(ownerProjection._numericFields)
        {
            [UidField] = cloneUid,
            [OwnerUidField] = CheckedUInt64(owner.Uid, nameof(owner.Uid)),
            [OwnerPetField] = 2,
            [OwnerPet1Field] = cloneFlag,
            [XField] = CheckedUInt64(owner.X, nameof(owner.X)),
            [YField] = CheckedUInt64(owner.Y, nameof(owner.Y)),
            [FacingField] = ReadAttributeUInt64(owner.Attributes, "Angle", "Facing"),
        };

        return new LegacyPlayerSpawnPacket(
            numeric,
            CreateShadowCloneNames(owner),
            new ulong[StatusFlagWords],
            ownerProjection._hundredWeapons);
    }

    public static LegacyPlayerSpawnPacket FromSnapshot(
        CharacterSnapshot snapshot,
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive,
        ushort serverId = 2,
        bool windowSpawn = false,
        uint? maxLife = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(playerItems);

        var nobilityRank = ReadNobilityRank(snapshot);
        var guildBattlePower = ReadAttributeUInt64(snapshot.Attributes, "GuildBattlePower");
        var rawExtraBattlePower = ReadAttributeUInt64(snapshot.Attributes, "ExtraBattlePower");
        var extraBattlePower = rawExtraBattlePower > guildBattlePower ? rawExtraBattlePower - guildBattlePower : 0;
        var battlePower = ReadAttributeUInt64(snapshot.Attributes, "BattlePower");
        if (battlePower == 0)
        {
            battlePower = CheckedUInt64(snapshot.Level + (snapshot.Reborn * 5), "BattlePower") +
                nobilityRank +
                extraBattlePower +
                LegacyBattlePowerCalculator.CalculateEquipmentBattlePower(playerItems, alternateEquipmentActive);
        }

        var numeric = CreateRequiredNumericFields();
        numeric[MeshField] = BuildMesh(snapshot);
        numeric[UidField] = CheckedUInt64(snapshot.Uid, nameof(snapshot.Uid));
        numeric[GuildIdField] = ReadAttributeUInt64(snapshot.Attributes, "GuildID");
        numeric[GuildRankField] = ReadAttributeUInt64(snapshot.Attributes, "GuildRank");
        numeric[XField] = CheckedUInt64(snapshot.X, nameof(snapshot.X));
        numeric[YField] = CheckedUInt64(snapshot.Y, nameof(snapshot.Y));
        numeric[HairStyleField] = CheckedUInt64(snapshot.Hair, nameof(snapshot.Hair));
        numeric[FacingField] = ReadAttributeUInt64(snapshot.Attributes, "Angle", "Facing");
        numeric[ActionField] = ReadAttributeUInt64(snapshot.Attributes, "Action");
        numeric[RebornField] = CheckedUInt64(snapshot.Reborn, nameof(snapshot.Reborn));
        numeric[LevelField] = CheckedUInt64(snapshot.Level, nameof(snapshot.Level));
        numeric[WindowSpawnField] = windowSpawn ? 1UL : 0;
        numeric[AwayField] = ReadAttributeBool(snapshot.Attributes, "Away");
        numeric[ExtraBattlePowerField] = extraBattlePower;
        numeric[FlowerIconField] = ReadAttributeUInt64(snapshot.Attributes, "FlowerRank") + 10_000;
        numeric[NobilityRankField] = nobilityRank;
        numeric[SubClassField] = ReadAttributeUInt64(snapshot.Attributes, "SubClassHashPoint", "SubClassHasPoints");
        numeric[ActiveSubClassesField] = ReadAttributeUInt64(snapshot.Attributes, "ActiveSublass", "ActiveSubClasses");
        numeric[ServerIdField] = serverId;
        numeric[UnknownBoolField] = 1;
        numeric[Test1Field] = 1;
        numeric[Test2Field] = 1;
        numeric[CollectionIdField] = ReadAttributeUInt64(snapshot.Attributes, "CollectionID");
        ApplyEquipment(numeric, playerItems, alternateEquipmentActive);
        SetOptional(numeric, GarmentField, ReadAttributeUInt64(snapshot.Attributes, "SpecialGarment"));

        SetOptional(numeric, AppearanceTypeField, ReadAttributeUInt64(snapshot.Attributes, "AparenceType", "AppearanceType"));
        SetOptional(numeric, HitpointsField, CheckedUInt64(snapshot.MinHitPoints, nameof(snapshot.MinHitPoints)));
        SetOptional(numeric, MaxLifeField, maxLife ?? ReadMaxLife(snapshot));
        SetOptional(numeric, QuizPointsField, ReadAttributeUInt64(snapshot.Attributes, "QuizPoints"));
        SetOptional(numeric, EnlightenField, ReadAttributeUInt64(snapshot.Attributes, "Enilghten", "Enlighten"));
        SetOptional(numeric, InnerStrengthScoreField, ReadAttributeUInt64(snapshot.Attributes, "InnerStrengthScore"));
        SetOptional(numeric, ClanUidField, ReadAttributeUInt64(snapshot.Attributes, "ClanID", "ClanUID"));
        SetOptional(numeric, ClanRankField, ReadAttributeUInt64(snapshot.Attributes, "ClanRank"));
        SetOptional(numeric, TitleField, ReadAttributeUInt64(snapshot.Attributes, "MyTitle"));
        SetOptional(numeric, PokerSeatField, ReadAttributeUInt64(snapshot.Attributes, "PokerSeat"));
        SetOptional(numeric, PokerTableIdField, ReadAttributeUInt64(snapshot.Attributes, "PokerTableID"));
        SetOptional(numeric, GuildBattlePowerField, guildBattlePower);
        SetOptional(numeric, RaceItemField, ReadAttributeBool(snapshot.Attributes, "RaceItem"));
        SetOptional(numeric, FirstRebornClassField, CheckedUInt64(snapshot.FirstClass, nameof(snapshot.FirstClass)));
        SetOptional(numeric, SecondRebornClassField, CheckedUInt64(snapshot.SecondClass, nameof(snapshot.SecondClass)));
        SetOptional(numeric, ClassField, CheckedUInt64(snapshot.Class, nameof(snapshot.Class)));
        SetOptional(numeric, CountryCodeField, ReadAttributeUInt64(snapshot.Attributes, "CountryID", "CountryCode"));
        SetOptional(numeric, TeamField, ReadAttributeUInt64(snapshot.Attributes, "Team"));
        SetOptional(numeric, BattlePowerField, battlePower);
        SetOptional(numeric, JiangHuTalentField, ReadAttributeUInt64(snapshot.Attributes, "JiangHuTalent"));
        SetOptional(numeric, JiangHuActiveField, ReadAttributeBool(snapshot.Attributes, "JiangHuActive"));
        SetOptional(numeric, OwnerPetField, ReadAttributeUInt64(snapshot.Attributes, "OwnerPet"));
        SetOptional(numeric, OwnerPet1Field, ReadAttributeUInt64(snapshot.Attributes, "OwnerPet1"));
        SetOptional(numeric, OwnerUidField, ReadAttributeUInt64(snapshot.Attributes, "RealUID", "OwnerUID"));
        SetOptional(numeric, UnionIdField, ReadAttributeUInt64(snapshot.Attributes, "UnionUID", "UnionID"));
        SetOptional(numeric, UnionExploitsField, ReadAttributeUInt64(snapshot.Attributes, "KingDomExploits", "UnionExploits"));
        SetOptional(numeric, OfficialHaremGuardsField, ReadAttributeUInt64(snapshot.Attributes, "ExploitsRank", "Official_Harem_Guards"));
        SetOptional(numeric, UnionRankField, ReadAttributeUInt64(snapshot.Attributes, "UnionRank"));
        SetOptional(numeric, UnionTypeField, ReadAttributeUInt64(snapshot.Attributes, "UnionType"));
        SetOptional(numeric, MyTitleField, ReadAttributeUInt64(snapshot.Attributes, "SpecialTitleID"));
        SetOptional(numeric, MyTitleScoreField, ReadAttributeUInt64(snapshot.Attributes, "SpecialTitleScore"));
        SetOptional(numeric, MyWingField, ReadAttributeUInt64(snapshot.Attributes, "SpecialWingID"));
        SetOptional(numeric, MainFlagField, ReadAttributeUInt64(snapshot.Attributes, "MainFlag"));
        SetOptionalIfAbsent(numeric, RelicField, ReadAttributeUInt64(snapshot.Attributes, "Relic"));

        return new LegacyPlayerSpawnPacket(
            numeric,
            CreateNames(snapshot),
            ReadVector(snapshot.Attributes, StatusFlagWords, "StatusFlags", "BitVector"),
            ReadUInt32Vector(snapshot.Attributes, HundredWeaponSlots, "HundredWeapons"));
    }

    private static void ApplyEquipment(
        Dictionary<int, ulong> numeric,
        PlayerItemSnapshot playerItems,
        bool alternateEquipmentActive)
    {
        ApplySpawnEquipment(
            numeric,
            SelectActiveEquipment(playerItems, HeadPosition, AlternateHeadPosition, alternateEquipmentActive),
            HeadPosition);
        ApplySpawnEquipment(
            numeric,
            SelectActiveEquipment(playerItems, ArmorPosition, AlternateArmorPosition, alternateEquipmentActive),
            ArmorPosition);
        ApplySpawnEquipment(
            numeric,
            SelectActiveEquipment(playerItems, RightWeaponPosition, AlternateRightWeaponPosition, alternateEquipmentActive),
            RightWeaponPosition);
        ApplySpawnEquipment(
            numeric,
            SelectActiveEquipment(playerItems, LeftWeaponPosition, AlternateLeftWeaponPosition, alternateEquipmentActive),
            LeftWeaponPosition);
        ApplySpawnEquipment(
            numeric,
            SelectActiveEquipment(playerItems, BottlePosition, AlternateBottlePosition, alternateEquipmentActive),
            BottlePosition);
        ApplySpawnEquipment(
            numeric,
            SelectActiveEquipment(playerItems, GarmentPosition, AlternateGarmentPosition, alternateEquipmentActive),
            GarmentPosition);
        ApplySpawnEquipment(numeric, SelectEquipment(playerItems, SteedPosition), SteedPosition);
        ApplySpawnEquipment(numeric, SelectEquipment(playerItems, RelicsPosition), RelicsPosition);
        ApplySpawnEquipment(numeric, SelectEquipment(playerItems, RightWeaponAccessoryPosition), RightWeaponAccessoryPosition);
        ApplySpawnEquipment(numeric, SelectEquipment(playerItems, LeftWeaponAccessoryPosition), LeftWeaponAccessoryPosition);
        ApplySpawnEquipment(numeric, SelectEquipment(playerItems, SteedMountPosition), SteedMountPosition);
        ApplySpawnEquipment(numeric, SelectEquipment(playerItems, WingPosition), WingPosition);
    }

    private static PlayerItemSnapshotItem? SelectActiveEquipment(
        PlayerItemSnapshot playerItems,
        ushort basePosition,
        ushort alternatePosition,
        bool alternateEquipmentActive)
    {
        var baseItem = SelectEquipment(playerItems, basePosition);
        if (!alternateEquipmentActive)
        {
            return baseItem;
        }

        return SelectEquipment(playerItems, alternatePosition) ?? baseItem;
    }

    private static PlayerItemSnapshotItem? SelectEquipment(
        PlayerItemSnapshot playerItems,
        ushort position)
    {
        foreach (var item in playerItems.EquipmentItems)
        {
            if (item.Position == position)
            {
                return item;
            }
        }

        return null;
    }

    private static void ApplySpawnEquipment(
        Dictionary<int, ulong> numeric,
        PlayerItemSnapshotItem? item,
        ushort spawnPosition)
    {
        if (item is null)
        {
            return;
        }

        switch (spawnPosition)
        {
            case HeadPosition:
                numeric[HeadField] = item.ItemId;
                numeric[HeadSoulField] = item.PurificationItemId;
                break;
            case ArmorPosition:
                numeric[ArmorField] = item.ItemId;
                numeric[ArmorSoulField] = item.PurificationItemId;
                break;
            case RightWeaponPosition:
                numeric[RightWeaponField] = item.ItemId;
                numeric[RightWeaponSoulField] = item.PurificationItemId;
                break;
            case LeftWeaponPosition:
                numeric[LeftWeaponField] = item.ItemId;
                numeric[LeftWeaponSoulField] = item.PurificationItemId;
                break;
            case BottlePosition:
                SetOptional(numeric, BottleField, item.ItemId);
                break;
            case GarmentPosition:
                numeric[GarmentField] = item.ItemId;
                break;
            case SteedPosition:
                SetOptional(numeric, SteedField, item.ItemId);
                SetOptional(numeric, SteedPlusField, item.Plus);
                SetOptional(numeric, SteedColorField, item.SocketProgress);
                break;
            case RelicsPosition:
                SetOptional(numeric, RelicField, ComputeRelicDisplay(item));
                break;
            case RightWeaponAccessoryPosition:
                SetOptional(numeric, RightWeaponAccessoryField, item.ItemId);
                break;
            case LeftWeaponAccessoryPosition:
                SetOptional(numeric, LeftWeaponAccessoryField, item.ItemId);
                break;
            case SteedMountPosition:
                SetOptional(numeric, MountArmorField, item.ItemId);
                break;
            case WingPosition:
                SetOptional(numeric, WingField, item.ItemId);
                SetOptional(numeric, WingPlusField, item.Plus);
                if (item.Plus == 12)
                {
                    SetOptional(numeric, WingProgressField, item.PlusProgress);
                }

                break;
        }
    }

    private static ulong ComputeRelicDisplay(PlayerItemSnapshotItem item)
    {
        var epicAttributes =
            CountEpicRelicAttribute(item.Attribute1) +
            CountEpicRelicAttribute(item.Attribute2) +
            CountEpicRelicAttribute(item.Attribute3) +
            CountEpicRelicAttribute(item.Attribute4) +
            CountEpicRelicAttribute(item.Attribute5);
        return CheckedUInt64((epicAttributes * 10) + 1, nameof(item.Attribute1));
    }

    private static int CountEpicRelicAttribute(uint value)
    {
        return value != 0 && value % 1000 - value % 100 == 100 ? 1 : 0;
    }

    public static bool TryParse(PacketFrame frame, out LegacyPlayerSpawnPacket packet)
    {
        packet = default!;

        if (frame.Header.Opcode != Opcode)
        {
            return false;
        }

        var reader = new ProtoReader(frame.Payload.ToArray());
        var numeric = new Dictionary<int, ulong>();
        var names = new List<string>(PlayerNameSlots);
        var statusFlags = new List<ulong>(StatusFlagWords);
        var hundredWeapons = new List<uint>(HundredWeaponSlots);

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
                case StatusFlagsField:
                    statusFlags.Add(value);
                    break;
                case HundredWeaponsField:
                    if (value > uint.MaxValue)
                    {
                        return false;
                    }

                    hundredWeapons.Add((uint)value);
                    break;
                default:
                    if (!IsKnownNumericField(fieldNumber) ||
                        (IsBoolField(fieldNumber) && value > 1))
                    {
                        return false;
                    }

                    numeric[fieldNumber] = value;
                    break;
            }
        }

        if (!HasRequiredFields(numeric) ||
            names.Count != PlayerNameSlots ||
            statusFlags.Count != StatusFlagWords ||
            hundredWeapons.Count != HundredWeaponSlots)
        {
            return false;
        }

        packet = new LegacyPlayerSpawnPacket(numeric, names, statusFlags, hundredWeapons);
        return true;
    }

    public byte[] ToFrameBytes()
    {
        var payload = new List<byte>();
        for (var fieldNumber = 1; fieldNumber <= CollectionIdField; fieldNumber++)
        {
            switch (fieldNumber)
            {
                case StatusFlagsField:
                    foreach (var statusFlag in StatusFlags)
                    {
                        WriteVarintField(payload, StatusFlagsField, statusFlag);
                    }

                    break;
                case NamesField:
                    foreach (var name in Names)
                    {
                        WriteString(payload, NamesField, name);
                    }

                    break;
                case HundredWeaponsField:
                    foreach (var weapon in HundredWeapons)
                    {
                        WriteVarintField(payload, HundredWeaponsField, weapon);
                    }

                    break;
                default:
                    if (IsRequiredNumericField(fieldNumber))
                    {
                        WriteVarintField(payload, fieldNumber, _numericFields[fieldNumber]);
                    }
                    else if (IsOptionalNumericField(fieldNumber) &&
                        _numericFields.TryGetValue(fieldNumber, out var optionalValue) &&
                        optionalValue != 0)
                    {
                        WriteVarintField(payload, fieldNumber, optionalValue);
                    }

                    break;
            }
        }

        var writer = new PacketWriter(Opcode, payload.Count);
        payload.CopyTo(writer.Payload);
        return writer.ToArray();
    }

    private static Dictionary<int, ulong> CreateRequiredNumericFields()
    {
        var fields = new Dictionary<int, ulong>();
        foreach (var fieldNumber in RequiredNumericFields)
        {
            fields[fieldNumber] = 0;
        }

        return fields;
    }

    private static string[] CreateNames(CharacterSnapshot snapshot)
    {
        var unionId = ReadAttributeUInt64(snapshot.Attributes, "UnionUID", "UnionID");
        return
        [
            snapshot.Name,
            string.Empty,
            ReadAttributeString(snapshot.Attributes, "ClanName"),
            string.Empty,
            string.Empty,
            ReadAttributeString(snapshot.Attributes, "GuildName"),
            unionId == 0 ? string.Empty : ReadAttributeString(snapshot.Attributes, "UnionName"),
        ];
    }

    private static string[] CreateShadowCloneNames(CharacterSnapshot owner)
    {
        return
        [
            owner.Name,
            string.Empty,
            string.Empty,
            string.Empty,
            $"ShadowClone({owner.Name})",
            string.Empty,
            string.Empty,
        ];
    }

    private static ulong ReadMaxLife(CharacterSnapshot snapshot)
    {
        var maxLife = ReadAttributeUInt64(snapshot.Attributes, "MaxHitpoints", "MaxLife");
        return maxLife != 0 ? maxLife : CheckedUInt64(snapshot.MinHitPoints, nameof(snapshot.MinHitPoints));
    }

    private static ulong BuildMesh(CharacterSnapshot snapshot)
    {
        var transformationId = ReadAttributeInt64(snapshot.Attributes, "TransformationID");
        return CheckedUInt64(
            checked((transformationId * 10_000_000) + (snapshot.Face * 10_000) + snapshot.Body),
            "Mesh");
    }

    private static ulong ReadNobilityRank(CharacterSnapshot snapshot)
    {
        if (snapshot.PaidNobility.TryGetValue("PaidRank", out var numeric) &&
            ulong.TryParse(numeric, out var paidRank))
        {
            return paidRank;
        }

        if (!snapshot.PaidNobility.TryGetValue("Rank", out var rank))
        {
            return 0;
        }

        return rank.Trim() switch
        {
            "Serf" => 0,
            "Knight" => 1,
            "Baron" => 3,
            "Earl" => 5,
            "Duke" => 7,
            "Prince" => 9,
            "King" => 12,
            _ => 0,
        };
    }

    private static void SetOptional(Dictionary<int, ulong> numeric, int fieldNumber, ulong value)
    {
        if (value != 0)
        {
            numeric[fieldNumber] = value;
        }
    }

    private static void SetOptionalIfAbsent(Dictionary<int, ulong> numeric, int fieldNumber, ulong value)
    {
        if (value != 0)
        {
            numeric.TryAdd(fieldNumber, value);
        }
    }

    private static ulong[] ReadVector(
        IReadOnlyDictionary<string, string> attributes,
        int count,
        params string[] keys)
    {
        var values = new ulong[count];
        var raw = ReadAttributeString(attributes, keys);
        if (raw.Length == 0)
        {
            return values;
        }

        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < Math.Min(values.Length, parts.Length); i++)
        {
            if (ulong.TryParse(parts[i], out var parsed))
            {
                values[i] = parsed;
            }
        }

        return values;
    }

    private static uint[] ReadUInt32Vector(
        IReadOnlyDictionary<string, string> attributes,
        int count,
        params string[] keys)
    {
        var values = new uint[count];
        var raw = ReadAttributeString(attributes, keys);
        if (raw.Length == 0)
        {
            return values;
        }

        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = 0; i < Math.Min(values.Length, parts.Length); i++)
        {
            if (uint.TryParse(parts[i], out var parsed))
            {
                values[i] = parsed;
            }
        }

        return values;
    }

    private static ulong ReadAttributeBool(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        var raw = ReadAttributeString(attributes, keys);
        if (raw.Length == 0)
        {
            return 0;
        }

        if (bool.TryParse(raw, out var boolValue))
        {
            return boolValue ? 1UL : 0;
        }

        return long.TryParse(raw, out var numericValue) && numericValue != 0 ? 1UL : 0;
    }

    private static ulong ReadAttributeUInt64(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        var value = ReadAttributeInt64(attributes, keys);
        return value <= 0 ? 0 : (ulong)value;
    }

    private static long ReadAttributeInt64(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value) &&
                long.TryParse(value, out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }

    private static string ReadAttributeString(IReadOnlyDictionary<string, string> attributes, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (attributes.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return string.Empty;
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
