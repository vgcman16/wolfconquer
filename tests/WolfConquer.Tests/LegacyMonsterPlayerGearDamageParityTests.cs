using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterPlayerGearDamageParityTests
{
    [Fact]
    public void SyntheticArmorBlessAndTortoiseFieldsReduceSelectedMonsterPhysicalHit()
    {
        var catalog = CreateCatalog(
            items:
            [
                CreateItemType(
                    id: 130_003,
                    name: "SyntheticArmor",
                    physicalDefense: 500),
            ]);
        var ungearedStatus = LegacyPlayerStatusCalculator.Create(
            CreateSnapshot(),
            PlayerItemSnapshot.Empty,
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);
        var gearedStatus = LegacyPlayerStatusCalculator.Create(
            CreateSnapshot(),
            Equipped(
            [
                Equipment(
                    itemId: 130_003,
                    position: 3,
                    bless: 7,
                    socketOne: 73,
                    socketTwo: 72),
            ]),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);

        var ungearedDamage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttackWithStatusFields(
            selectedAttack: 2_000,
            targetStatusFields: ungearedStatus.Fields);
        var gearedDamage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttackWithStatusFields(
            selectedAttack: 2_000,
            targetStatusFields: gearedStatus.Fields);

        Assert.Equal(500U, gearedStatus.Defence);
        Assert.Equal(7U, gearedStatus.ItemBless);
        Assert.Equal(10U, gearedStatus.Damage);
        Assert.Equal(2_000U, ungearedDamage);
        Assert.Equal(1_310U, gearedDamage);
        Assert.True(gearedDamage < ungearedDamage);
    }

    [Fact]
    public void SyntheticArmorDefenseFieldsFloorSelectedMonsterPhysicalHitToOne()
    {
        var catalog = CreateCatalog(
            items:
            [
                CreateItemType(
                    id: 130_003,
                    name: "SyntheticArmor",
                    physicalDefense: 1_200),
            ]);
        var gearedStatus = LegacyPlayerStatusCalculator.Create(
            CreateSnapshot(),
            Equipped(
            [
                Equipment(itemId: 130_003, position: 3),
            ]),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);

        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttackWithStatusFields(
            selectedAttack: 1_000,
            targetStatusFields: gearedStatus.Fields);

        Assert.Equal(1_200U, gearedStatus.Defence);
        Assert.Equal(1U, damage);
    }

    [Fact]
    public void SyntheticArmorSourceDefenseStatesCanFloorMonsterPhysicalHitToOne()
    {
        var catalog = CreateCatalog(
            items:
            [
                CreateItemType(
                    id: 130_003,
                    name: "SyntheticArmor",
                    physicalDefense: 999),
            ]);
        var gearedStatus = LegacyPlayerStatusCalculator.Create(
            CreateSnapshot(),
            Equipped(
            [
                Equipment(
                    itemId: 130_003,
                    position: 3,
                    socketOne: 73,
                    socketTwo: 72),
            ]),
            catalog,
            maxHitpoints: 100,
            prestigeLevel: 0);

        var damage = LegacyMonsterPlayerPhysicalDamageCalculator.CalculateFromSelectedAttackWithStatusFields(
            selectedAttack: 3_400,
            targetStatusFields: gearedStatus.Fields,
            playerDefenseAdjustments: new LegacyPhysicalDefenseAdjustments(
                MagicShieldOrDefensePotion: true,
                DragonSwing: true,
                DivineGuard: true));

        Assert.Equal(999U, gearedStatus.Defence);
        Assert.Equal(10U, gearedStatus.Damage);
        Assert.Equal(1U, damage);
    }

    private static PlayerItemSnapshot Equipped(IReadOnlyList<PlayerItemSnapshotItem> equipment)
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems: equipment,
            RuneItems: []);
    }

    private static PlayerItemSnapshotItem Equipment(
        uint itemId,
        ushort position,
        byte bless = 0,
        byte socketOne = 0,
        byte socketTwo = 0)
    {
        return new PlayerItemSnapshotItem
        {
            ItemId = itemId,
            Durability = 100,
            Position = position,
            Bless = bless,
            SocketOne = socketOne,
            SocketTwo = socketTwo,
        };
    }

    private static CharacterSnapshot CreateSnapshot()
    {
        return new CharacterSnapshot(
            Uid: 1,
            Name: "GearDamageParity",
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
            SourceFile: "gear-damage-parity.ini",
            Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static ItemType CreateItemType(int id, string name, uint physicalDefense)
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = id.ToString();
        fields[1] = name;
        fields[15] = physicalDefense.ToString();
        return new ItemType(id, name, fields);
    }

    private static WorldDataCatalog CreateCatalog(IReadOnlyList<ItemType> items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-10T00:00:00Z"),
                "fixture://legacy-monster-player-gear-damage",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: [],
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
