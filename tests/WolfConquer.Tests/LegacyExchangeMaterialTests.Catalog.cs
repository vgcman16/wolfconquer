using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyExchangeMaterialTests
{
    private static WorldDataCatalog ExchangeCatalog(IReadOnlyList<ExchangeShopRow> goods, bool includeReward = true)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.UnixEpoch,
                "fixture://item-catalog",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: includeReward ? [ExchangeRewardType()] : [],
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
            shops: [],
            shopItems: [],
            exchangeShopGoods: goods,
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

    private const uint ExchangeMaterialA = 700001;
    private const uint ExchangeMaterialB = 700002;
    private const uint ExchangeRewardId = 730001;

    private static ItemType ExchangeRewardType()
    {
        var fields = Enumerable.Repeat("0", 64).ToArray();
        fields[0] = ExchangeRewardId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[1] = "ExchangeMaterialReward";
        fields[ItemType.DurabilityFieldIndex] = "321";
        return new((int)ExchangeRewardId, "ExchangeMaterialReward", fields);
    }

    private static ExchangeShopRow ExchangeRow(uint amount = 0, uint monopoly = 1,
        params (uint ItemId, uint Count)[] materials)
    {
        if (materials.Length > 4) throw new ArgumentOutOfRangeException(nameof(materials));
        var fields = new List<string> { "444", ExchangeRewardId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "23", amount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            monopoly.ToString(System.Globalization.CultureInfo.InvariantCulture) };
        for (int index = 0; index < 4; index++)
        {
            var material = index < materials.Length ? materials[index] : default;
            fields.Add(material.ItemId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            fields.Add(material.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        return new("fixture://exchange-materials", 1, fields.ToArray());
    }
}
