using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class MsgItemUsageNonEquipmentItemUseTests
{
    private static IReadOnlyList<RuneRankingRecord> RuneRankFullSeed() =>
        Enumerable.Range(0, 99)
            .Select(index => RuneRankSeed(7_000_000 + index, 11_000 + index))
            .Append(RuneRankSeed(RuneRankPlayerB, 10_000))
            .ToArray();

    private static RuneRankingRecord RuneRankSeed(long uid, int score, string name = "Demonic")
    {
        var fields = new[]
        {
            name, score.ToString(CultureInfo.InvariantCulture),
            uid.ToString(CultureInfo.InvariantCulture), "",
        };
        return new RuneRankingRecord(
            name, score, checked((int)uid), "fixture://rune-ranking/RuneRanking.txt",
            LineNumber: 2, RawLine: string.Join('/', fields), Fields: fields);
    }

    private static WorldDataCatalog CreateRuneRankingCatalog(IReadOnlyList<RuneRankingRecord> seed)
    {
        // One stack-one material worth 500 raises family 402 level 14 to 16: (300-100)+(600-300).
        // Level 17 is present for the loop's final next-delta read after reaching 16/EXP 0.
        var runeLevelExperience = new[]
        {
            CreateRuneComposePrefixExperience(1, 402, 14, 100),
            CreateRuneComposePrefixExperience(2, 402, 15, 300),
            CreateRuneComposePrefixExperience(3, 402, 16, 600),
            CreateRuneComposePrefixExperience(4, 402, 17, 1_000),
            CreateRuneComposePrefixExperience(5, 404, 1, 500),
        };
        var items = new[]
        {
            CreateItemType(RuneRankTargetId, "RankBlue14", durability: 100),
            CreateItemType(RuneRankTargetId + 1, "RankBlue15", durability: 100),
            CreateItemType(RuneRankTargetId + 2, "RankBlue16", durability: 100),
            CreateItemType(RuneRankTargetId + 3, "RankBlue17Publication", durability: 100),
            CreateItemType(RuneRankStoredId, "RankStoredBlue15", durability: 100),
            CreateItemType(RuneRankMaterialId, "RankEssence", durability: 100),
        };
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://non-equipment-item-use",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
            itemAddBonuses: [],
            levelExperience: [],
            runeLevelExperience: runeLevelExperience,
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
            runeRankings: seed,
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
}
