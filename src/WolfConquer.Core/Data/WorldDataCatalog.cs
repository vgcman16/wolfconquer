namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    private WorldDataCatalog(
        SeedManifest manifest,
        IReadOnlyList<ClientConfigRecord> clientConfigs,
        IReadOnlyList<ItemType> items,
        IReadOnlyList<ItemTypeSub> itemTypeSubs,
        IReadOnlyList<ItemAddBonus> itemAddBonuses,
        IReadOnlyList<LevelExperience> levelExperience,
        IReadOnlyList<RuneLevelExperience> runeLevelExperience,
        IReadOnlyList<RuneStorageAttribute> runeStorageAttributes,
        IReadOnlyList<ItemRefineEffect> itemRefineEffects,
        IReadOnlyList<ItemRefineEffectEx> itemRefineEffectEx,
        IReadOnlyList<ItemRefineUpgrade> itemRefineUpgrades,
        IReadOnlyList<TransformInfo> transformInfo,
        IReadOnlyList<Furniture> furniture,
        IReadOnlyList<HairColorType> hairColorTypes,
        IReadOnlyList<HairFaceStorageType> hairFaceStorageTypes,
        IReadOnlyList<CoatStorageType> coatStorageTypes,
        IReadOnlyList<CoatStorageAttribute> coatStorageAttributes,
        IReadOnlyList<CoatColorRule> coatColorRules,
        IReadOnlyList<XuanbaoAdditionAttribute> xuanbaoAdditionAttributes,
        IReadOnlyList<XuanbaoComposeAttributeLimit> xuanbaoComposeAttributeLimits,
        IReadOnlyList<XuanbaoRandomAttribute> xuanbaoRandomAttributes,
        IReadOnlyList<ChristmasNpc> christmasNpcs,
        IReadOnlyList<TutorType> tutorTypes,
        IReadOnlyList<TutorBattleLimitType> tutorBattleLimitTypes,
        IReadOnlyList<BeastAttribute> beastAttributes,
        IReadOnlyList<FateExperience> fateExperience,
        IReadOnlyList<SpiritRate> spiritRates,
        IReadOnlyList<HundredWeapon> hundredWeapons,
        IReadOnlyList<GouyuType> gouyuTypes,
        IReadOnlyList<GouyuImmortal> gouyuImmortals,
        IReadOnlyList<SpecialEffectAttribute> specialEffectAttributes,
        IReadOnlyList<DaoqiDictType> daoqiDictTypes,
        IReadOnlyList<SyndicateFormationType> syndicateFormationTypes,
        IReadOnlyList<SyndicateSkillType> syndicateSkillTypes,
        IReadOnlyList<DisdainRule> disdainRules,
        IReadOnlyList<JiangHuAttribute> jiangHuAttributes,
        IReadOnlyList<JiangHuCultivateStatus> jiangHuCultivateStatuses,
        IReadOnlyList<RefineryBox> refineryBoxes,
        IReadOnlyList<RefinerySoul> refinerySouls,
        IReadOnlyList<RefineryMaterial> refineryMaterials,
        IReadOnlyList<StateCountFileData> stateCountFiles,
        IReadOnlyList<BinarySeedFile> binarySeedFiles,
        IReadOnlyList<EmptySeedFile> emptySeedFiles,
        IReadOnlyList<BackupSeedFile> backupSeedFiles,
        IReadOnlyList<PokerTable> pokerTables,
        IReadOnlyList<RouletteTable> rouletteTables,
        IReadOnlyList<NpcTimeEntry> npcTimeEntries,
        ActivityTaskCatalog activityTasks,
        IReadOnlyList<MeltingTypeRule> meltingTypeRules,
        IReadOnlyList<LotteryPrize> lotteryPrizes,
        IReadOnlyList<ArenaRecord> arenaRecords,
        IReadOnlyList<TeamArenaRecord> teamArenaRecords,
        IReadOnlyList<PkTournamentRecord> elitePkRecords,
        IReadOnlyList<PkTournamentRecord> teamElitePkRecords,
        IReadOnlyList<PkTournamentRecord> skillTeamPkRecords,
        IReadOnlyList<ClassPkWarRecord> classPkWarRecords,
        IReadOnlyList<CombatGearRankEntry> combatGearRanks,
        IReadOnlyList<PrestigeRankEntry> prestigeRanks,
        IReadOnlyList<InnerPowerRankEntry> innerPowerRanks,
        IReadOnlyList<RuneRankingRecord> runeRankings,
        IReadOnlyList<StaticStatueRecord> staticStatues,
        IReadOnlyList<NewPlayerRewardRecord> newPlayerRewards,
        IReadOnlyList<VoteRecord> votes,
        IReadOnlyList<CouplesPkRecord> couplesPkRecords,
        IReadOnlyList<SocialAssociationRecord> socialAssociations,
        GuildWarData guildWarInfo,
        GuildWarData superGuildWarInfo,
        IReadOnlyList<ClassLevelStat> classLevelStats,
        IReadOnlyList<ProfessionLevelUp> professionLevelUps,
        IReadOnlyList<ProfessionLevelBenefit> professionLevelBenefits,
        IReadOnlyList<ProfessionTitleBenefit> professionTitleBenefits,
        QuestInfoMetadata questInfo,
        IReadOnlyList<QuestTaskType> questTaskTypes,
        IReadOnlyList<QuestMission> questMissions,
        IReadOnlyList<QuizQuestionEntry> quizQuestions,
        IReadOnlyList<MagicType> magic,
        IReadOnlyList<MagicEffect> magicEffects,
        IReadOnlyList<MagicTypeOperationEntry> magicTypeOperations,
        IReadOnlyList<TitleType> titleTypes,
        IReadOnlyList<CombatGear> combatGear,
        IReadOnlyList<Trap> traps,
        IReadOnlyList<Npc> npcs,
        IReadOnlyList<SobNpc> sobNpcs,
        IReadOnlyList<Portal> portals,
        IReadOnlyList<MapMetadata> maps,
        IReadOnlyList<MapName> mapNames,
        IReadOnlyList<LegacyGameMapEntry> legacyGameMaps,
        IReadOnlyList<Monster> monsters,
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        IReadOnlyList<LotteryDictionaryEntry> lotteryDictionary,
        IReadOnlyList<CollectionRewardTier> collectionRewardTiers,
        IReadOnlyList<TaskRewardTypeEntry> taskRewardTypes,
        IReadOnlyList<TaskRewardTypeIniEntry> taskRewardTypeIniEntries,
        NeiGongHeader neiGongHeader,
        IReadOnlyList<NeiGongSecret> neiGongSecrets,
        IReadOnlyList<NeiGongType> neiGongTypes,
        IReadOnlyList<SubProfessionEntry> subProfessions,
        IReadOnlyList<SyndicateLevel> syndicateLevels,
        StageGoalInfo stageGoalInfo,
        IReadOnlyList<StageGoalStage> stageGoalStages,
        IReadOnlyList<StageGoalTask> stageGoalTasks,
        IReadOnlyList<ShopDefinition> shops,
        IReadOnlyList<ShopItem> shopItems,
        IReadOnlyList<ExchangeShopRow> exchangeShopGoods,
        IReadOnlyList<ExchangeShopRow> exchangeShopGoodsEx,
        IReadOnlyList<ExchangeShopRow> exchangeShopLevels,
        IReadOnlyList<HonorShopItem> honorShopItems,
        IReadOnlyList<HonorShopRecommendation> honorShopRecommendations,
        IReadOnlyList<PointShopItem> racePointShopItems,
        IReadOnlyList<PointShopRecommendation> racePointShopRecommendations,
        IReadOnlyList<PointShopItem> goldenLeagueShopItems,
        IReadOnlyList<PointShopRecommendation> goldenLeagueShopRecommendations,
        IReadOnlyDictionary<string, string> goldenLeagueShopConfig,
        IReadOnlyList<BoothDefinition> booths,
        IReadOnlyList<BoothItem> boothItems,
        IReadOnlyList<WorldDataDiagnostic> diagnostics)
    {
        Manifest = manifest;
        ClientConfigs = clientConfigs;
        Items = items;
        ItemTypeSubs = itemTypeSubs;
        ItemAddBonuses = itemAddBonuses;
        LevelExperience = levelExperience;
        RuneLevelExperience = runeLevelExperience;
        RuneStorageAttributes = runeStorageAttributes;
        ItemRefineEffects = itemRefineEffects;
        ItemRefineEffectEx = itemRefineEffectEx;
        ItemRefineUpgrades = itemRefineUpgrades;
        TransformInfo = transformInfo;
        Furniture = furniture;
        HairColorTypes = hairColorTypes;
        HairFaceStorageTypes = hairFaceStorageTypes;
        CoatStorageTypes = coatStorageTypes;
        CoatStorageAttributes = coatStorageAttributes;
        CoatColorRules = coatColorRules;
        XuanbaoAdditionAttributes = xuanbaoAdditionAttributes;
        XuanbaoComposeAttributeLimits = xuanbaoComposeAttributeLimits;
        XuanbaoRandomAttributes = xuanbaoRandomAttributes;
        ChristmasNpcs = christmasNpcs;
        TutorTypes = tutorTypes;
        TutorBattleLimitTypes = tutorBattleLimitTypes;
        BeastAttributes = beastAttributes;
        FateExperience = fateExperience;
        SpiritRates = spiritRates;
        HundredWeapons = hundredWeapons;
        GouyuTypes = gouyuTypes;
        GouyuImmortals = gouyuImmortals;
        SpecialEffectAttributes = specialEffectAttributes;
        DaoqiDictTypes = daoqiDictTypes;
        SyndicateFormationTypes = syndicateFormationTypes;
        SyndicateSkillTypes = syndicateSkillTypes;
        DisdainRules = disdainRules;
        JiangHuAttributes = jiangHuAttributes;
        JiangHuCultivateStatuses = jiangHuCultivateStatuses;
        RefineryBoxes = refineryBoxes;
        RefinerySouls = refinerySouls;
        RefineryMaterials = refineryMaterials;
        StateCountFiles = stateCountFiles;
        BinarySeedFiles = binarySeedFiles;
        EmptySeedFiles = emptySeedFiles;
        BackupSeedFiles = backupSeedFiles;
        PokerTables = pokerTables;
        RouletteTables = rouletteTables;
        NpcTimeEntries = npcTimeEntries;
        ActivityTasks = activityTasks;
        MeltingTypeRules = meltingTypeRules;
        LotteryPrizes = lotteryPrizes;
        ArenaRecords = arenaRecords;
        TeamArenaRecords = teamArenaRecords;
        ElitePkRecords = elitePkRecords;
        TeamElitePkRecords = teamElitePkRecords;
        SkillTeamPkRecords = skillTeamPkRecords;
        ClassPkWarRecords = classPkWarRecords;
        CombatGearRanks = combatGearRanks;
        PrestigeRanks = prestigeRanks;
        InnerPowerRanks = innerPowerRanks;
        RuneRankings = runeRankings;
        StaticStatues = staticStatues;
        NewPlayerRewards = newPlayerRewards;
        Votes = votes;
        CouplesPkRecords = couplesPkRecords;
        SocialAssociations = socialAssociations;
        GuildWarInfo = guildWarInfo;
        SuperGuildWarInfo = superGuildWarInfo;
        ClassLevelStats = classLevelStats;
        ProfessionLevelUps = professionLevelUps;
        ProfessionLevelBenefits = professionLevelBenefits;
        ProfessionTitleBenefits = professionTitleBenefits;
        QuestInfo = questInfo;
        QuestTaskTypes = questTaskTypes;
        QuestMissions = questMissions;
        QuizQuestions = quizQuestions;
        Magic = magic;
        MagicEffects = magicEffects;
        MagicTypeOperations = magicTypeOperations;
        TitleTypes = titleTypes;
        CombatGear = combatGear;
        Traps = traps;
        Npcs = npcs;
        SobNpcs = sobNpcs;
        Portals = portals;
        Maps = maps;
        MapNames = mapNames;
        LegacyGameMaps = legacyGameMaps;
        Monsters = monsters;
        MonsterSpawns = monsterSpawns;
        LegacyMonsterRuntimeUidBases = BuildLegacyMonsterRuntimeUidBases(monsterSpawns, monsters);
        LotteryDictionary = lotteryDictionary;
        CollectionRewardTiers = collectionRewardTiers;
        TaskRewardTypes = taskRewardTypes;
        TaskRewardTypeIniEntries = taskRewardTypeIniEntries;
        NeiGongHeader = neiGongHeader;
        NeiGongSecrets = neiGongSecrets;
        NeiGongTypes = neiGongTypes;
        SubProfessions = subProfessions;
        SyndicateLevels = syndicateLevels;
        StageGoalInfo = stageGoalInfo;
        StageGoalStages = stageGoalStages;
        StageGoalTasks = stageGoalTasks;
        Shops = shops;
        ShopItems = shopItems;
        ExchangeShopGoods = exchangeShopGoods;
        ExchangeShopGoodsEx = exchangeShopGoodsEx;
        ExchangeShopLevels = exchangeShopLevels;
        HonorShopItems = honorShopItems;
        HonorShopRecommendations = honorShopRecommendations;
        RacePointShopItems = racePointShopItems;
        RacePointShopRecommendations = racePointShopRecommendations;
        GoldenLeagueShopItems = goldenLeagueShopItems;
        GoldenLeagueShopRecommendations = goldenLeagueShopRecommendations;
        GoldenLeagueShopConfig = goldenLeagueShopConfig;
        Booths = booths;
        BoothItems = boothItems;
        Diagnostics = diagnostics;

        ClientConfigsByServerId = BuildUniqueIndex(clientConfigs, entry => entry.ServerId);
        ClientConfigsByServerName = BuildGroupedIndex(clientConfigs, entry => entry.ServerName, StringComparer.OrdinalIgnoreCase);
        ClientConfigsByHost = BuildGroupedIndex(clientConfigs, entry => entry.Host, StringComparer.OrdinalIgnoreCase);
        ClientConfigsByPort = BuildGroupedIndex(clientConfigs, entry => entry.Port);
        MapsById = BuildUniqueIndex(maps, map => map.Id);
        MapNamesByMapId = BuildUniqueIndex(mapNames, mapName => mapName.MapId);
        MapNamesByName = BuildGroupedIndex(mapNames, mapName => mapName.Name, StringComparer.OrdinalIgnoreCase);
        LegacyGameMapsByMapId = BuildUniqueIndex(legacyGameMaps, map => map.MapId);
        MonstersById = BuildUniqueIndex(monsters, monster => monster.Id);
        MagicById = BuildUniqueIndex(magic, spell => spell.Id);
        MagicEffectsById = BuildGroupedIndex(magicEffects, effect => effect.Id);
        MagicEffectByIdAndOccurrence = BuildUniqueIndex(magicEffects, effect => (effect.Id, effect.Occurrence));
        MagicTypeOperationsById = BuildUniqueIndex(magicTypeOperations, entry => entry.Id);
        MagicTypeOperationsByMode = BuildGroupedIndex(magicTypeOperations, entry => entry.Mode);
        MagicTypeOperationsByReferencedMagicTypeId = BuildMagicTypeOperationReferenceIndex(magicTypeOperations);
        TitleTypesById = BuildUniqueIndex(titleTypes, title => title.Id);
        TitleTypesByTypeId = BuildGroupedIndex(titleTypes, title => title.TypeId);
        TitleTypesByName = BuildGroupedIndex(titleTypes, title => title.Name, StringComparer.OrdinalIgnoreCase);
        CombatGearById = BuildUniqueIndex(combatGear, gear => gear.Id);
        CombatGearByGearTypeId = BuildGroupedIndex(combatGear, gear => gear.GearTypeId);
        CombatGearByGearTypeAndLevel = BuildUniqueIndex(combatGear, gear => (gear.GearTypeId, gear.Level));
        TrapsByMapId = BuildGroupedIndex(traps, trap => trap.MapId);
        TrapsByTrapTypeId = BuildGroupedIndex(traps, trap => trap.TrapTypeId);
        TrapsByMapPosition = BuildGroupedIndex(traps, trap => (trap.MapId, trap.X, trap.Y));
        ItemsById = BuildGroupedIndex(items, item => item.Id);
        ItemTypeSubsByItemTypeId = BuildGroupedIndex(itemTypeSubs, entry => entry.ItemTypeId);
        ItemAddBonusesByItemTypeId = BuildGroupedIndex(itemAddBonuses, bonus => bonus.ItemTypeId);
        ItemAddBonusByItemTypeIdAndLevel = BuildUniqueIndex(
            itemAddBonuses,
            bonus => (bonus.ItemTypeId, bonus.Level));
        LevelExperienceByType = BuildGroupedIndex(levelExperience, entry => entry.Type);
        LevelExperienceByTypeAndLevel = BuildUniqueIndex(
            levelExperience,
            entry => (entry.Type, entry.Level));
        RuneLevelExperienceById = BuildUniqueIndex(runeLevelExperience, entry => entry.Id);
        RuneLevelExperienceByRuneType = BuildGroupedIndex(runeLevelExperience, entry => entry.RuneType);
        RuneLevelExperienceByRuneTypeAndLevel = BuildUniqueIndex(runeLevelExperience, entry => (entry.RuneType, entry.Level));
        RuneStorageAttributesById = BuildUniqueIndex(runeStorageAttributes, entry => entry.Id);
        RuneStorageAttributesByRequiredStorageScore = BuildGroupedIndex(runeStorageAttributes, entry => entry.RequiredStorageScore);
        ItemRefineEffectsById = BuildGroupedIndex(itemRefineEffects, entry => entry.Id);
        ItemRefineEffectExById = BuildUniqueIndex(itemRefineEffectEx, entry => entry.Id);
        ItemRefineEffectExByItemTypeId = BuildGroupedIndex(itemRefineEffectEx, entry => entry.ItemTypeId);
        ItemRefineUpgradesById = BuildGroupedIndex(itemRefineUpgrades, entry => entry.Id);
        ItemRefineUpgradeByIdAndLevel = BuildUniqueIndex(itemRefineUpgrades, entry => (entry.Id, entry.Level));
        TransformInfoByTransformId = BuildGroupedIndex(transformInfo, entry => entry.TransformId);
        TransformInfoByTransformIdAndLevel = BuildUniqueIndex(transformInfo, entry => (entry.TransformId, entry.Level));
        TransformInfoByLookface = BuildGroupedIndex(transformInfo, entry => entry.Lookface);
        TransformInfoByName = BuildGroupedIndex(transformInfo, entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        FurnitureById = BuildUniqueIndex(furniture, entry => entry.Id);
        FurnitureByItemTypeId = BuildGroupedIndex(furniture, entry => entry.ItemTypeId);
        FurnitureByMapId = BuildGroupedIndex(furniture, entry => entry.MapId);
        FurnitureByMapPosition = BuildGroupedIndex(furniture, entry => (entry.MapId, entry.X, entry.Y));
        FurnitureByKind = BuildGroupedIndex(furniture, entry => entry.Kind);
        FurnitureByName = BuildGroupedIndex(furniture, entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        HairColorTypesByHairColorId = BuildGroupedIndex(hairColorTypes, entry => entry.HairColorId);
        HairColorTypesByColorIndex = BuildGroupedIndex(hairColorTypes, entry => entry.ColorIndex);
        HairColorTypeByHairColorIdAndColorIndex = BuildUniqueIndex(hairColorTypes, entry => (entry.HairColorId, entry.ColorIndex));
        HairColorTypesByItemTypeId = BuildGroupedIndex(hairColorTypes, entry => entry.ItemTypeId);
        HairFaceStorageTypesByCategoryType = BuildGroupedIndex(hairFaceStorageTypes, entry => entry.CategoryType);
        HairFaceStorageTypesById = BuildGroupedIndex(hairFaceStorageTypes, entry => entry.Id);
        HairFaceStorageTypeByCategoryAndId = BuildUniqueIndex(hairFaceStorageTypes, entry => (entry.CategoryType, entry.Id));
        HairFaceStorageTypesByName = BuildGroupedIndex(hairFaceStorageTypes, entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        HairFaceStorageTypesByUnlockType = BuildGroupedIndex(hairFaceStorageTypes, entry => entry.UnlockType);
        CoatStorageTypesByItemTypeId = BuildUniqueIndex(coatStorageTypes, entry => entry.ItemTypeId);
        CoatStorageTypesByStorageType = BuildGroupedIndex(coatStorageTypes, entry => entry.StorageType);
        CoatStorageAttributesById = BuildUniqueIndex(coatStorageAttributes, entry => entry.Id);
        CoatStorageAttributesByStorageType = BuildGroupedIndex(coatStorageAttributes, entry => entry.StorageType);
        CoatColorRulesByItemTypeId = BuildUniqueIndex(coatColorRules, entry => entry.ItemTypeId);
        XuanbaoAdditionAttributesById = BuildUniqueIndex(xuanbaoAdditionAttributes, entry => entry.Id);
        XuanbaoAdditionAttributesByAttributeType = BuildGroupedIndex(xuanbaoAdditionAttributes, entry => entry.AttributeType);
        XuanbaoAdditionAttributeByAttributeTypeAndLevel = BuildUniqueIndex(xuanbaoAdditionAttributes, entry => (entry.AttributeType, entry.Level));
        XuanbaoComposeAttributeLimitsById = BuildUniqueIndex(xuanbaoComposeAttributeLimits, entry => entry.Id);
        XuanbaoRandomAttributesByItemTypeId = BuildGroupedIndex(xuanbaoRandomAttributes, entry => entry.ItemTypeId);
        XuanbaoRandomAttributesByAttributeType = BuildGroupedIndex(xuanbaoRandomAttributes, entry => entry.AttributeType);
        XuanbaoRandomAttributeByItemTypeIdAndAttributeType = BuildUniqueIndex(xuanbaoRandomAttributes, entry => (entry.ItemTypeId, entry.AttributeType));
        ChristmasNpcsById = BuildGroupedIndex(christmasNpcs, entry => entry.Id);
        ChristmasNpcsByMapId = BuildGroupedIndex(christmasNpcs, entry => entry.MapId);
        ChristmasNpcsByMapPosition = BuildGroupedIndex(christmasNpcs, entry => (entry.MapId, entry.X, entry.Y));
        ChristmasNpcsByLookface = BuildGroupedIndex(christmasNpcs, entry => entry.Lookface);
        TutorTypesById = BuildUniqueIndex(tutorTypes, entry => entry.Id);
        TutorTypesByMinLevel = BuildGroupedIndex(tutorTypes, entry => entry.MinLevel);
        TutorBattleLimitTypesById = BuildUniqueIndex(tutorBattleLimitTypes, entry => entry.Id);
        BeastAttributesByLevel = BuildUniqueIndex(beastAttributes, entry => entry.Level);
        FateExperienceById = BuildUniqueIndex(fateExperience, entry => entry.Id);
        FateExperienceByType = BuildGroupedIndex(fateExperience, entry => entry.Type);
        FateExperienceByTypeAndLevel = BuildUniqueIndex(fateExperience, entry => (entry.Type, entry.Level));
        SpiritRatesById = BuildUniqueIndex(spiritRates, entry => entry.Id);
        SpiritRatesByType = BuildGroupedIndex(spiritRates, entry => entry.Type);
        SpiritRatesByItemTypeId = BuildGroupedIndex(spiritRates, entry => entry.ItemTypeId);
        HundredWeaponsById = BuildUniqueIndex(hundredWeapons, entry => entry.Id);
        HundredWeaponsByWeaponType = BuildGroupedIndex(hundredWeapons, entry => entry.WeaponType);
        HundredWeaponsByLevel = BuildGroupedIndex(hundredWeapons, entry => entry.Level);
        GouyuTypesById = BuildUniqueIndex(gouyuTypes, entry => entry.Id);
        GouyuTypesByType = BuildGroupedIndex(gouyuTypes, entry => entry.Type);
        GouyuTypesByLevel = BuildGroupedIndex(gouyuTypes, entry => entry.Level);
        GouyuImmortalsById = BuildUniqueIndex(gouyuImmortals, entry => entry.Id);
        SpecialEffectAttributesById = BuildUniqueIndex(specialEffectAttributes, entry => entry.Id);
        SpecialEffectAttributesByType = BuildGroupedIndex(specialEffectAttributes, entry => entry.Type);
        DaoqiDictTypesById = BuildUniqueIndex(daoqiDictTypes, entry => entry.Id);
        DaoqiDictTypesByType = BuildGroupedIndex(daoqiDictTypes, entry => entry.Type);
        DaoqiDictTypesByLevel = BuildGroupedIndex(daoqiDictTypes, entry => entry.Level);
        SyndicateFormationTypesById = BuildUniqueIndex(syndicateFormationTypes, entry => entry.Id);
        SyndicateFormationTypesByType = BuildGroupedIndex(syndicateFormationTypes, entry => entry.Type);
        SyndicateFormationTypesByLevel = BuildGroupedIndex(syndicateFormationTypes, entry => entry.Level);
        SyndicateSkillTypesById = BuildUniqueIndex(syndicateSkillTypes, entry => entry.Id);
        SyndicateSkillTypesBySkillType = BuildGroupedIndex(syndicateSkillTypes, entry => entry.SkillType);
        SyndicateSkillTypesBySkillTypeAndLevel = BuildUniqueIndex(syndicateSkillTypes, entry => (entry.SkillType, entry.Level));
        DisdainRulesById = BuildUniqueIndex(disdainRules, entry => entry.Id);
        JiangHuAttributesById = BuildUniqueIndex(jiangHuAttributes, entry => entry.Id);
        JiangHuAttributesByType = BuildGroupedIndex(jiangHuAttributes, entry => entry.AttributeType);
        JiangHuAttributesByTypeAndLevel = BuildUniqueIndex(jiangHuAttributes, entry => (entry.AttributeType, entry.Level));
        JiangHuCultivateStatusesById = BuildUniqueIndex(jiangHuCultivateStatuses, entry => entry.Id);
        RefineryBoxesByItemType = BuildUniqueIndex(refineryBoxes, entry => entry.ItemType);
        RefinerySoulsByLevel = BuildGroupedIndex(refinerySouls, entry => entry.SoulLevel);
        RefinerySoulsByItemType = BuildUniqueIndex(refinerySouls, entry => entry.ItemType);
        RefineryMaterialsByItemType = BuildUniqueIndex(refineryMaterials, entry => entry.ItemType);
        RefineryMaterialsByEffectType = BuildGroupedIndex(refineryMaterials, entry => entry.EffectType);
        RefineryMaterialsByTarget = BuildGroupedIndex(refineryMaterials, entry => entry.Target, StringComparer.OrdinalIgnoreCase);
        StateCountFilesBySourceFile = BuildUniqueIndex(stateCountFiles, entry => entry.SourceFile, StringComparer.OrdinalIgnoreCase);
        BinarySeedFilesBySourceFile = BuildUniqueIndex(binarySeedFiles, entry => entry.SourceFile, StringComparer.OrdinalIgnoreCase);
        EmptySeedFilesBySourceFile = BuildUniqueIndex(emptySeedFiles, entry => entry.SourceFile, StringComparer.OrdinalIgnoreCase);
        BackupSeedFilesBySourceFile = BuildUniqueIndex(backupSeedFiles, entry => entry.SourceFile, StringComparer.OrdinalIgnoreCase);
        PokerTablesById = BuildUniqueIndex(pokerTables, entry => entry.Id);
        RouletteTablesById = BuildUniqueIndex(rouletteTables, entry => entry.Id);
        NpcTimeEntriesById = BuildUniqueIndex(npcTimeEntries, entry => entry.Id);
        ActivityTasksBySectionId = BuildUniqueIndex(activityTasks.Tasks, entry => entry.SectionId);
        ActivityTasksById = BuildUniqueIndex(activityTasks.Tasks, entry => entry.Id);
        ActivityTasksByType = BuildGroupedIndex(activityTasks.Tasks, entry => entry.Type);
        ActivityTasksByTitle = BuildGroupedIndex(activityTasks.Tasks, entry => entry.Title, StringComparer.OrdinalIgnoreCase);
        MeltingTypeRulesBySourceFile = BuildGroupedIndex(meltingTypeRules, entry => entry.SourceFile, StringComparer.OrdinalIgnoreCase);
        MeltingTypeRulesByRuleType = BuildGroupedIndex(meltingTypeRules, entry => entry.RuleType);
        MeltingTypeRulesByItemTypeId = BuildGroupedIndex(meltingTypeRules, entry => entry.ItemTypeId);
        LotteryPrizesByType = BuildGroupedIndex(lotteryPrizes, entry => entry.Type);
        LotteryPrizesByLevel = BuildGroupedIndex(lotteryPrizes, entry => entry.Level);
        LotteryPrizesByItemTypeId = BuildGroupedIndex(
            lotteryPrizes.Where(entry => entry.ItemTypeId is not null).ToArray(),
            entry => entry.ItemTypeId!.Value);
        LotteryPrizesByItemName = BuildGroupedIndex(lotteryPrizes, entry => entry.ItemName, StringComparer.OrdinalIgnoreCase);
        ArenaRecordsByPlayerId = BuildUniqueIndex(arenaRecords, entry => entry.PlayerId);
        ArenaRecordsByRank = BuildUniqueIndex(arenaRecords, entry => entry.Rank);
        TeamArenaRecordsByPlayerId = BuildUniqueIndex(teamArenaRecords, entry => entry.PlayerId);
        TeamArenaRecordsByRank = BuildUniqueIndex(teamArenaRecords, entry => entry.Rank);
        ElitePkRecordsByGroupAndSlot = BuildUniqueIndex(elitePkRecords, entry => (entry.Group, entry.Slot));
        TeamElitePkRecordsByGroupAndSlot = BuildUniqueIndex(teamElitePkRecords, entry => (entry.Group, entry.Slot));
        SkillTeamPkRecordsByGroupAndSlot = BuildUniqueIndex(skillTeamPkRecords, entry => (entry.Group, entry.Slot));
        ClassPkWarRecordsByClassAndSlot = BuildUniqueIndex(classPkWarRecords, entry => (entry.ClassId, entry.Slot));
        CombatGearRanksByCharacterId = BuildGroupedIndex(combatGearRanks, entry => entry.CharacterId);
        PrestigeRanksByCharacterId = BuildGroupedIndex(prestigeRanks, entry => entry.CharacterId);
        InnerPowerRanksByCharacterId = BuildGroupedIndex(innerPowerRanks, entry => entry.CharacterId);
        RuneRankingsByCharacterId = BuildGroupedIndex(runeRankings, entry => entry.CharacterId);
        RuneRankingsByCharacterName = BuildGroupedIndex(runeRankings, entry => entry.CharacterName, StringComparer.OrdinalIgnoreCase);
        StaticStatuesByRecordId = BuildGroupedIndex(staticStatues, entry => entry.RecordId);
        NewPlayerRewardsByIpAddress = BuildGroupedIndex(newPlayerRewards, entry => entry.IpAddress, StringComparer.OrdinalIgnoreCase);
        VotesByCharacterId = BuildUniqueIndex(votes, entry => entry.CharacterId);
        CouplesPkRecordsByPlayerName = BuildUniqueIndex(couplesPkRecords, entry => entry.PlayerName, StringComparer.OrdinalIgnoreCase);
        SocialAssociationsByOwnerId = BuildGroupedIndex(socialAssociations, entry => entry.OwnerId);
        SocialAssociationsByRelationType = BuildGroupedIndex(socialAssociations, entry => entry.RelationType);
        SocialAssociationsByTargetId = BuildGroupedIndex(socialAssociations, entry => entry.TargetId);
        SocialAssociationsByTargetName = BuildGroupedIndex(socialAssociations, entry => entry.TargetName, StringComparer.OrdinalIgnoreCase);
        ClassLevelStatsByClassName = BuildGroupedIndex(
            classLevelStats,
            entry => entry.ClassName,
            StringComparer.OrdinalIgnoreCase);
        ClassLevelStatsByClassAndLevel = BuildUniqueIndex(
            classLevelStats,
            entry => (entry.ClassName, entry.Level));
        ProfessionLevelUpsByProfessionCode = BuildUniqueIndex(
            professionLevelUps,
            entry => entry.ProfessionCode);
        ProfessionLevelBenefitsByProfessionCode = BuildUniqueIndex(
            professionLevelBenefits,
            entry => entry.ProfessionCode);
        ProfessionTitleBenefitsByProfessionAndRank = BuildUniqueIndex(
            professionTitleBenefits,
            entry => (entry.ProfessionGroup, entry.Rank));
        QuestTaskTypesById = BuildUniqueIndex(questTaskTypes, taskType => taskType.Id);
        QuestMissionsBySectionId = BuildUniqueIndex(questMissions, mission => mission.SectionId);
        QuestMissionsByMissionId = BuildGroupedIndex(questMissions, mission => mission.MissionId);
        QuestMissionsByTypeId = BuildGroupedIndex(questMissions, mission => mission.TypeId);
        QuizQuestionsByPosition = BuildUniqueIndex(quizQuestions, question => question.Position);
        QuizQuestionsByQuestion = BuildGroupedIndex(quizQuestions, question => question.Question, StringComparer.OrdinalIgnoreCase);
        QuizQuestionsByCorrectOption = BuildGroupedIndex(quizQuestions, question => question.CorrectOption);
        MagicByTypeId = BuildGroupedIndex(magic, spell => spell.TypeId);
        NpcsById = BuildGroupedIndex(npcs, npc => npc.Id);
        NpcsByMapId = BuildGroupedIndex(npcs, npc => npc.MapId);
        SobNpcsById = BuildGroupedIndex(sobNpcs, sobNpc => sobNpc.Id);
        SobNpcsByMapId = BuildGroupedIndex(sobNpcs, sobNpc => sobNpc.MapId);
        PortalsBySourceMapId = BuildGroupedIndex(portals, portal => portal.SourceMapId);
        MonsterSpawnsByGeneratorId = BuildGroupedIndex(monsterSpawns, spawn => spawn.GeneratorId);
        MonsterSpawnsByMapId = BuildGroupedIndex(monsterSpawns, spawn => spawn.MapId);
        LotteryDictionaryById = BuildUniqueIndex(lotteryDictionary, entry => entry.Id);
        LotteryDictionaryByType = BuildGroupedIndex(lotteryDictionary, entry => entry.Type);
        CollectionRewardTiersById = BuildUniqueIndex(collectionRewardTiers, tier => tier.Id);
        CollectionRewardTiersByCollectionId = collectionRewardTiers
            .GroupBy(tier => tier.CollectionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<CollectionRewardTier>)group
                    .OrderBy(tier => tier.MinInclusive)
                    .ThenBy(tier => tier.MaxInclusive)
                    .ThenBy(tier => tier.Id)
                    .ToArray());
        CollectionRewardTierByCollectionIdAndMinValue = BuildUniqueIndex(collectionRewardTiers, tier => (tier.CollectionId, tier.MinInclusive));
        TaskRewardTypesById = BuildUniqueIndex(taskRewardTypes, entry => entry.Id);
        TaskRewardTypeIniEntriesById = BuildUniqueIndex(taskRewardTypeIniEntries, entry => entry.Id);
        NeiGongSecretsById = BuildUniqueIndex(neiGongSecrets, secret => secret.Id);
        NeiGongTypesByTypeId = BuildUniqueIndex(neiGongTypes, type => type.TypeId);
        NeiGongTypesBySecretId = BuildGroupedIndex(neiGongTypes, type => type.SecretId);
        NeiGongTypeBySecretIdAndSlot = BuildUniqueIndex(neiGongTypes, type => (type.SecretId, type.Slot));
        SubProfessionsById = BuildUniqueIndex(subProfessions, entry => entry.Id);
        SubProfessionsByTitle = BuildGroupedIndex(subProfessions, entry => entry.Title, StringComparer.OrdinalIgnoreCase);
        SyndicateLevelsByLevel = BuildUniqueIndex(syndicateLevels, level => level.Level);
        StageGoalStagesById = BuildUniqueIndex(stageGoalStages, stage => stage.StageId);
        StageGoalTasksByStageId = BuildGroupedIndex(stageGoalTasks, task => task.StageId);
        StageGoalTasksByStageAndGoalId = BuildUniqueIndex(stageGoalTasks, task => (task.StageId, task.GoalId));
        ShopsById = BuildGroupedIndex(shops, shop => shop.Id);
        ShopItemsByShopId = BuildGroupedIndex(shopItems, item => item.ShopId);
        ShopItemsByItemTypeId = BuildGroupedIndex(shopItems, item => item.ItemTypeId);
        HonorShopItemsByItemTypeId = BuildGroupedIndex(honorShopItems, item => item.ItemTypeId);
        HonorShopRecommendationsByItemTypeId = BuildGroupedIndex(honorShopRecommendations, recommendation => recommendation.ItemTypeId);
        RacePointShopItemsByItemTypeId = BuildGroupedIndex(racePointShopItems, item => item.ItemTypeId);
        RacePointShopRecommendationsByItemTypeId = BuildGroupedIndex(racePointShopRecommendations, recommendation => recommendation.ItemTypeId);
        GoldenLeagueShopItemsByItemTypeId = BuildGroupedIndex(goldenLeagueShopItems, item => item.ItemTypeId);
        GoldenLeagueShopRecommendationsByItemTypeId = BuildGroupedIndex(goldenLeagueShopRecommendations, recommendation => recommendation.ItemTypeId);
        BoothsById = BuildGroupedIndex(booths, booth => booth.Id);
        BoothItemsByBoothId = BuildGroupedIndex(boothItems, item => item.BoothId);
        BoothItemsByItemTypeId = BuildGroupedIndex(boothItems, item => item.ItemTypeId);
    }

}
