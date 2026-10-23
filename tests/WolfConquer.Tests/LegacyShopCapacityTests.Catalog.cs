using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyShopCapacityTests
{
    private static WorldDataCatalog Catalog()
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.UnixEpoch,
                "fixture://item-catalog",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: [PurchaseType()],
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
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, string.Empty, 0, []),
                [],
                "fixture://activity"),
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
            npcs: [new Npc(3, 1, 10, 1002, 430, 380, null),
                new Npc(4, 1, 10, 1002, 430, 380, null), new Npc(5, 1, 10, 1002, 430, 380, null)],
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
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                string.Empty,
                new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [Shop(3, 0), Shop(4, 1), Shop(5, 1)],
            shopItems: [Listing(3, false), Listing(4, false), Listing(5, true)],
            exchangeShopGoods: [],
            exchangeShopGoodsEx: [],
            exchangeShopLevels: [],
            honorShopItems: [new HonorShopItem((int)RewardId, 10, 0, "fixture", false, null, 0, "owned", "", [])],
            honorShopRecommendations: [],
            racePointShopItems: [PointItem()],
            racePointShopRecommendations: [],
            goldenLeagueShopItems: [PointItem()],
            goldenLeagueShopRecommendations: [],
            goldenLeagueShopConfig: new Dictionary<string, string>(),
            booths: [],
            boothItems: []);
    }

    private static ShopDefinition Shop(int id, int money) =>
        new(id, "OwnedShop", 0, money, "owned", "fixture", new Dictionary<string, string>());

    private static ShopItem Listing(int id, bool bound) =>
        new(id, (int)RewardId, 0, null, null, bound, null, null, [], "owned", "fixture", "",
            new Dictionary<string, string>());

    private static PointShopItem PointItem() =>
        new((int)RewardId, 10, "owned", "fixture", null, "Items", 0, "owned", "", []);

    private static ItemType PurchaseType()
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = RewardId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "OwnedCapacityItem";
        fields[ItemType.GoldWorthFieldIndex] = "5";
        fields[ItemType.ConquerPointsWorthFieldIndex] = "7";
        fields[ItemType.DurabilityFieldIndex] = "1";
        return new((int)RewardId, "OwnedCapacityItem", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name) =>
        new("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
}
