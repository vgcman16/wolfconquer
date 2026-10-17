using System.Text.Json;

namespace WolfConquer.Core.Data;

public sealed class JsonWorldDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly WorldDataOptions _options;

    public JsonWorldDataLoader(WorldDataOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public static JsonWorldDataLoader FromEnvironment()
    {
        return new JsonWorldDataLoader(WorldDataOptions.FromEnvironment());
    }

    public async Task<WorldDataCatalog> LoadAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await ReadRequiredAsync<SeedManifest>("manifest.json", cancellationToken).ConfigureAwait(false);
        var clientConfigs = await ReadRequiredAsync<ClientConfigRecord[]>("clientConfigs.json", cancellationToken).ConfigureAwait(false);
        var items = await ReadRequiredAsync<ItemType[]>("items.json", cancellationToken).ConfigureAwait(false);
        var itemTypeSubs = await ReadOptionalAsync("itemTypeSubs.json", Array.Empty<ItemTypeSub>(), cancellationToken).ConfigureAwait(false);
        var itemAddBonuses = await ReadRequiredAsync<ItemAddBonus[]>("itemAddBonuses.json", cancellationToken).ConfigureAwait(false);
        var levelExperience = await ReadRequiredAsync<LevelExperience[]>("levelExperience.json", cancellationToken).ConfigureAwait(false);
        var runeLevelExperience = await ReadRequiredAsync<RuneLevelExperience[]>("runeLevelExperience.json", cancellationToken).ConfigureAwait(false);
        var runeStorageAttributes = await ReadRequiredAsync<RuneStorageAttribute[]>("runeStorageAttributes.json", cancellationToken).ConfigureAwait(false);
        var itemRefineEffects = await ReadRequiredAsync<ItemRefineEffect[]>("itemRefineEffects.json", cancellationToken).ConfigureAwait(false);
        var itemRefineEffectEx = await ReadRequiredAsync<ItemRefineEffectEx[]>("itemRefineEffectEx.json", cancellationToken).ConfigureAwait(false);
        var itemRefineUpgrades = await ReadRequiredAsync<ItemRefineUpgrade[]>("itemRefineUpgrades.json", cancellationToken).ConfigureAwait(false);
        var transformInfo = await ReadRequiredAsync<TransformInfo[]>("transformInfo.json", cancellationToken).ConfigureAwait(false);
        var furniture = await ReadRequiredAsync<Furniture[]>("furniture.json", cancellationToken).ConfigureAwait(false);
        var hairColorTypes = await ReadRequiredAsync<HairColorType[]>("hairColorTypes.json", cancellationToken).ConfigureAwait(false);
        var hairFaceStorageTypes = await ReadRequiredAsync<HairFaceStorageType[]>("hairFaceStorageTypes.json", cancellationToken).ConfigureAwait(false);
        var coatStorageTypes = await ReadRequiredAsync<CoatStorageType[]>("coatStorageTypes.json", cancellationToken).ConfigureAwait(false);
        var coatStorageAttributes = await ReadRequiredAsync<CoatStorageAttribute[]>("coatStorageAttributes.json", cancellationToken).ConfigureAwait(false);
        var coatColorRules = await ReadRequiredAsync<CoatColorRule[]>("coatColorRules.json", cancellationToken).ConfigureAwait(false);
        var xuanbaoAdditionAttributes = await ReadRequiredAsync<XuanbaoAdditionAttribute[]>("xuanbaoAdditionAttributes.json", cancellationToken).ConfigureAwait(false);
        var xuanbaoComposeAttributeLimits = await ReadRequiredAsync<XuanbaoComposeAttributeLimit[]>("xuanbaoComposeAttributeLimits.json", cancellationToken).ConfigureAwait(false);
        var xuanbaoRandomAttributes = await ReadRequiredAsync<XuanbaoRandomAttribute[]>("xuanbaoRandomAttributes.json", cancellationToken).ConfigureAwait(false);
        var christmasNpcs = await ReadRequiredAsync<ChristmasNpc[]>("christmasNpcs.json", cancellationToken).ConfigureAwait(false);
        var tutorTypes = await ReadRequiredAsync<TutorType[]>("tutorTypes.json", cancellationToken).ConfigureAwait(false);
        var tutorBattleLimitTypes = await ReadRequiredAsync<TutorBattleLimitType[]>("tutorBattleLimitTypes.json", cancellationToken).ConfigureAwait(false);
        var beastAttributes = await ReadRequiredAsync<BeastAttribute[]>("beastAttributes.json", cancellationToken).ConfigureAwait(false);
        var fateExperience = await ReadRequiredAsync<FateExperience[]>("fateExperience.json", cancellationToken).ConfigureAwait(false);
        var spiritRates = await ReadRequiredAsync<SpiritRate[]>("spiritRates.json", cancellationToken).ConfigureAwait(false);
        var hundredWeapons = await ReadRequiredAsync<HundredWeapon[]>("hundredWeapons.json", cancellationToken).ConfigureAwait(false);
        var gouyuTypes = await ReadRequiredAsync<GouyuType[]>("gouyuTypes.json", cancellationToken).ConfigureAwait(false);
        var gouyuImmortals = await ReadRequiredAsync<GouyuImmortal[]>("gouyuImmortals.json", cancellationToken).ConfigureAwait(false);
        var specialEffectAttributes = await ReadRequiredAsync<SpecialEffectAttribute[]>("specialEffectAttributes.json", cancellationToken).ConfigureAwait(false);
        var daoqiDictTypes = await ReadRequiredAsync<DaoqiDictType[]>("daoqiDictTypes.json", cancellationToken).ConfigureAwait(false);
        var syndicateFormationTypes = await ReadRequiredAsync<SyndicateFormationType[]>("syndicateFormationTypes.json", cancellationToken).ConfigureAwait(false);
        var syndicateSkillTypes = await ReadRequiredAsync<SyndicateSkillType[]>("syndicateSkillTypes.json", cancellationToken).ConfigureAwait(false);
        var disdainRules = await ReadRequiredAsync<DisdainRule[]>("disdainRules.json", cancellationToken).ConfigureAwait(false);
        var jiangHuAttributes = await ReadRequiredAsync<JiangHuAttribute[]>("jiangHuAttributes.json", cancellationToken).ConfigureAwait(false);
        var jiangHuCultivateStatuses = await ReadRequiredAsync<JiangHuCultivateStatus[]>("jiangHuCultivateStatuses.json", cancellationToken).ConfigureAwait(false);
        var refineryBoxes = await ReadRequiredAsync<RefineryBox[]>("refineryBoxes.json", cancellationToken).ConfigureAwait(false);
        var refinerySouls = await ReadRequiredAsync<RefinerySoul[]>("refinerySouls.json", cancellationToken).ConfigureAwait(false);
        var refineryMaterials = await ReadRequiredAsync<RefineryMaterial[]>("refineryMaterials.json", cancellationToken).ConfigureAwait(false);
        var stateCountFiles = await ReadRequiredAsync<StateCountFileData[]>("stateCountFiles.json", cancellationToken).ConfigureAwait(false);
        var binarySeedFiles = await ReadRequiredAsync<BinarySeedFile[]>("binarySeedFiles.json", cancellationToken).ConfigureAwait(false);
        var emptySeedFiles = await ReadRequiredAsync<EmptySeedFile[]>("emptySeedFiles.json", cancellationToken).ConfigureAwait(false);
        var backupSeedFiles = await ReadRequiredAsync<BackupSeedFile[]>("backupSeedFiles.json", cancellationToken).ConfigureAwait(false);
        var pokerTables = await ReadRequiredAsync<PokerTable[]>("pokerTables.json", cancellationToken).ConfigureAwait(false);
        var rouletteTables = await ReadRequiredAsync<RouletteTable[]>("rouletteTables.json", cancellationToken).ConfigureAwait(false);
        var npcTimeEntries = await ReadRequiredAsync<NpcTimeEntry[]>("npcTimeEntries.json", cancellationToken).ConfigureAwait(false);
        var activityTasks = await ReadRequiredAsync<ActivityTaskCatalog>("activityTasks.json", cancellationToken).ConfigureAwait(false);
        var meltingTypeRules = await ReadRequiredAsync<MeltingTypeRule[]>("meltingTypeRules.json", cancellationToken).ConfigureAwait(false);
        var lotteryPrizes = await ReadRequiredAsync<LotteryPrize[]>("lotteryPrizes.json", cancellationToken).ConfigureAwait(false);
        var arenaRecords = await ReadRequiredAsync<ArenaRecord[]>("arenaRecords.json", cancellationToken).ConfigureAwait(false);
        var teamArenaRecords = await ReadRequiredAsync<TeamArenaRecord[]>("teamArenaRecords.json", cancellationToken).ConfigureAwait(false);
        var elitePkRecords = await ReadRequiredAsync<PkTournamentRecord[]>("elitePkRecords.json", cancellationToken).ConfigureAwait(false);
        var teamElitePkRecords = await ReadRequiredAsync<PkTournamentRecord[]>("teamElitePkRecords.json", cancellationToken).ConfigureAwait(false);
        var skillTeamPkRecords = await ReadRequiredAsync<PkTournamentRecord[]>("skillTeamPkRecords.json", cancellationToken).ConfigureAwait(false);
        var classPkWarRecords = await ReadRequiredAsync<ClassPkWarRecord[]>("classPkWarRecords.json", cancellationToken).ConfigureAwait(false);
        var combatGearRanks = await ReadRequiredAsync<CombatGearRankEntry[]>("combatGearRanks.json", cancellationToken).ConfigureAwait(false);
        var prestigeRanks = await ReadRequiredAsync<PrestigeRankEntry[]>("prestigeRanks.json", cancellationToken).ConfigureAwait(false);
        IReadOnlyList<InnerPowerRankEntry> innerPowerRanks =
            await ReadRequiredAsync<InnerPowerRankEntry[]>("innerPowerRanks.json", cancellationToken).ConfigureAwait(false);
        innerPowerRanks = LegacyMutableWorldDataOverlay.ReadInnerPowerRanks(manifest, innerPowerRanks);
        var runeRankings = await ReadRequiredAsync<RuneRankingRecord[]>("runeRankings.json", cancellationToken).ConfigureAwait(false);
        var staticStatues = await ReadRequiredAsync<StaticStatueRecord[]>("staticStatues.json", cancellationToken).ConfigureAwait(false);
        var newPlayerRewards = await ReadRequiredAsync<NewPlayerRewardRecord[]>("newPlayerRewards.json", cancellationToken).ConfigureAwait(false);
        var votes = await ReadRequiredAsync<VoteRecord[]>("votes.json", cancellationToken).ConfigureAwait(false);
        var couplesPkRecords = await ReadRequiredAsync<CouplesPkRecord[]>("couplesPkRecords.json", cancellationToken).ConfigureAwait(false);
        var socialAssociations = await ReadRequiredAsync<SocialAssociationRecord[]>("socialAssociations.json", cancellationToken).ConfigureAwait(false);
        var guildWarInfo = await ReadRequiredAsync<GuildWarData>("guildWarInfo.json", cancellationToken).ConfigureAwait(false);
        var superGuildWarInfo = await ReadRequiredAsync<GuildWarData>("superGuildWarInfo.json", cancellationToken).ConfigureAwait(false);
        var classLevelStats = await ReadRequiredAsync<ClassLevelStat[]>("classLevelStats.json", cancellationToken).ConfigureAwait(false);
        var professionLevelUps = await ReadRequiredAsync<ProfessionLevelUp[]>("professionLevelUps.json", cancellationToken).ConfigureAwait(false);
        var professionLevelBenefits = await ReadRequiredAsync<ProfessionLevelBenefit[]>("professionLevelBenefits.json", cancellationToken).ConfigureAwait(false);
        var professionTitleBenefits = await ReadRequiredAsync<ProfessionTitleBenefit[]>("professionTitleBenefits.json", cancellationToken).ConfigureAwait(false);
        var questInfo = await ReadRequiredAsync<QuestInfoMetadata>("questInfo.json", cancellationToken).ConfigureAwait(false);
        var questTaskTypes = await ReadRequiredAsync<QuestTaskType[]>("questTaskTypes.json", cancellationToken).ConfigureAwait(false);
        var questMissions = await ReadRequiredAsync<QuestMission[]>("questMissions.json", cancellationToken).ConfigureAwait(false);
        var quizQuestions = await ReadRequiredAsync<QuizQuestionEntry[]>("quizQuestions.json", cancellationToken).ConfigureAwait(false);
        var magic = await ReadRequiredAsync<MagicType[]>("magic.json", cancellationToken).ConfigureAwait(false);
        var magicEffects = await ReadRequiredAsync<MagicEffect[]>("magicEffects.json", cancellationToken).ConfigureAwait(false);
        var magicTypeOperations = await ReadRequiredAsync<MagicTypeOperationEntry[]>("magicTypeOperations.json", cancellationToken).ConfigureAwait(false);
        var titleTypes = await ReadRequiredAsync<TitleType[]>("titleTypes.json", cancellationToken).ConfigureAwait(false);
        var combatGear = await ReadRequiredAsync<CombatGear[]>("combatGear.json", cancellationToken).ConfigureAwait(false);
        var traps = await ReadRequiredAsync<Trap[]>("traps.json", cancellationToken).ConfigureAwait(false);
        var npcs = await ReadRequiredAsync<Npc[]>("npcs.json", cancellationToken).ConfigureAwait(false);
        var sobNpcs = await ReadRequiredAsync<SobNpc[]>("sobNpcs.json", cancellationToken).ConfigureAwait(false);
        var portals = await ReadRequiredAsync<Portal[]>("portals.json", cancellationToken).ConfigureAwait(false);
        var maps = await ReadRequiredAsync<MapMetadata[]>("maps.json", cancellationToken).ConfigureAwait(false);
        var mapNames = await ReadRequiredAsync<MapName[]>("mapNames.json", cancellationToken).ConfigureAwait(false);
        var legacyGameMaps = await ReadOptionalAsync("legacyGameMaps.json", Array.Empty<LegacyGameMapEntry>(), cancellationToken).ConfigureAwait(false);
        var monsters = await ReadRequiredAsync<Monster[]>("monsters.json", cancellationToken).ConfigureAwait(false);
        var monsterSpawns = await ReadRequiredAsync<MonsterSpawn[]>("monsterSpawns.json", cancellationToken).ConfigureAwait(false);
        var lotteryDictionary = await ReadRequiredAsync<LotteryDictionaryEntry[]>("lotteryDictionary.json", cancellationToken).ConfigureAwait(false);
        var collectionRewardTiers = await ReadRequiredAsync<CollectionRewardTier[]>("collectionRewardTiers.json", cancellationToken).ConfigureAwait(false);
        var taskRewardTypes = await ReadRequiredAsync<TaskRewardTypeEntry[]>("taskRewardTypes.json", cancellationToken).ConfigureAwait(false);
        var taskRewardTypeIniEntries = await ReadRequiredAsync<TaskRewardTypeIniEntry[]>("taskRewardTypeIniEntries.json", cancellationToken).ConfigureAwait(false);
        var neiGongHeader = await ReadRequiredAsync<NeiGongHeader>("neiGongHeader.json", cancellationToken).ConfigureAwait(false);
        var neiGongSecrets = await ReadRequiredAsync<NeiGongSecret[]>("neiGongSecrets.json", cancellationToken).ConfigureAwait(false);
        var neiGongTypes = await ReadRequiredAsync<NeiGongType[]>("neiGongTypes.json", cancellationToken).ConfigureAwait(false);
        var subProfessions = await ReadRequiredAsync<SubProfessionEntry[]>("subProfessions.json", cancellationToken).ConfigureAwait(false);
        var syndicateLevels = await ReadRequiredAsync<SyndicateLevel[]>("syndicateLevels.json", cancellationToken).ConfigureAwait(false);
        var stageGoalInfo = await ReadRequiredAsync<StageGoalInfo>("stageGoalInfo.json", cancellationToken).ConfigureAwait(false);
        var stageGoalStages = await ReadRequiredAsync<StageGoalStage[]>("stageGoalStages.json", cancellationToken).ConfigureAwait(false);
        var stageGoalTasks = await ReadRequiredAsync<StageGoalTask[]>("stageGoalTasks.json", cancellationToken).ConfigureAwait(false);
        var shops = await ReadRequiredAsync<ShopDefinition[]>("shops.json", cancellationToken).ConfigureAwait(false);
        var shopItems = await ReadRequiredAsync<ShopItem[]>("shopItems.json", cancellationToken).ConfigureAwait(false);
        var exchangeShopGoods = await ReadRequiredAsync<ExchangeShopRow[]>("exchangeShopGoods.json", cancellationToken).ConfigureAwait(false);
        var exchangeShopGoodsEx = await ReadRequiredAsync<ExchangeShopRow[]>("exchangeShopGoodsEx.json", cancellationToken).ConfigureAwait(false);
        var exchangeShopLevels = await ReadRequiredAsync<ExchangeShopRow[]>("exchangeShopLevels.json", cancellationToken).ConfigureAwait(false);
        var honorShopItems = await ReadRequiredAsync<HonorShopItem[]>("honorShopItems.json", cancellationToken).ConfigureAwait(false);
        var honorShopRecommendations = await ReadRequiredAsync<HonorShopRecommendation[]>("honorShopRecommendations.json", cancellationToken).ConfigureAwait(false);
        var racePointShopItems = await ReadRequiredAsync<PointShopItem[]>("racePointShopItems.json", cancellationToken).ConfigureAwait(false);
        var racePointShopRecommendations = await ReadRequiredAsync<PointShopRecommendation[]>("racePointShopRecommendations.json", cancellationToken).ConfigureAwait(false);
        var goldenLeagueShopItems = await ReadRequiredAsync<PointShopItem[]>("goldenLeagueShopItems.json", cancellationToken).ConfigureAwait(false);
        var goldenLeagueShopRecommendations = await ReadRequiredAsync<PointShopRecommendation[]>("goldenLeagueShopRecommendations.json", cancellationToken).ConfigureAwait(false);
        var goldenLeagueShopConfig = await ReadRequiredAsync<Dictionary<string, string>>("goldenLeagueShopConfig.json", cancellationToken).ConfigureAwait(false);
        var booths = await ReadRequiredAsync<BoothDefinition[]>("booths.json", cancellationToken).ConfigureAwait(false);
        var boothItems = await ReadRequiredAsync<BoothItem[]>("boothItems.json", cancellationToken).ConfigureAwait(false);

        return WorldDataCatalog.Create(
            manifest,
            clientConfigs,
            items,
            itemAddBonuses,
            levelExperience,
            runeLevelExperience,
            runeStorageAttributes,
            itemRefineEffects,
            itemRefineEffectEx,
            itemRefineUpgrades,
            transformInfo,
            furniture,
            hairColorTypes,
            hairFaceStorageTypes,
            coatStorageTypes,
            coatStorageAttributes,
            coatColorRules,
            xuanbaoAdditionAttributes,
            xuanbaoComposeAttributeLimits,
            xuanbaoRandomAttributes,
            christmasNpcs,
            tutorTypes,
            tutorBattleLimitTypes,
            beastAttributes,
            fateExperience,
            spiritRates,
            hundredWeapons,
            gouyuTypes,
            gouyuImmortals,
            specialEffectAttributes,
            daoqiDictTypes,
            syndicateFormationTypes,
            syndicateSkillTypes,
            disdainRules,
            jiangHuAttributes,
            jiangHuCultivateStatuses,
            refineryBoxes,
            refinerySouls,
            refineryMaterials,
            stateCountFiles,
            binarySeedFiles,
            emptySeedFiles,
            backupSeedFiles,
            pokerTables,
            rouletteTables,
            npcTimeEntries,
            activityTasks,
            meltingTypeRules,
            lotteryPrizes,
            arenaRecords,
            elitePkRecords,
            teamElitePkRecords,
            skillTeamPkRecords,
            classPkWarRecords,
            combatGearRanks,
            prestigeRanks,
            innerPowerRanks,
            runeRankings,
            staticStatues,
            newPlayerRewards,
            votes,
            couplesPkRecords,
            socialAssociations,
            guildWarInfo,
            superGuildWarInfo,
            classLevelStats,
            professionLevelUps,
            professionLevelBenefits,
            professionTitleBenefits,
            questInfo,
            questTaskTypes,
            questMissions,
            quizQuestions,
            magic,
            magicEffects.Select(NormalizeMagicEffect).ToArray(),
            magicTypeOperations,
            titleTypes,
            combatGear,
            traps,
            npcs,
            sobNpcs,
            portals,
            maps.Select(NormalizeMap).ToArray(),
            mapNames,
            monsters.Select(NormalizeMonster).ToArray(),
            monsterSpawns.Select(NormalizeMonsterSpawn).ToArray(),
            lotteryDictionary,
            collectionRewardTiers,
            taskRewardTypes,
            taskRewardTypeIniEntries,
            neiGongHeader,
            neiGongSecrets,
            neiGongTypes,
            subProfessions,
            syndicateLevels,
            stageGoalInfo,
            stageGoalStages,
            stageGoalTasks,
            shops.Select(NormalizeShop).ToArray(),
            shopItems.Select(NormalizeShopItem).ToArray(),
            exchangeShopGoods,
            exchangeShopGoodsEx,
            exchangeShopLevels,
            honorShopItems,
            honorShopRecommendations,
            racePointShopItems,
            racePointShopRecommendations,
            goldenLeagueShopItems,
            goldenLeagueShopRecommendations,
            NormalizeStringDictionary(goldenLeagueShopConfig),
            booths.Select(NormalizeBooth).ToArray(),
            boothItems,
            teamArenaRecords: teamArenaRecords,
            itemTypeSubs: itemTypeSubs,
            legacyGameMaps: legacyGameMaps);
    }

    private async Task<T> ReadRequiredAsync<T>(string fileName, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_options.RootPath, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Required world seed file was not found: {fileName}", path);
        }

        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return value ?? throw new InvalidDataException($"World seed file did not contain a {typeof(T).Name}: {fileName}");
    }

    private async Task<T> ReadOptionalAsync<T>(string fileName, T defaultValue, CancellationToken cancellationToken)
    {
        var path = Path.Combine(_options.RootPath, fileName);
        if (!File.Exists(path))
        {
            return defaultValue;
        }

        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return value ?? throw new InvalidDataException($"World seed file did not contain a {typeof(T).Name}: {fileName}");
    }

    private static MapMetadata NormalizeMap(MapMetadata map)
    {
        return map with
        {
            Attributes = NormalizeAttributes(map.Attributes)
        };
    }

    private static Monster NormalizeMonster(Monster monster)
    {
        // MonsterParser applies the decompiled load rules before canonical JSON is exported.
        return monster with
        {
            Attributes = NormalizeAttributes(monster.Attributes)
        };
    }

    private static MonsterSpawn NormalizeMonsterSpawn(MonsterSpawn spawn)
    {
        return spawn with
        {
            Attributes = NormalizeAttributes(spawn.Attributes)
        };
    }

    private static ShopDefinition NormalizeShop(ShopDefinition shop)
    {
        return shop with
        {
            Attributes = NormalizeAttributes(shop.Attributes)
        };
    }

    private static MagicEffect NormalizeMagicEffect(MagicEffect effect)
    {
        return effect with
        {
            Attributes = NormalizeAttributes(effect.Attributes)
        };
    }

    private static ShopItem NormalizeShopItem(ShopItem item)
    {
        return item with
        {
            Attributes = NormalizeAttributes(item.Attributes)
        };
    }

    private static BoothDefinition NormalizeBooth(BoothDefinition booth)
    {
        return booth with
        {
            Attributes = NormalizeAttributes(booth.Attributes)
        };
    }

    private static IReadOnlyDictionary<string, string> NormalizeAttributes(IReadOnlyDictionary<string, string> attributes)
    {
        return new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, string> NormalizeStringDictionary(IReadOnlyDictionary<string, string> values)
    {
        return new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }
}
