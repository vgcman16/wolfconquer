using System.Text;
using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

internal static class OwnedHostSeed
{
    // The real JSON loader's required table names, with empty unrelated systems.
    // This fixture never exports or reads an installed client's data.
    private const string Arrays = """
        clientConfigs items itemAddBonuses levelExperience runeLevelExperience runeStorageAttributes
        itemRefineEffects itemRefineEffectEx itemRefineUpgrades transformInfo furniture hairColorTypes
        hairFaceStorageTypes coatStorageTypes coatStorageAttributes coatColorRules xuanbaoAdditionAttributes
        xuanbaoComposeAttributeLimits xuanbaoRandomAttributes christmasNpcs tutorTypes tutorBattleLimitTypes
        beastAttributes fateExperience spiritRates hundredWeapons gouyuTypes gouyuImmortals specialEffectAttributes
        daoqiDictTypes syndicateFormationTypes syndicateSkillTypes disdainRules jiangHuAttributes jiangHuCultivateStatuses
        refineryBoxes refinerySouls refineryMaterials stateCountFiles binarySeedFiles emptySeedFiles backupSeedFiles
        pokerTables rouletteTables npcTimeEntries meltingTypeRules lotteryPrizes arenaRecords teamArenaRecords
        elitePkRecords teamElitePkRecords skillTeamPkRecords classPkWarRecords combatGearRanks prestigeRanks
        innerPowerRanks runeRankings staticStatues newPlayerRewards votes couplesPkRecords socialAssociations
        classLevelStats professionLevelUps professionLevelBenefits professionTitleBenefits questTaskTypes
        questMissions quizQuestions magic magicEffects magicTypeOperations titleTypes combatGear traps npcs
        sobNpcs portals maps mapNames monsters monsterSpawns lotteryDictionary collectionRewardTiers
        taskRewardTypes taskRewardTypeIniEntries neiGongSecrets neiGongTypes subProfessions syndicateLevels
        stageGoalStages stageGoalTasks shops shopItems exchangeShopGoods exchangeShopGoodsEx exchangeShopLevels
        honorShopItems honorShopRecommendations racePointShopItems racePointShopRecommendations goldenLeagueShopItems
        goldenLeagueShopRecommendations booths boothItems
        """;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal static void Write(string ownedRoot)
    {
        var root = Path.Combine(ownedRoot, "seed");
        Directory.CreateDirectory(root);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        void Table<T>(string name, T value, int count)
        {
            File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(value, Json), new UTF8Encoding(false));
            counts[name] = count;
        }
        foreach (var name in Arrays.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)) Table(name, Array.Empty<object>(), 0);
        Table("activityTasks", new ActivityTaskCatalog(new(0, "owned", 1, []), [], "owned.synthetic"), 0);
        Table("guildWarInfo", new GuildWarData("owned.synthetic", [], [], [], null, null, []), 1);
        Table("superGuildWarInfo", new GuildWarData("owned.synthetic", [], [], [], null, null, []), 1);
        Table("questInfo", new QuestInfoMetadata(null, null, 0), 1);
        Table("neiGongHeader", new NeiGongHeader(0, [], "owned.synthetic", 1, "owned", new Dictionary<string, string>()), 1);
        Table("stageGoalInfo", new StageGoalInfo(0), 1);
        Table("goldenLeagueShopConfig", new Dictionary<string, string>(), 0);
        var fields = Enumerable.Repeat("0", 60).ToArray();
        fields[0] = "1000000"; fields[1] = "OwnedSavedItem";
        fields[ItemType.DurabilityFieldIndex] = "100";
        fields[ItemType.StackSizeFieldIndex] = "1";
        Table("items", new[] { new ItemType(1_000_000, fields[1], fields) }, 1);
        // Native prefix requires an actual contiguous, positive Beast table even
        // when saved points are zero; WorldDataStartup performs its real adoption.
        var row = string.Join("@@", new[] { "1", "100" }.Concat(Enumerable.Repeat("0", 16)).Append(""));
        Table("beastAttributes", new[] { BeastParser.ParseRow("owned.synthetic", 1, row) }, 1);
        Table("manifest", new SeedManifest(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero), ownedRoot, counts), 1);
    }
}
