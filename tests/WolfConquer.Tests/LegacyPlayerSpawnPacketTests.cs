using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPlayerSpawnPacketTests
{
    private const int MeshField = 1;
    private const int UidField = 2;
    private const int GuildIdField = 3;
    private const int GuildRankField = 4;
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
    private const int XField = 27;
    private const int YField = 28;
    private const int HairStyleField = 29;
    private const int FacingField = 30;
    private const int ActionField = 31;
    private const int RebornField = 34;
    private const int LevelField = 35;
    private const int WindowSpawnField = 36;
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
    private const int HeadSoulField = 64;
    private const int ArmorSoulField = 65;
    private const int LeftWeaponSoulField = 66;
    private const int RightWeaponSoulField = 67;
    private const int UnknownBoolField = 108;
    private const int Test1Field = 112;
    private const int Test2Field = 113;
    private const int CollectionIdField = 121;

    [Fact]
    public void LegacyPlayerSpawnOpcodeMatchesLegacyGetArrayFinalize()
    {
        Assert.Equal(WorldPacketOpcodes.LegacyRoleSpawn, LegacyPlayerSpawnPacket.Opcode);
    }

    [Fact]
    public void LegacyPlayerSpawnFromSnapshotUsesLegacyFieldSources()
    {
        var snapshot = CreateSnapshot(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["TransformationID"] = "2",
                ["GuildID"] = "1370",
                ["GuildRank"] = "1",
                ["AparenceType"] = "3",
                ["Angle"] = "6",
                ["Action"] = "100",
                ["MaxHitpoints"] = "70000",
                ["FlowerRank"] = "7",
                ["QuizPoints"] = "12",
                ["Enilghten"] = "200",
                ["InnerStrengthScore"] = "3456",
                ["ClanID"] = "329",
                ["ClanRank"] = "100",
                ["MyTitle"] = "4",
                ["PokerSeat"] = "2",
                ["PokerTableID"] = "102130",
                ["ExtraBattlePower"] = "20",
                ["GuildBattlePower"] = "5",
                ["RaceItem"] = "True",
                ["SubClassHashPoint"] = "777",
                ["ActiveSublass"] = "6",
                ["CountryID"] = "17",
                ["Team"] = "2",
                ["BattlePower"] = "321",
                ["JiangHuTalent"] = "9",
                ["JiangHuActive"] = "1",
                ["OwnerPet"] = "10",
                ["OwnerPet1"] = "20",
                ["RealUID"] = "1293600",
                ["UnionUID"] = "1370",
                ["KingDomExploits"] = "44",
                ["ExploitsRank"] = "8",
                ["UnionRank"] = "1000",
                ["UnionType"] = "1",
                ["SpecialTitleID"] = "6012",
                ["SpecialTitleScore"] = "123",
                ["SpecialWingID"] = "2092",
                ["MainFlag"] = "2",
                ["Relic"] = "11",
                ["StatusFlags"] = "1/2/3/4/5/6/7/8/9/10/",
                ["HundredWeapons"] = "11/22/33/44/55/",
                ["ClanName"] = "LegacyClan",
                ["GuildName"] = "LegacyGuild",
                ["UnionName"] = "LegacyUnion",
                ["CollectionID"] = "4300000"
            },
            paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["PaidRank"] = "9"
            });

        var packet = LegacyPlayerSpawnPacket.FromSnapshot(snapshot, serverId: 7, windowSpawn: true);

        Assert.Equal(21_541_008UL, packet.GetNumericField(MeshField));
        Assert.Equal(1_293_600UL, packet.GetNumericField(UidField));
        Assert.Equal(1370UL, packet.GetNumericField(GuildIdField));
        Assert.Equal(1UL, packet.GetNumericField(GuildRankField));
        Assert.Equal(3UL, packet.GetNumericField(AppearanceTypeField));
        Assert.Equal(61_258UL, packet.GetNumericField(HitpointsField));
        Assert.Equal(70_000UL, packet.GetNumericField(MaxLifeField));
        Assert.Equal(830UL, packet.GetNumericField(XField));
        Assert.Equal(584UL, packet.GetNumericField(YField));
        Assert.Equal(27UL, packet.GetNumericField(HairStyleField));
        Assert.Equal(6UL, packet.GetNumericField(FacingField));
        Assert.Equal(100UL, packet.GetNumericField(ActionField));
        Assert.Equal(2UL, packet.GetNumericField(RebornField));
        Assert.Equal(140UL, packet.GetNumericField(LevelField));
        Assert.Equal(1UL, packet.GetNumericField(WindowSpawnField));
        Assert.Equal(15UL, packet.GetNumericField(ExtraBattlePowerField));
        Assert.Equal(10_007UL, packet.GetNumericField(FlowerIconField));
        Assert.Equal(9UL, packet.GetNumericField(NobilityRankField));
        Assert.Equal(12UL, packet.GetNumericField(QuizPointsField));
        Assert.Equal(200UL, packet.GetNumericField(EnlightenField));
        Assert.Equal(3456UL, packet.GetNumericField(InnerStrengthScoreField));
        Assert.Equal(329UL, packet.GetNumericField(ClanUidField));
        Assert.Equal(100UL, packet.GetNumericField(ClanRankField));
        Assert.Equal(4UL, packet.GetNumericField(TitleField));
        Assert.Equal(2UL, packet.GetNumericField(PokerSeatField));
        Assert.Equal(102_130UL, packet.GetNumericField(PokerTableIdField));
        Assert.Equal(5UL, packet.GetNumericField(GuildBattlePowerField));
        Assert.Equal(1UL, packet.GetNumericField(RaceItemField));
        Assert.Equal(777UL, packet.GetNumericField(SubClassField));
        Assert.Equal(6UL, packet.GetNumericField(ActiveSubClassesField));
        Assert.Equal(0UL, packet.GetNumericField(FirstRebornClassField));
        Assert.Equal(7050UL, packet.GetNumericField(SecondRebornClassField));
        Assert.Equal(7051UL, packet.GetNumericField(ClassField));
        Assert.Equal(17UL, packet.GetNumericField(CountryCodeField));
        Assert.Equal(2UL, packet.GetNumericField(TeamField));
        Assert.Equal(321UL, packet.GetNumericField(BattlePowerField));
        Assert.Equal(9UL, packet.GetNumericField(JiangHuTalentField));
        Assert.Equal(1UL, packet.GetNumericField(JiangHuActiveField));
        Assert.Equal(7UL, packet.GetNumericField(ServerIdField));
        Assert.Equal(10UL, packet.GetNumericField(OwnerPetField));
        Assert.Equal(20UL, packet.GetNumericField(OwnerPet1Field));
        Assert.Equal(1_293_600UL, packet.GetNumericField(OwnerUidField));
        Assert.Equal(1370UL, packet.GetNumericField(UnionIdField));
        Assert.Equal(44UL, packet.GetNumericField(UnionExploitsField));
        Assert.Equal(8UL, packet.GetNumericField(OfficialHaremGuardsField));
        Assert.Equal(1000UL, packet.GetNumericField(UnionRankField));
        Assert.Equal(1UL, packet.GetNumericField(UnionTypeField));
        Assert.Equal(6012UL, packet.GetNumericField(MyTitleField));
        Assert.Equal(123UL, packet.GetNumericField(MyTitleScoreField));
        Assert.Equal(2092UL, packet.GetNumericField(MyWingField));
        Assert.Equal(2UL, packet.GetNumericField(MainFlagField));
        Assert.Equal(11UL, packet.GetNumericField(RelicField));
        Assert.Equal(1UL, packet.GetNumericField(UnknownBoolField));
        Assert.Equal(1UL, packet.GetNumericField(Test1Field));
        Assert.Equal(1UL, packet.GetNumericField(Test2Field));
        Assert.Equal(4_300_000UL, packet.GetNumericField(CollectionIdField));
        Assert.Equal(["HeroOne", string.Empty, "LegacyClan", string.Empty, string.Empty, "LegacyGuild", "LegacyUnion"], packet.Names);
        Assert.Equal([1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL, 9UL, 10UL], packet.StatusFlags);
        Assert.Equal([11U, 22U, 33U, 44U, 55U], packet.HundredWeapons);
    }

    [Fact]
    public void LegacyPlayerSpawnPacketRoundTripsSnapshotProjection()
    {
        var packet = LegacyPlayerSpawnPacket.FromSnapshot(
            CreateSnapshot(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["TransformationID"] = "2",
                    ["StatusFlags"] = "1/0/0/0/0/0/0/0/0/2/",
                    ["HundredWeapons"] = "100/200/"
                },
                paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Rank"] = "Prince"
                }),
            serverId: 2);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(LegacyPlayerSpawnPacket.Opcode, frame.Header.Opcode);
        Assert.True(LegacyPlayerSpawnPacket.TryParse(frame, out var parsed));

        Assert.Equal(packet.GetNumericField(MeshField), parsed.GetNumericField(MeshField));
        Assert.Equal(packet.GetNumericField(UidField), parsed.GetNumericField(UidField));
        Assert.Equal(packet.GetNumericField(HitpointsField), parsed.GetNumericField(HitpointsField));
        Assert.Equal(packet.GetNumericField(MaxLifeField), parsed.GetNumericField(MaxLifeField));
        Assert.Equal(packet.GetNumericField(FlowerIconField), parsed.GetNumericField(FlowerIconField));
        Assert.Equal(packet.GetNumericField(NobilityRankField), parsed.GetNumericField(NobilityRankField));
        Assert.Equal(packet.GetNumericField(BattlePowerField), parsed.GetNumericField(BattlePowerField));
        Assert.Equal(packet.GetNumericField(UnknownBoolField), parsed.GetNumericField(UnknownBoolField));
        Assert.Equal(packet.GetNumericField(Test1Field), parsed.GetNumericField(Test1Field));
        Assert.Equal(packet.GetNumericField(Test2Field), parsed.GetNumericField(Test2Field));
        Assert.Equal(packet.Names, parsed.Names);
        Assert.Equal(packet.StatusFlags, parsed.StatusFlags);
        Assert.Equal([100U, 200U, 0U, 0U, 0U], parsed.HundredWeapons);
    }

    [Fact]
    public void LegacyPlayerSpawnFromShadowCloneUsesLegacyCloneProjectionFields()
    {
        var owner = CreateSnapshot(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Angle"] = "5",
                ["OwnerPet"] = "99",
                ["OwnerPet1"] = "88",
                ["RealUID"] = "777",
                ["StatusFlags"] = "1/2/3/4/5/6/7/8/9/10/",
            },
            paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 120269, position: 3, purificationItemId: 800003),
                Equipment(itemId: 500109, position: 4, purificationItemId: 800004),
            ],
            RuneItems: []);

        var packet = LegacyPlayerSpawnPacket.FromShadowClone(
            owner,
            cloneUid: 9_001_234,
            cloneFlag: 10003,
            playerItems,
            serverId: 6);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.True(LegacyPlayerSpawnPacket.TryParse(frame, out var parsed));
        Assert.Equal(9_001_234UL, parsed.GetNumericField(UidField));
        Assert.Equal(1_293_600UL, parsed.GetNumericField(OwnerUidField));
        Assert.Equal(2UL, parsed.GetNumericField(OwnerPetField));
        Assert.Equal(10_003UL, parsed.GetNumericField(OwnerPet1Field));
        Assert.Equal(830UL, parsed.GetNumericField(XField));
        Assert.Equal(584UL, parsed.GetNumericField(YField));
        Assert.Equal(5UL, parsed.GetNumericField(FacingField));
        Assert.Equal("HeroOne", parsed.Names[0]);
        Assert.Equal("ShadowClone(HeroOne)", parsed.Names[4]);
        Assert.Equal(
            ["HeroOne", string.Empty, string.Empty, string.Empty, "ShadowClone(HeroOne)", string.Empty, string.Empty],
            parsed.Names);
        Assert.Equal(new ulong[10], parsed.StatusFlags);
        Assert.Equal(1_541_008UL, parsed.GetNumericField(MeshField));
        Assert.Equal(120_269UL, parsed.GetNumericField(ArmorField));
        Assert.Equal(500_109UL, parsed.GetNumericField(RightWeaponField));
        Assert.Equal(6UL, parsed.GetNumericField(ServerIdField));
    }

    [Fact]
    public void LegacyPlayerSpawnFromSnapshotUsesLoadedEquipmentSpawnFields()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 111003, position: 1, purificationItemId: 800001),
                Equipment(itemId: 120269, position: 3, purificationItemId: 800003),
                Equipment(itemId: 500109, position: 4, purificationItemId: 800004),
                Equipment(itemId: 900109, position: 5, purificationItemId: 800005),
                Equipment(itemId: 210002, position: 7),
                Equipment(itemId: 181525, position: 9),
                Equipment(itemId: 300000, position: 12, plus: 6, socketProgress: 123456),
                Equipment(itemId: 360001, position: 15),
                Equipment(itemId: 360002, position: 16),
                Equipment(itemId: 200009, position: 17),
                Equipment(itemId: 204003, position: 19, plus: 12, plusProgress: 654321),
            ],
            RuneItems: []);

        var packet = LegacyPlayerSpawnPacket.FromSnapshot(
            CreateSnapshot(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            playerItems);

        Assert.Equal(111_003UL, packet.GetNumericField(HeadField));
        Assert.Equal(181_525UL, packet.GetNumericField(GarmentField));
        Assert.Equal(120_269UL, packet.GetNumericField(ArmorField));
        Assert.Equal(900_109UL, packet.GetNumericField(LeftWeaponField));
        Assert.Equal(500_109UL, packet.GetNumericField(RightWeaponField));
        Assert.Equal(360_002UL, packet.GetNumericField(LeftWeaponAccessoryField));
        Assert.Equal(360_001UL, packet.GetNumericField(RightWeaponAccessoryField));
        Assert.Equal(300_000UL, packet.GetNumericField(SteedField));
        Assert.Equal(200_009UL, packet.GetNumericField(MountArmorField));
        Assert.Equal(204_003UL, packet.GetNumericField(WingField));
        Assert.Equal(12UL, packet.GetNumericField(WingPlusField));
        Assert.Equal(654_321UL, packet.GetNumericField(WingProgressField));
        Assert.Equal(210_002UL, packet.GetNumericField(BottleField));
        Assert.Equal(6UL, packet.GetNumericField(SteedPlusField));
        Assert.Equal(123_456UL, packet.GetNumericField(SteedColorField));
        Assert.Equal(800_001UL, packet.GetNumericField(HeadSoulField));
        Assert.Equal(800_003UL, packet.GetNumericField(ArmorSoulField));
        Assert.Equal(800_005UL, packet.GetNumericField(LeftWeaponSoulField));
        Assert.Equal(800_004UL, packet.GetNumericField(RightWeaponSoulField));
    }

    [Fact]
    public void LegacyPlayerSpawnFromSnapshotUsesRuntimeAlternateEquipmentSlotsWhenActive()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 111003, position: 1, purificationItemId: 800001),
                Equipment(itemId: 120269, position: 3, purificationItemId: 800003),
                Equipment(itemId: 500109, position: 4, purificationItemId: 800004),
                Equipment(itemId: 900109, position: 5, purificationItemId: 800005),
                Equipment(itemId: 210002, position: 7),
                Equipment(itemId: 181525, position: 9),
                Equipment(itemId: 112003, position: 51, purificationItemId: 810001),
                Equipment(itemId: 130269, position: 53, purificationItemId: 810003),
                Equipment(itemId: 601109, position: 54, purificationItemId: 810004),
                Equipment(itemId: 910109, position: 55, purificationItemId: 810005),
                Equipment(itemId: 220002, position: 57),
                Equipment(itemId: 182525, position: 59),
            ],
            RuneItems: []);

        var snapshot = CreateSnapshot(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        var inactive = LegacyPlayerSpawnPacket.FromSnapshot(snapshot, playerItems);
        var active = LegacyPlayerSpawnPacket.FromSnapshot(
            snapshot,
            playerItems,
            alternateEquipmentActive: true);

        Assert.Equal(111_003UL, inactive.GetNumericField(HeadField));
        Assert.Equal(120_269UL, inactive.GetNumericField(ArmorField));
        Assert.Equal(900_109UL, inactive.GetNumericField(LeftWeaponField));
        Assert.Equal(500_109UL, inactive.GetNumericField(RightWeaponField));
        Assert.Equal(210_002UL, inactive.GetNumericField(BottleField));
        Assert.Equal(181_525UL, inactive.GetNumericField(GarmentField));
        Assert.Equal(800_001UL, inactive.GetNumericField(HeadSoulField));
        Assert.Equal(800_003UL, inactive.GetNumericField(ArmorSoulField));
        Assert.Equal(800_005UL, inactive.GetNumericField(LeftWeaponSoulField));
        Assert.Equal(800_004UL, inactive.GetNumericField(RightWeaponSoulField));

        Assert.Equal(112_003UL, active.GetNumericField(HeadField));
        Assert.Equal(130_269UL, active.GetNumericField(ArmorField));
        Assert.Equal(910_109UL, active.GetNumericField(LeftWeaponField));
        Assert.Equal(601_109UL, active.GetNumericField(RightWeaponField));
        Assert.Equal(220_002UL, active.GetNumericField(BottleField));
        Assert.Equal(182_525UL, active.GetNumericField(GarmentField));
        Assert.Equal(810_001UL, active.GetNumericField(HeadSoulField));
        Assert.Equal(810_003UL, active.GetNumericField(ArmorSoulField));
        Assert.Equal(810_005UL, active.GetNumericField(LeftWeaponSoulField));
        Assert.Equal(810_004UL, active.GetNumericField(RightWeaponSoulField));
    }

    [Fact]
    public void LegacyPlayerSpawnFromSnapshotComputesRelicDisplayFromEpicAttributes()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(
                    itemId: 4100001,
                    position: 13,
                    attribute1: 50_107,
                    attribute2: 50_007,
                    attribute3: 90_114),
            ],
            RuneItems: []);

        var packet = LegacyPlayerSpawnPacket.FromSnapshot(
            CreateSnapshot(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Relic"] = "99",
                },
                paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            playerItems);

        Assert.Equal(21UL, packet.GetNumericField(RelicField));
    }

    [Fact]
    public void LegacyPlayerSpawnFromSnapshotKeepsRelicAttributeFallbackWithoutEquippedRelic()
    {
        var packet = LegacyPlayerSpawnPacket.FromSnapshot(
            CreateSnapshot(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Relic"] = "31",
                },
                paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItemSnapshot.Empty);

        Assert.Equal(31UL, packet.GetNumericField(RelicField));
    }

    [Fact]
    public void LegacyPlayerSpawnFromSnapshotLetsSpecialGarmentOverrideEquippedGarment()
    {
        var playerItems = new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                Equipment(itemId: 181525, position: 9),
            ],
            RuneItems: []);

        var packet = LegacyPlayerSpawnPacket.FromSnapshot(
            CreateSnapshot(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["SpecialGarment"] = "182675",
                },
                paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            playerItems);

        Assert.Equal(182_675UL, packet.GetNumericField(GarmentField));
    }

    [Fact]
    public void LegacyPlayerSpawnPacketWritesSparseSnapshotDefaults()
    {
        var packet = LegacyPlayerSpawnPacket.FromSnapshot(
            CreateSnapshot(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            serverId: 2);

        Assert.Equal(1_541_008UL, packet.GetNumericField(MeshField));
        Assert.Equal(61_258UL, packet.GetNumericField(HitpointsField));
        Assert.Equal(61_258UL, packet.GetNumericField(MaxLifeField));
        Assert.Equal(10_000UL, packet.GetNumericField(FlowerIconField));
        Assert.Equal(150UL, packet.GetNumericField(BattlePowerField));
        Assert.Equal(2UL, packet.GetNumericField(ServerIdField));
        Assert.Equal(1UL, packet.GetNumericField(UnknownBoolField));
        Assert.Equal(1UL, packet.GetNumericField(Test1Field));
        Assert.Equal(1UL, packet.GetNumericField(Test2Field));
        Assert.Equal(["HeroOne", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty], packet.Names);
        Assert.Equal(new ulong[10], packet.StatusFlags);
        Assert.Equal(new uint[5], packet.HundredWeapons);
    }

    [Fact]
    public void LegacyPlayerSpawnFromSnapshotUsesRuntimeMaxLifeOverride()
    {
        var packet = LegacyPlayerSpawnPacket.FromSnapshot(
            CreateSnapshot(
                attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["MaxHitpoints"] = "100",
                },
                paidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            maxLife: 70_500);

        Assert.Equal(61_258UL, packet.GetNumericField(HitpointsField));
        Assert.Equal(70_500UL, packet.GetNumericField(MaxLifeField));
    }

    [Fact]
    public void LegacyPlayerSpawnPacketRejectsWrongOpcode()
    {
        var writer = new PacketWriter(LegacyPlayerSpawnPacket.Opcode + 1, payloadLength: 0);
        var frame = ReadFrame(writer.ToArray());

        Assert.False(LegacyPlayerSpawnPacket.TryParse(frame, out _));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static CharacterSnapshot CreateSnapshot(
        IReadOnlyDictionary<string, string> attributes,
        IReadOnlyDictionary<string, string> paidNobility)
    {
        return new CharacterSnapshot(
            Uid: 1_293_600,
            Name: "HeroOne",
            Body: 1008,
            Face: 154,
            Hair: 27,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 7050,
            Reborn: 2,
            Level: 140,
            Experience: 0,
            MapId: 1015,
            X: 830,
            Y: 584,
            PreviousMapId: 1000,
            PreviousMapX: 500,
            PreviousMapY: 650,
            Strength: 0,
            Agility: 36,
            Vitality: 447,
            Spirit: 0,
            AttributePoints: 12,
            MinHitPoints: 61_258,
            MinMana: 5_000,
            Money: 98_602_439_956,
            ConquerPoints: 639_778_904,
            BoundConquerPoints: 456,
            BankConquerPoints: 123,
            PkPoints: 0,
            Online: 7,
            SourceFile: "1293600.ini",
            Attributes: attributes,
            PaidNobility: paidNobility);
    }

    private static PlayerItemSnapshotItem Equipment(
        uint itemId,
        ushort position,
        byte plus = 0,
        uint plusProgress = 0,
        uint socketProgress = 0,
        uint purificationItemId = 0,
        uint attribute1 = 0,
        uint attribute2 = 0,
        uint attribute3 = 0,
        uint attribute4 = 0,
        uint attribute5 = 0)
    {
        return new PlayerItemSnapshotItem
        {
            ItemId = itemId,
            Position = position,
            Plus = plus,
            PlusProgress = plusProgress,
            SocketProgress = socketProgress,
            PurificationItemId = purificationItemId,
            Attribute1 = attribute1,
            Attribute2 = attribute2,
            Attribute3 = attribute3,
            Attribute4 = attribute4,
            Attribute5 = attribute5,
        };
    }
}
