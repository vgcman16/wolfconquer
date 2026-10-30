namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    public static WorldDataCatalog Create(
        SeedManifest manifest,
        IReadOnlyList<ClientConfigRecord> clientConfigs,
        IReadOnlyList<ItemType> items,
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
        IReadOnlyList<TeamArenaRecord>? teamArenaRecords = null,
        IReadOnlyList<ItemTypeSub>? itemTypeSubs = null,
        IReadOnlyList<LegacyGameMapEntry>? legacyGameMaps = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(clientConfigs);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(itemAddBonuses);
        ArgumentNullException.ThrowIfNull(levelExperience);
        ArgumentNullException.ThrowIfNull(runeLevelExperience);
        ArgumentNullException.ThrowIfNull(runeStorageAttributes);
        ArgumentNullException.ThrowIfNull(itemRefineEffects);
        ArgumentNullException.ThrowIfNull(itemRefineEffectEx);
        ArgumentNullException.ThrowIfNull(itemRefineUpgrades);
        ArgumentNullException.ThrowIfNull(transformInfo);
        ArgumentNullException.ThrowIfNull(furniture);
        ArgumentNullException.ThrowIfNull(hairColorTypes);
        ArgumentNullException.ThrowIfNull(hairFaceStorageTypes);
        ArgumentNullException.ThrowIfNull(coatStorageTypes);
        ArgumentNullException.ThrowIfNull(coatStorageAttributes);
        ArgumentNullException.ThrowIfNull(coatColorRules);
        ArgumentNullException.ThrowIfNull(xuanbaoAdditionAttributes);
        ArgumentNullException.ThrowIfNull(xuanbaoComposeAttributeLimits);
        ArgumentNullException.ThrowIfNull(xuanbaoRandomAttributes);
        ArgumentNullException.ThrowIfNull(christmasNpcs);
        ArgumentNullException.ThrowIfNull(tutorTypes);
        ArgumentNullException.ThrowIfNull(tutorBattleLimitTypes);
        ArgumentNullException.ThrowIfNull(beastAttributes);
        ArgumentNullException.ThrowIfNull(fateExperience);
        ArgumentNullException.ThrowIfNull(spiritRates);
        ArgumentNullException.ThrowIfNull(hundredWeapons);
        ArgumentNullException.ThrowIfNull(gouyuTypes);
        ArgumentNullException.ThrowIfNull(gouyuImmortals);
        ArgumentNullException.ThrowIfNull(specialEffectAttributes);
        ArgumentNullException.ThrowIfNull(daoqiDictTypes);
        ArgumentNullException.ThrowIfNull(syndicateFormationTypes);
        ArgumentNullException.ThrowIfNull(syndicateSkillTypes);
        ArgumentNullException.ThrowIfNull(disdainRules);
        ArgumentNullException.ThrowIfNull(jiangHuAttributes);
        ArgumentNullException.ThrowIfNull(jiangHuCultivateStatuses);
        ArgumentNullException.ThrowIfNull(refineryBoxes);
        ArgumentNullException.ThrowIfNull(refinerySouls);
        ArgumentNullException.ThrowIfNull(refineryMaterials);
        ArgumentNullException.ThrowIfNull(stateCountFiles);
        ArgumentNullException.ThrowIfNull(binarySeedFiles);
        ArgumentNullException.ThrowIfNull(emptySeedFiles);
        ArgumentNullException.ThrowIfNull(backupSeedFiles);
        ArgumentNullException.ThrowIfNull(pokerTables);
        ArgumentNullException.ThrowIfNull(rouletteTables);
        ArgumentNullException.ThrowIfNull(npcTimeEntries);
        ArgumentNullException.ThrowIfNull(activityTasks);
        ArgumentNullException.ThrowIfNull(meltingTypeRules);
        ArgumentNullException.ThrowIfNull(lotteryPrizes);
        ArgumentNullException.ThrowIfNull(arenaRecords);
        ArgumentNullException.ThrowIfNull(elitePkRecords);
        ArgumentNullException.ThrowIfNull(teamElitePkRecords);
        ArgumentNullException.ThrowIfNull(skillTeamPkRecords);
        ArgumentNullException.ThrowIfNull(classPkWarRecords);
        ArgumentNullException.ThrowIfNull(combatGearRanks);
        ArgumentNullException.ThrowIfNull(prestigeRanks);
        ArgumentNullException.ThrowIfNull(innerPowerRanks);
        ArgumentNullException.ThrowIfNull(runeRankings);
        ArgumentNullException.ThrowIfNull(staticStatues);
        ArgumentNullException.ThrowIfNull(newPlayerRewards);
        ArgumentNullException.ThrowIfNull(votes);
        ArgumentNullException.ThrowIfNull(couplesPkRecords);
        ArgumentNullException.ThrowIfNull(socialAssociations);
        ArgumentNullException.ThrowIfNull(guildWarInfo);
        ArgumentNullException.ThrowIfNull(superGuildWarInfo);
        ArgumentNullException.ThrowIfNull(classLevelStats);
        ArgumentNullException.ThrowIfNull(professionLevelUps);
        ArgumentNullException.ThrowIfNull(professionLevelBenefits);
        ArgumentNullException.ThrowIfNull(professionTitleBenefits);
        ArgumentNullException.ThrowIfNull(questInfo);
        ArgumentNullException.ThrowIfNull(questTaskTypes);
        ArgumentNullException.ThrowIfNull(questMissions);
        ArgumentNullException.ThrowIfNull(quizQuestions);
        ArgumentNullException.ThrowIfNull(magic);
        ArgumentNullException.ThrowIfNull(magicEffects);
        ArgumentNullException.ThrowIfNull(magicTypeOperations);
        ArgumentNullException.ThrowIfNull(titleTypes);
        ArgumentNullException.ThrowIfNull(combatGear);
        ArgumentNullException.ThrowIfNull(traps);
        ArgumentNullException.ThrowIfNull(npcs);
        ArgumentNullException.ThrowIfNull(sobNpcs);
        ArgumentNullException.ThrowIfNull(portals);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(mapNames);
        ArgumentNullException.ThrowIfNull(monsters);
        ArgumentNullException.ThrowIfNull(monsterSpawns);
        ArgumentNullException.ThrowIfNull(lotteryDictionary);
        ArgumentNullException.ThrowIfNull(collectionRewardTiers);
        ArgumentNullException.ThrowIfNull(taskRewardTypes);
        ArgumentNullException.ThrowIfNull(taskRewardTypeIniEntries);
        ArgumentNullException.ThrowIfNull(neiGongHeader);
        ArgumentNullException.ThrowIfNull(neiGongSecrets);
        ArgumentNullException.ThrowIfNull(neiGongTypes);
        ArgumentNullException.ThrowIfNull(subProfessions);
        ArgumentNullException.ThrowIfNull(syndicateLevels);
        ArgumentNullException.ThrowIfNull(stageGoalInfo);
        ArgumentNullException.ThrowIfNull(stageGoalStages);
        ArgumentNullException.ThrowIfNull(stageGoalTasks);
        ArgumentNullException.ThrowIfNull(shops);
        ArgumentNullException.ThrowIfNull(shopItems);
        ArgumentNullException.ThrowIfNull(exchangeShopGoods);
        ArgumentNullException.ThrowIfNull(exchangeShopGoodsEx);
        ArgumentNullException.ThrowIfNull(exchangeShopLevels);
        ArgumentNullException.ThrowIfNull(honorShopItems);
        ArgumentNullException.ThrowIfNull(honorShopRecommendations);
        ArgumentNullException.ThrowIfNull(racePointShopItems);
        ArgumentNullException.ThrowIfNull(racePointShopRecommendations);
        ArgumentNullException.ThrowIfNull(goldenLeagueShopItems);
        ArgumentNullException.ThrowIfNull(goldenLeagueShopRecommendations);
        ArgumentNullException.ThrowIfNull(goldenLeagueShopConfig);
        ArgumentNullException.ThrowIfNull(booths);
        ArgumentNullException.ThrowIfNull(boothItems);
        legacyGameMaps ??= [];
        itemTypeSubs ??= [];
        var hasTeamArenaRecordsInput = teamArenaRecords is not null;
        teamArenaRecords ??= [];
        var diagnostics = new List<WorldDataDiagnostic>();
        AddCountDiagnostics(manifest, diagnostics, "clientConfigs", clientConfigs.Count);
        AddCountDiagnostics(manifest, diagnostics, "items", items.Count);
        if (manifest.Counts.ContainsKey("itemTypeSubs"))
        {
            AddCountDiagnostics(manifest, diagnostics, "itemTypeSubs", itemTypeSubs.Count);
        }

        AddCountDiagnostics(manifest, diagnostics, "itemAddBonuses", itemAddBonuses.Count);
        AddCountDiagnostics(manifest, diagnostics, "levelExperience", levelExperience.Count);
        AddCountDiagnostics(manifest, diagnostics, "runeLevelExperience", runeLevelExperience.Count);
        AddCountDiagnostics(manifest, diagnostics, "runeStorageAttributes", runeStorageAttributes.Count);
        AddCountDiagnostics(manifest, diagnostics, "itemRefineEffects", itemRefineEffects.Count);
        AddCountDiagnostics(manifest, diagnostics, "itemRefineEffectEx", itemRefineEffectEx.Count);
        AddCountDiagnostics(manifest, diagnostics, "itemRefineUpgrades", itemRefineUpgrades.Count);
        AddCountDiagnostics(manifest, diagnostics, "transformInfo", transformInfo.Count);
        AddCountDiagnostics(manifest, diagnostics, "furniture", furniture.Count);
        AddCountDiagnostics(manifest, diagnostics, "hairColorTypes", hairColorTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "hairFaceStorageTypes", hairFaceStorageTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "coatStorageTypes", coatStorageTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "coatStorageAttributes", coatStorageAttributes.Count);
        AddCountDiagnostics(manifest, diagnostics, "coatColorRules", coatColorRules.Count);
        AddCountDiagnostics(manifest, diagnostics, "xuanbaoAdditionAttributes", xuanbaoAdditionAttributes.Count);
        AddCountDiagnostics(manifest, diagnostics, "xuanbaoComposeAttributeLimits", xuanbaoComposeAttributeLimits.Count);
        AddCountDiagnostics(manifest, diagnostics, "xuanbaoRandomAttributes", xuanbaoRandomAttributes.Count);
        AddCountDiagnostics(manifest, diagnostics, "christmasNpcs", christmasNpcs.Count);
        AddCountDiagnostics(manifest, diagnostics, "tutorTypes", tutorTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "tutorBattleLimitTypes", tutorBattleLimitTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "beastAttributes", beastAttributes.Count);
        AddCountDiagnostics(manifest, diagnostics, "fateExperience", fateExperience.Count);
        AddCountDiagnostics(manifest, diagnostics, "spiritRates", spiritRates.Count);
        AddCountDiagnostics(manifest, diagnostics, "hundredWeapons", hundredWeapons.Count);
        AddCountDiagnostics(manifest, diagnostics, "gouyuTypes", gouyuTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "gouyuImmortals", gouyuImmortals.Count);
        AddCountDiagnostics(manifest, diagnostics, "specialEffectAttributes", specialEffectAttributes.Count);
        AddCountDiagnostics(manifest, diagnostics, "daoqiDictTypes", daoqiDictTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "syndicateFormationTypes", syndicateFormationTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "syndicateSkillTypes", syndicateSkillTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "disdainRules", disdainRules.Count);
        AddCountDiagnostics(manifest, diagnostics, "jiangHuAttributes", jiangHuAttributes.Count);
        AddCountDiagnostics(manifest, diagnostics, "jiangHuCultivateStatuses", jiangHuCultivateStatuses.Count);
        AddCountDiagnostics(manifest, diagnostics, "refineryBoxes", refineryBoxes.Count);
        AddCountDiagnostics(manifest, diagnostics, "refinerySouls", refinerySouls.Count);
        AddCountDiagnostics(manifest, diagnostics, "refineryMaterials", refineryMaterials.Count);
        AddCountDiagnostics(manifest, diagnostics, "stateCountFiles", stateCountFiles.Count);
        AddCountDiagnostics(manifest, diagnostics, "binarySeedFiles", binarySeedFiles.Count);
        AddCountDiagnostics(manifest, diagnostics, "emptySeedFiles", emptySeedFiles.Count);
        AddCountDiagnostics(manifest, diagnostics, "backupSeedFiles", backupSeedFiles.Count);
        AddCountDiagnostics(manifest, diagnostics, "pokerTables", pokerTables.Count);
        AddCountDiagnostics(manifest, diagnostics, "rouletteTables", rouletteTables.Count);
        AddCountDiagnostics(manifest, diagnostics, "npcTimeEntries", npcTimeEntries.Count);
        AddCountDiagnostics(manifest, diagnostics, "activityTasks", activityTasks.Tasks.Count);
        AddCountDiagnostics(manifest, diagnostics, "meltingTypeRules", meltingTypeRules.Count);
        AddCountDiagnostics(manifest, diagnostics, "lotteryPrizes", lotteryPrizes.Count);
        AddCountDiagnostics(manifest, diagnostics, "arenaRecords", arenaRecords.Count);
        if (hasTeamArenaRecordsInput || manifest.Counts.ContainsKey("teamArenaRecords"))
        {
            AddCountDiagnostics(manifest, diagnostics, "teamArenaRecords", teamArenaRecords.Count);
        }
        AddCountDiagnostics(manifest, diagnostics, "elitePkRecords", elitePkRecords.Count);
        AddCountDiagnostics(manifest, diagnostics, "teamElitePkRecords", teamElitePkRecords.Count);
        AddCountDiagnostics(manifest, diagnostics, "skillTeamPkRecords", skillTeamPkRecords.Count);
        AddCountDiagnostics(manifest, diagnostics, "classPkWarRecords", classPkWarRecords.Count);
        AddCountDiagnostics(manifest, diagnostics, "combatGearRanks", combatGearRanks.Count);
        AddCountDiagnostics(manifest, diagnostics, "prestigeRanks", prestigeRanks.Count);
        AddCountDiagnostics(manifest, diagnostics, "innerPowerRanks", innerPowerRanks.Count);
        AddCountDiagnostics(manifest, diagnostics, "runeRankings", runeRankings.Count);
        AddCountDiagnostics(manifest, diagnostics, "staticStatues", staticStatues.Count);
        AddCountDiagnostics(manifest, diagnostics, "newPlayerRewards", newPlayerRewards.Count);
        AddCountDiagnostics(manifest, diagnostics, "votes", votes.Count);
        AddCountDiagnostics(manifest, diagnostics, "couplesPkRecords", couplesPkRecords.Count);
        AddCountDiagnostics(manifest, diagnostics, "socialAssociations", socialAssociations.Count);
        AddCountDiagnostics(manifest, diagnostics, "guildWarInfo", 1);
        AddCountDiagnostics(manifest, diagnostics, "superGuildWarInfo", 1);
        AddCountDiagnostics(manifest, diagnostics, "classLevelStats", classLevelStats.Count);
        AddCountDiagnostics(manifest, diagnostics, "professionLevelUps", professionLevelUps.Count);
        AddCountDiagnostics(manifest, diagnostics, "professionLevelBenefits", professionLevelBenefits.Count);
        AddCountDiagnostics(manifest, diagnostics, "professionTitleBenefits", professionTitleBenefits.Count);
        AddCountDiagnostics(manifest, diagnostics, "questInfo", 1);
        AddCountDiagnostics(manifest, diagnostics, "questTaskTypes", questTaskTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "questMissions", questMissions.Count);
        AddCountDiagnostics(manifest, diagnostics, "quizQuestions", quizQuestions.Count);
        AddCountDiagnostics(manifest, diagnostics, "magic", magic.Count);
        AddCountDiagnostics(manifest, diagnostics, "magicEffects", magicEffects.Count);
        diagnostics.AddRange(MagicEffectRuneClassificationDecoder.Warnings(magicEffects));
        AddCountDiagnostics(manifest, diagnostics, "magicTypeOperations", magicTypeOperations.Count);
        AddCountDiagnostics(manifest, diagnostics, "titleTypes", titleTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "combatGear", combatGear.Count);
        AddCountDiagnostics(manifest, diagnostics, "traps", traps.Count);
        AddCountDiagnostics(manifest, diagnostics, "npcs", npcs.Count);
        AddCountDiagnostics(manifest, diagnostics, "sobNpcs", sobNpcs.Count);
        AddCountDiagnostics(manifest, diagnostics, "portals", portals.Count);
        AddCountDiagnostics(manifest, diagnostics, "maps", maps.Count);
        AddCountDiagnostics(manifest, diagnostics, "mapNames", mapNames.Count);
        if (manifest.Counts.ContainsKey("legacyGameMaps"))
        {
            AddCountDiagnostics(manifest, diagnostics, "legacyGameMaps", legacyGameMaps.Count);
        }

        AddCountDiagnostics(manifest, diagnostics, "monsters", monsters.Count);
        AddCountDiagnostics(manifest, diagnostics, "monsterSpawns", monsterSpawns.Count);
        AddCountDiagnostics(manifest, diagnostics, "lotteryDictionary", lotteryDictionary.Count);
        AddCountDiagnostics(manifest, diagnostics, "collectionRewardTiers", collectionRewardTiers.Count);
        AddCountDiagnostics(manifest, diagnostics, "taskRewardTypes", taskRewardTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "taskRewardTypeIniEntries", taskRewardTypeIniEntries.Count);
        AddCountDiagnostics(manifest, diagnostics, "neiGongHeader", 1);
        AddCountDiagnostics(manifest, diagnostics, "neiGongSecrets", neiGongSecrets.Count);
        AddCountDiagnostics(manifest, diagnostics, "neiGongTypes", neiGongTypes.Count);
        AddCountDiagnostics(manifest, diagnostics, "subProfessions", subProfessions.Count);
        AddCountDiagnostics(manifest, diagnostics, "syndicateLevels", syndicateLevels.Count);
        AddCountDiagnostics(manifest, diagnostics, "stageGoalInfo", 1);
        AddCountDiagnostics(manifest, diagnostics, "stageGoalStages", stageGoalStages.Count);
        AddCountDiagnostics(manifest, diagnostics, "stageGoalTasks", stageGoalTasks.Count);
        AddCountDiagnostics(manifest, diagnostics, "shops", shops.Count);
        AddCountDiagnostics(manifest, diagnostics, "shopItems", shopItems.Count);
        AddCountDiagnostics(manifest, diagnostics, "exchangeShopGoods", exchangeShopGoods.Count);
        AddCountDiagnostics(manifest, diagnostics, "exchangeShopGoodsEx", exchangeShopGoodsEx.Count);
        AddCountDiagnostics(manifest, diagnostics, "exchangeShopLevels", exchangeShopLevels.Count);
        AddCountDiagnostics(manifest, diagnostics, "honorShopItems", honorShopItems.Count);
        AddCountDiagnostics(manifest, diagnostics, "honorShopRecommendations", honorShopRecommendations.Count);
        AddCountDiagnostics(manifest, diagnostics, "racePointShopItems", racePointShopItems.Count);
        AddCountDiagnostics(manifest, diagnostics, "racePointShopRecommendations", racePointShopRecommendations.Count);
        AddCountDiagnostics(manifest, diagnostics, "goldenLeagueShopItems", goldenLeagueShopItems.Count);
        AddCountDiagnostics(manifest, diagnostics, "goldenLeagueShopRecommendations", goldenLeagueShopRecommendations.Count);
        AddCountDiagnostics(manifest, diagnostics, "goldenLeagueShopConfig", goldenLeagueShopConfig.Count);
        AddCountDiagnostics(manifest, diagnostics, "booths", booths.Count);
        AddCountDiagnostics(manifest, diagnostics, "boothItems", boothItems.Count);
        AddDuplicateDiagnostics(diagnostics, items, item => item.Id, "DuplicateItemId", "item id");
        AddDuplicateDiagnostics(diagnostics, clientConfigs, entry => entry.ServerId, "DuplicateClientConfigServerId", "client config server id");
        AddDuplicateDiagnostics(diagnostics, levelExperience, entry => (entry.Type, entry.Level), "DuplicateLevelExperienceKey", "level experience key");
        AddDuplicateDiagnostics(diagnostics, runeLevelExperience, entry => entry.Id, "DuplicateRuneLevelExperienceId", "rune level experience id");
        AddDuplicateDiagnostics(diagnostics, runeLevelExperience, entry => (entry.RuneType, entry.Level), "DuplicateRuneLevelExperienceKey", "rune level experience key");
        AddDuplicateDiagnostics(diagnostics, runeStorageAttributes, entry => entry.Id, "DuplicateRuneStorageAttributeId", "rune storage attribute id");
        AddDuplicateDiagnostics(diagnostics, itemRefineEffects, entry => entry.Id, "DuplicateItemRefineEffectId", "item refine effect id");
        AddDuplicateDiagnostics(diagnostics, itemRefineEffectEx, entry => entry.Id, "DuplicateItemRefineEffectExId", "item refine effect ex id");
        AddDuplicateDiagnostics(diagnostics, itemRefineUpgrades, entry => (entry.Id, entry.Level), "DuplicateItemRefineUpgradeKey", "item refine upgrade key");
        AddDuplicateDiagnostics(diagnostics, transformInfo, entry => (entry.TransformId, entry.Level), "DuplicateTransformInfoKey", "transform info key");
        AddDuplicateDiagnostics(diagnostics, furniture, entry => entry.Id, "DuplicateFurnitureId", "furniture id");
        AddDuplicateDiagnostics(diagnostics, hairColorTypes, entry => (entry.HairColorId, entry.ColorIndex), "DuplicateHairColorTypeKey", "hair color type key");
        AddDuplicateDiagnostics(diagnostics, hairFaceStorageTypes, entry => (entry.CategoryType, entry.Id), "DuplicateHairFaceStorageTypeKey", "hair/face storage type key");
        AddDuplicateDiagnostics(diagnostics, coatStorageTypes, entry => entry.ItemTypeId, "DuplicateCoatStorageTypeItemTypeId", "coat storage item type id");
        AddDuplicateDiagnostics(diagnostics, coatStorageAttributes, entry => entry.Id, "DuplicateCoatStorageAttributeId", "coat storage attribute id");
        AddDuplicateDiagnostics(diagnostics, coatColorRules, entry => entry.ItemTypeId, "DuplicateCoatColorRuleItemTypeId", "coat color rule item type id");
        AddDuplicateDiagnostics(diagnostics, xuanbaoAdditionAttributes, entry => entry.Id, "DuplicateXuanbaoAdditionAttributeId", "xuanbao addition attribute id");
        AddDuplicateDiagnostics(diagnostics, xuanbaoAdditionAttributes, entry => (entry.AttributeType, entry.Level), "DuplicateXuanbaoAdditionAttributeKey", "xuanbao addition attribute type/level");
        AddDuplicateDiagnostics(diagnostics, xuanbaoComposeAttributeLimits, entry => entry.Id, "DuplicateXuanbaoComposeAttributeLimitId", "xuanbao compose attribute limit id");
        AddDuplicateDiagnostics(diagnostics, xuanbaoRandomAttributes, entry => (entry.ItemTypeId, entry.AttributeType), "DuplicateXuanbaoRandomAttributeKey", "xuanbao random item/type");
        AddDuplicateDiagnostics(diagnostics, tutorTypes, entry => entry.Id, "DuplicateTutorTypeId", "tutor type id");
        AddDuplicateDiagnostics(diagnostics, tutorBattleLimitTypes, entry => entry.Id, "DuplicateTutorBattleLimitTypeId", "tutor battle limit type id");
        AddDuplicateDiagnostics(diagnostics, beastAttributes, entry => entry.Level, "DuplicateBeastAttributeLevel", "beast attribute level");
        AddDuplicateDiagnostics(diagnostics, fateExperience, entry => entry.Id, "DuplicateFateExperienceId", "fate experience id");
        AddDuplicateDiagnostics(diagnostics, fateExperience, entry => (entry.Type, entry.Level), "DuplicateFateExperienceKey", "fate experience type/level");
        AddDuplicateDiagnostics(diagnostics, spiritRates, entry => entry.Id, "DuplicateSpiritRateId", "spirit rate id");
        AddDuplicateDiagnostics(diagnostics, hundredWeapons, entry => entry.Id, "DuplicateHundredWeaponId", "hundred weapon id");
        AddDuplicateDiagnostics(diagnostics, gouyuTypes, entry => entry.Id, "DuplicateGouyuTypeId", "gouyu type id");
        AddDuplicateDiagnostics(diagnostics, gouyuImmortals, entry => entry.Id, "DuplicateGouyuImmortalId", "gouyu immortal id");
        AddDuplicateDiagnostics(diagnostics, specialEffectAttributes, entry => entry.Id, "DuplicateSpecialEffectAttributeId", "special effect attribute id");
        AddDuplicateDiagnostics(diagnostics, daoqiDictTypes, entry => entry.Id, "DuplicateDaoqiDictTypeId", "daoqi dict type id");
        AddDuplicateDiagnostics(diagnostics, syndicateFormationTypes, entry => entry.Id, "DuplicateSyndicateFormationTypeId", "syndicate formation type id");
        AddDuplicateDiagnostics(diagnostics, syndicateSkillTypes, entry => entry.Id, "DuplicateSyndicateSkillTypeId", "syndicate skill type id");
        AddDuplicateDiagnostics(diagnostics, syndicateSkillTypes, entry => (entry.SkillType, entry.Level), "DuplicateSyndicateSkillTypeKey", "syndicate skill type/level");
        AddDuplicateDiagnostics(diagnostics, disdainRules, entry => entry.Id, "DuplicateDisdainRuleId", "disdain rule id");
        AddDuplicateDiagnostics(diagnostics, jiangHuAttributes, entry => entry.Id, "DuplicateJiangHuAttributeId", "Jiang Hu attribute id");
        AddDuplicateDiagnostics(diagnostics, jiangHuAttributes, entry => (entry.AttributeType, entry.Level), "DuplicateJiangHuAttributeKey", "Jiang Hu attribute type/level");
        AddDuplicateDiagnostics(diagnostics, jiangHuCultivateStatuses, entry => entry.Id, "DuplicateJiangHuCultivateStatusId", "Jiang Hu cultivate status id");
        AddDuplicateDiagnostics(diagnostics, refineryBoxes, entry => entry.ItemType, "DuplicateRefineryBoxItemType", "refinery box item type");
        AddDuplicateDiagnostics(diagnostics, refinerySouls, entry => entry.ItemType, "DuplicateRefinerySoulItemType", "refinery soul item type");
        AddDuplicateDiagnostics(diagnostics, refineryMaterials, entry => entry.ItemType, "DuplicateRefineryMaterialItemType", "refinery material item type");
        AddDuplicateDiagnostics(
            diagnostics,
            stateCountFiles,
            entry => entry.SourceFile,
            "DuplicateStateCountSourceFile",
            "state count source file",
            StringComparer.OrdinalIgnoreCase);
        AddDuplicateDiagnostics(
            diagnostics,
            binarySeedFiles,
            entry => entry.SourceFile,
            "DuplicateBinarySeedSourceFile",
            "binary seed source file",
            StringComparer.OrdinalIgnoreCase);
        AddDuplicateDiagnostics(
            diagnostics,
            emptySeedFiles,
            entry => entry.SourceFile,
            "DuplicateEmptySeedSourceFile",
            "empty seed source file",
            StringComparer.OrdinalIgnoreCase);
        AddDuplicateDiagnostics(
            diagnostics,
            backupSeedFiles,
            entry => entry.SourceFile,
            "DuplicateBackupSeedSourceFile",
            "backup seed source file",
            StringComparer.OrdinalIgnoreCase);
        AddDuplicateDiagnostics(diagnostics, pokerTables, entry => entry.Id, "DuplicatePokerTableId", "poker table id");
        AddDuplicateDiagnostics(diagnostics, rouletteTables, entry => entry.Id, "DuplicateRouletteTableId", "roulette table id");
        AddDuplicateDiagnostics(diagnostics, npcTimeEntries, entry => entry.Id, "DuplicateNpcTimeEntryId", "NPC time entry id");
        AddDuplicateDiagnostics(diagnostics, activityTasks.Tasks, entry => entry.SectionId, "DuplicateActivityTaskSectionId", "activity task section id");
        AddDuplicateDiagnostics(diagnostics, activityTasks.Tasks, entry => entry.Id, "DuplicateActivityTaskId", "activity task id");
        AddDuplicateDiagnostics(diagnostics, arenaRecords, entry => entry.PlayerId, "DuplicateArenaPlayerId", "arena player id");
        AddDuplicateDiagnostics(diagnostics, arenaRecords, entry => entry.Rank, "DuplicateArenaRank", "arena rank");
        AddDuplicateDiagnostics(diagnostics, teamArenaRecords, entry => entry.PlayerId, "DuplicateTeamArenaPlayerId", "team arena player id");
        AddDuplicateDiagnostics(diagnostics, teamArenaRecords, entry => entry.Rank, "DuplicateTeamArenaRank", "team arena rank");
        AddDuplicateDiagnostics(diagnostics, elitePkRecords, entry => (entry.Group, entry.Slot), "DuplicateElitePkKey", "Elite PK group/slot");
        AddDuplicateDiagnostics(diagnostics, teamElitePkRecords, entry => (entry.Group, entry.Slot), "DuplicateTeamElitePkKey", "Team Elite PK group/slot");
        AddDuplicateDiagnostics(diagnostics, skillTeamPkRecords, entry => (entry.Group, entry.Slot), "DuplicateSkillTeamPkKey", "Skill Team PK group/slot");
        AddDuplicateDiagnostics(diagnostics, classPkWarRecords, entry => (entry.ClassId, entry.Slot), "DuplicateClassPkWarKey", "class PK war class/slot");
        AddDuplicateDiagnostics(diagnostics, votes, entry => entry.CharacterId, "DuplicateVoteCharacterId", "vote character id");
        AddDuplicateDiagnostics(
            diagnostics,
            couplesPkRecords,
            entry => entry.PlayerName,
            "DuplicateCouplesPkPlayerName",
            "couples PK player name",
            StringComparer.OrdinalIgnoreCase);
        AddDuplicateDiagnostics(diagnostics, classLevelStats, entry => (entry.ClassName, entry.Level), "DuplicateClassLevelStatKey", "class level stat key");
        diagnostics.AddRange(guildWarInfo.Diagnostics);
        diagnostics.AddRange(superGuildWarInfo.Diagnostics);
        AddDuplicateDiagnostics(diagnostics, professionLevelUps, entry => entry.ProfessionCode, "DuplicateProfessionLevelUpCode", "profession level-up code");
        AddDuplicateDiagnostics(diagnostics, professionLevelBenefits, entry => entry.ProfessionCode, "DuplicateProfessionLevelBenefitCode", "profession level benefit code");
        AddDuplicateDiagnostics(diagnostics, professionTitleBenefits, entry => (entry.ProfessionGroup, entry.Rank), "DuplicateProfessionTitleBenefitKey", "profession title benefit key");
        AddDuplicateDiagnostics(diagnostics, questMissions, mission => mission.MissionId, "DuplicateQuestMissionId", "quest mission id");
        AddDuplicateDiagnostics(diagnostics, quizQuestions, entry => entry.Position, "DuplicateQuizQuestionPosition", "quiz question position");
        AddDuplicateDiagnostics(diagnostics, magicTypeOperations, entry => entry.Id, "DuplicateMagicTypeOperationId", "magic type operation id");
        AddDuplicateDiagnostics(diagnostics, titleTypes, title => title.Id, "DuplicateTitleTypeId", "title type id");
        AddDuplicateDiagnostics(diagnostics, combatGear, gear => gear.Id, "DuplicateCombatGearId", "combat gear id");
        AddDuplicateDiagnostics(diagnostics, combatGear, gear => (gear.GearTypeId, gear.Level), "DuplicateCombatGearTypeLevel", "combat gear type/level");
        AddDuplicateDiagnostics(diagnostics, mapNames, mapName => mapName.MapId, "DuplicateMapNameId", "map name id");
        AddDuplicateDiagnostics(diagnostics, collectionRewardTiers, tier => tier.Id, "DuplicateCollectionRewardTierId", "collection reward tier id");
        AddDuplicateDiagnostics(diagnostics, collectionRewardTiers, tier => (tier.CollectionId, tier.MinInclusive), "DuplicateCollectionRewardTierKey", "collection reward tier key");
        AddDuplicateDiagnostics(diagnostics, taskRewardTypeIniEntries, entry => entry.Id, "DuplicateTaskRewardTypeIniId", "task reward type ini id");
        AddDuplicateDiagnostics(diagnostics, neiGongSecrets, secret => secret.Id, "DuplicateNeiGongSecretId", "NeiGong secret id");
        AddDuplicateDiagnostics(diagnostics, neiGongTypes, type => type.TypeId, "DuplicateNeiGongTypeId", "NeiGong type id");
        AddDuplicateDiagnostics(diagnostics, neiGongTypes, type => (type.SecretId, type.Slot), "DuplicateNeiGongTypeSlot", "NeiGong secret/slot");
        AddDuplicateDiagnostics(diagnostics, subProfessions, entry => entry.Id, "DuplicateSubProfessionId", "sub-profession id");
        AddDuplicateDiagnostics(diagnostics, syndicateLevels, level => level.Level, "DuplicateSyndicateLevel", "syndicate level");
        AddDuplicateDiagnostics(diagnostics, npcs, npc => npc.Id, "DuplicateNpcId", "NPC id");
        AddDuplicateDiagnostics(diagnostics, sobNpcs, sobNpc => sobNpc.Id, "DuplicateSobNpcId", "SobNpc id");
        var generatorBackedMonsterSpawns = monsterSpawns
            .Where(spawn => !IsLegacyLoadMapMonstersSpawn(spawn))
            .ToArray();
        AddDuplicateDiagnostics(
            diagnostics,
            generatorBackedMonsterSpawns,
            spawn => spawn.GeneratorId,
            "DuplicateMonsterSpawnGeneratorId",
            "monster spawn generator id");

        var mapIds = maps
            .Select(map => map.Id)
            .Concat(mapNames.Select(mapName => mapName.MapId))
            .Concat(legacyGameMaps.Select(map => map.MapId))
            .ToHashSet();
        foreach (var alias in LegacyMapAliasResolver.KnownAliases)
        {
            if (mapIds.Contains(alias.TerrainSourceMapId))
            {
                mapIds.Add(alias.RequestedMapId);
            }
        }

        var itemIds = items.Select(item => item.Id).ToHashSet();
        var magicTypeIds = magic.Select(spell => spell.TypeId).ToHashSet();
        var questMissionIds = questMissions.Select(mission => mission.MissionId).ToHashSet();
        var questNavigationMapIds = questMissions
            .SelectMany(mission => mission.BeginNpcAnchors.Concat(mission.FinishNpcAnchors))
            .Where(anchor => anchor.MapId is > 0 && IsCompleteQuestNavigationAnchor(anchor))
            .Select(anchor => anchor.MapId!.Value)
            .ToHashSet();
        var questNpcIds = npcs.Select(npc => npc.Id).Concat(sobNpcs.Select(sobNpc => sobNpc.Id)).ToHashSet();
        var monsterIds = monsters.Select(monster => monster.Id).ToHashSet();
        var neiGongSecretIds = neiGongSecrets.Select(secret => secret.Id).ToHashSet();

        foreach (var type in neiGongTypes.Where(type => !neiGongSecretIds.Contains(type.SecretId)))
        {
            diagnostics.Add(new WorldDataDiagnostic(
                "MissingNeiGongSecret",
                $"NeiGong type {type.TypeId} references missing secret {type.SecretId}."));
        }

        foreach (var transformId in transformInfo.Select(entry => entry.TransformId).Distinct().Where(id => !magicTypeIds.Contains(id)))
        {
            diagnostics.Add(new WorldDataDiagnostic(
                "MissingTransformMagicType",
                $"TransformInfo references missing magic type id {transformId}."));
        }

        foreach (var magicTypeId in magicTypeOperations.SelectMany(entry => entry.ReferencedMagicTypeIds).Distinct().Where(id => !magicTypeIds.Contains(id)))
        {
            diagnostics.Add(new WorldDataDiagnostic(
                "MissingMagicTypeOperationReference",
                $"MagicTypeOperation references missing magic type id {magicTypeId}."));
        }

        foreach (var entry in furniture)
        {
            if (!mapIds.Contains(entry.MapId))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingFurnitureMap",
                    $"Furniture {entry.Id} references missing map {entry.MapId}."));
            }

            if (!itemIds.Contains(entry.ItemTypeId))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingFurnitureItem",
                    $"Furniture {entry.Id} references missing item {entry.ItemTypeId}."));
            }
        }

        foreach (var entry in christmasNpcs.Where(entry => !mapIds.Contains(entry.MapId)))
        {
            diagnostics.Add(new WorldDataDiagnostic(
                "MissingChristmasNpcMap",
                $"Christmas NPC {entry.Id} references missing map {entry.MapId}."));
        }

        foreach (var mission in questMissions)
        {
            if (mission.MapId is > 0 &&
                !mapIds.Contains(mission.MapId.Value) &&
                !questNavigationMapIds.Contains(mission.MapId.Value))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingQuestMap",
                    $"Quest mission {mission.MissionId} references missing map {mission.MapId.Value}."));
            }

            foreach (var prerequisite in mission.PrerequisiteMissionIds.Where(id => !questMissionIds.Contains(id)))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingQuestPrerequisite",
                    $"Quest mission {mission.MissionId} references missing prerequisite mission {prerequisite}."));
            }

            foreach (var prizeItem in mission.PrizeItems.Where(item => !itemIds.Contains(item.ItemTypeId)))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingQuestPrizeItem",
                    $"Quest mission {mission.MissionId} references missing prize item {prizeItem.ItemTypeId}."));
            }

            foreach (var anchor in mission.BeginNpcAnchors.Concat(mission.FinishNpcAnchors))
            {
                if (anchor.MapId is > 0 &&
                    !mapIds.Contains(anchor.MapId.Value) &&
                    !questNavigationMapIds.Contains(anchor.MapId.Value))
                {
                    diagnostics.Add(new WorldDataDiagnostic(
                        "MissingQuestAnchorMap",
                        $"Quest mission {mission.MissionId} {anchor.Key} references missing map {anchor.MapId.Value}."));
                }

                if (anchor.NpcId is > 0 &&
                    !questNpcIds.Contains(anchor.NpcId.Value) &&
                    !IsCompleteQuestNavigationAnchor(anchor))
                {
                    diagnostics.Add(new WorldDataDiagnostic(
                        "MissingQuestAnchorNpc",
                        $"Quest mission {mission.MissionId} {anchor.Key} references missing NPC {anchor.NpcId.Value}."));
                }
            }

            if (mission.IntentAmount is { } intentAmount && intentAmount != mission.Intentions.Count)
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "QuestIntentAmountMismatch",
                    $"Quest mission {mission.MissionId} declares {intentAmount} intentions but has {mission.Intentions.Count}."));
            }
        }

        foreach (var monster in monsters)
        {
            foreach (var specialDrop in (monster.SpecialDrops ?? []).Where(drop => !itemIds.Contains(drop.ItemTypeId)))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingMonsterSpecialDropItem",
                    $"Monster {monster.Id} references missing special drop item {specialDrop.ItemTypeId}."));
            }
        }

        foreach (var spawn in monsterSpawns)
        {
            if (!mapIds.Contains(spawn.MapId))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingSpawnMap",
                    $"Monster spawn generator {spawn.GeneratorId} references missing map {spawn.MapId}."));
            }

            if (!monsterIds.Contains(spawn.MonsterTypeId))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingSpawnMonster",
                    $"Monster spawn generator {spawn.GeneratorId} references missing monster type {spawn.MonsterTypeId}."));
            }
        }

        foreach (var npc in npcs.Where(npc => !mapIds.Contains(npc.MapId)))
        {
            diagnostics.Add(new WorldDataDiagnostic("MissingNpcMap", $"NPC {npc.Id} references missing map {npc.MapId}."));
        }

        foreach (var sobNpc in sobNpcs.Where(sobNpc => !mapIds.Contains(sobNpc.MapId)))
        {
            diagnostics.Add(new WorldDataDiagnostic(
                "MissingSobNpcMap",
                $"SobNpc {sobNpc.Id} ({sobNpc.Name}) references missing map {sobNpc.MapId} at ({sobNpc.X},{sobNpc.Y})."));
        }

        foreach (var portal in portals)
        {
            if (!mapIds.Contains(portal.SourceMapId))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingPortalSourceMap",
                    $"Portal from map {portal.SourceMapId} at ({portal.SourceX},{portal.SourceY}) references a missing source map."));
            }

            if (!mapIds.Contains(portal.TargetMapId))
            {
                diagnostics.Add(new WorldDataDiagnostic(
                    "MissingPortalTargetMap",
                    $"Portal from map {portal.SourceMapId} at ({portal.SourceX},{portal.SourceY}) references missing target map {portal.TargetMapId} at ({portal.TargetX},{portal.TargetY})."));
            }
        }

        return new WorldDataCatalog(
            manifest,
            clientConfigs,
            items,
            itemTypeSubs,
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
            teamArenaRecords,
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
            magicEffects,
            magicTypeOperations,
            titleTypes,
            combatGear,
            traps,
            npcs,
            sobNpcs,
            portals,
            maps,
            mapNames,
            legacyGameMaps,
            monsters,
            monsterSpawns,
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
            shops,
            shopItems,
            exchangeShopGoods,
            exchangeShopGoodsEx,
            exchangeShopLevels,
            honorShopItems,
            honorShopRecommendations,
            racePointShopItems,
            racePointShopRecommendations,
            goldenLeagueShopItems,
            goldenLeagueShopRecommendations,
            goldenLeagueShopConfig,
            booths,
            boothItems,
            diagnostics);
    }
}
