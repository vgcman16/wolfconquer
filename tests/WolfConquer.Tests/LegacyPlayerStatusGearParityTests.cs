using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPlayerStatusGearParityTests
{
    [Fact]
    public void EquippedItemTypeBaseFieldsFlowIntoStatusFields()
    {
        var catalog = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 111003,
                    maxAttack: 123,
                    minAttack: 45,
                    physicalDefence: 67,
                    magicAttack: 89,
                    magicDefence: 201),
            ]);
        var playerItems = EquipmentSnapshot(Equipment(itemId: 111003, position: 1));

        var fields = Calculate(playerItems, catalog);

        Assert.Equal(124U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(45U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(67U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(90U, fields[LegacyPlayerStatusPacket.MagicAttackIndex]);
        Assert.Equal(201U, fields[LegacyPlayerStatusPacket.MDefenceIndex]);
    }

    [Fact]
    public void EquippedItemTypeManaFieldFlowsIntoStatusMaxMana()
    {
        var catalog = CreateCatalog(
            items:
            [
                ItemTypeDefinition(
                    itemTypeId: 111003,
                    itemMP: 789),
            ]);
        var playerItems = EquipmentSnapshot(Equipment(itemId: 111003, position: 1));

        var fields = Calculate(playerItems, catalog);

        Assert.Equal(5_789U, fields[LegacyPlayerStatusPacket.MaxManaIndex]);
    }

    [Fact]
    public void SpecialGearPositionsRouteBaseItemFieldsLikeLegacy()
    {
        var catalog = CreateCatalog(
            items:
            [
                ItemTypeDefinition(itemTypeId: 201003, maxAttack: 300, minAttack: 111, magicAttack: 40),
                ItemTypeDefinition(itemTypeId: 202003, physicalDefence: 70, magicDefence: 80),
                ItemTypeDefinition(itemTypeId: 900003, maxAttack: 101, minAttack: 41, magicAttack: 21),
            ]);
        var playerItems = EquipmentSnapshot(
            Equipment(itemId: 201003, position: 10),
            Equipment(itemId: 202003, position: 11),
            Equipment(itemId: 900003, position: 5));

        var fields = Calculate(playerItems, catalog);

        Assert.Equal(51U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(20U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(11U, fields[LegacyPlayerStatusPacket.MagicAttackIndex]);
        Assert.Equal(300U, fields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex]);
        Assert.Equal(40U, fields[LegacyPlayerStatusPacket.MagicDamageIncreaseIndex]);
        Assert.Equal(70U, fields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
        Assert.Equal(80U, fields[LegacyPlayerStatusPacket.MagicDamageDecreaseIndex]);
    }

    [Fact]
    public void ItemAddBonusUsesBaseItemTypeIdWhenPlusIsApplied()
    {
        var catalog = CreateCatalog(
            items:
            [
                ItemTypeDefinition(itemTypeId: 111003),
            ],
            itemAddBonuses:
            [
                ItemAddBonusDefinition(
                    itemTypeId: 111000,
                    level: 3,
                    minAttack: 7,
                    maxAttack: 11,
                    physicalDefence: 13,
                    magicAttack: 17,
                    magicDefence: 19,
                    dodge: 23),
            ]);
        var playerItems = EquipmentSnapshot(Equipment(itemId: 111003, position: 1, plus: 3));

        var fields = Calculate(playerItems, catalog);

        Assert.Equal(12U, fields[LegacyPlayerStatusPacket.MaxAttackIndex]);
        Assert.Equal(7U, fields[LegacyPlayerStatusPacket.MinAttackIndex]);
        Assert.Equal(13U, fields[LegacyPlayerStatusPacket.DefenceIndex]);
        Assert.Equal(18U, fields[LegacyPlayerStatusPacket.MagicAttackIndex]);
        Assert.Equal(19U, fields[LegacyPlayerStatusPacket.MagicDefenceIndex]);
        Assert.Equal(23U, fields[LegacyPlayerStatusPacket.DodgeIndex]);
    }

    private static uint[] Calculate(PlayerItemSnapshot playerItems, WorldDataCatalog catalog)
    {
        return LegacyPlayerStatusCalculator.CalculateFields(
            CreateSnapshot(),
            playerItems,
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);
    }

    private static PlayerItemSnapshot EquipmentSnapshot(params PlayerItemSnapshotItem[] equipment)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: equipment,
            RuneItems: []);
    }

    private static PlayerItemSnapshotItem Equipment(uint itemId, ushort position, byte plus = 0)
    {
        return new PlayerItemSnapshotItem
        {
            ItemId = itemId,
            Durability = 100,
            Position = position,
            Plus = plus,
        };
    }

    private static CharacterSnapshot CreateSnapshot()
    {
        return new CharacterSnapshot(
            Uid: 1,
            Name: "GearStatusProof",
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
            MinHitPoints: 100,
            MinMana: 5_000,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 7,
            SourceFile: "gear-status-proof.ini",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static ItemType ItemTypeDefinition(
        int itemTypeId,
        int maxAttack = 0,
        int minAttack = 0,
        int physicalDefence = 0,
        int magicAttack = 0,
        int magicDefence = 0,
        int itemMP = 0)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = itemTypeId.ToString();
        fields[1] = "GearStatusProof";
        fields[13] = maxAttack.ToString();
        fields[14] = minAttack.ToString();
        fields[15] = physicalDefence.ToString();
        fields[19] = itemMP.ToString();
        fields[29] = magicAttack.ToString();
        fields[30] = magicDefence.ToString();

        return new ItemType(itemTypeId, fields[1], fields);
    }

    private static ItemAddBonus ItemAddBonusDefinition(
        int itemTypeId,
        int level,
        int minAttack = 0,
        int maxAttack = 0,
        int physicalDefence = 0,
        int magicAttack = 0,
        int magicDefence = 0,
        int dodge = 0)
    {
        return new ItemAddBonus(
            itemTypeId,
            level,
            [0, minAttack, maxAttack, physicalDefence, magicAttack, magicDefence, 0, dodge],
            SourceLine: 1,
            RawLine: "fixture://item-add");
    }

    private static WorldDataCatalog CreateCatalog(
        IReadOnlyList<ItemType> items,
        IReadOnlyList<ItemAddBonus>? itemAddBonuses = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-12T00:00:00Z"),
                "fixture://legacy-player-status-gear",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: itemAddBonuses ?? [],
            levelExperience: [],
            runeLevelExperience: [],
            runeStorageAttributes: [],
            itemRefineEffects: [],
            itemRefineEffectEx: [],
            itemRefineUpgrades: [],
            transformInfo: [],
            furniture: [],
            hairColorTypes: [],
            hairFaceStorageTypes: [],
            coatStorageTypes: [],
            coatStorageAttributes: [],
            coatColorRules: [],
            xuanbaoAdditionAttributes: [],
            xuanbaoComposeAttributeLimits: [],
            xuanbaoRandomAttributes: [],
            christmasNpcs: [],
            tutorTypes: [],
            tutorBattleLimitTypes: [],
            beastAttributes: [],
            fateExperience: [],
            spiritRates: [],
            hundredWeapons: [],
            gouyuTypes: [],
            gouyuImmortals: [],
            specialEffectAttributes: [],
            daoqiDictTypes: [],
            syndicateFormationTypes: [],
            syndicateSkillTypes: [],
            disdainRules: [],
            jiangHuAttributes: [],
            jiangHuCultivateStatuses: [],
            refineryBoxes: [],
            refinerySouls: [],
            refineryMaterials: [],
            stateCountFiles: [],
            binarySeedFiles: [],
            emptySeedFiles: [],
            backupSeedFiles: [],
            pokerTables: [],
            rouletteTables: [],
            npcTimeEntries: [],
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
            meltingTypeRules: [],
            lotteryPrizes: [],
            arenaRecords: [],
            elitePkRecords: [],
            teamElitePkRecords: [],
            skillTeamPkRecords: [],
            classPkWarRecords: [],
            combatGearRanks: [],
            prestigeRanks: [],
            innerPowerRanks: [],
            runeRankings: [],
            staticStatues: [],
            newPlayerRewards: [],
            votes: [],
            couplesPkRecords: [],
            socialAssociations: [],
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [],
            honorShopRecommendations: [],
            racePointShopItems: [],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }
}
