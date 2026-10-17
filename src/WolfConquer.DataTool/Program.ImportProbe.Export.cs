using WolfConquer.Core.Data;

internal static partial class Program
{
    static int Export(string dbServerPath, string outputPath, IReadOnlyList<string> includedGameMapExPaths)
    {
        Console.WriteLine($"DBServer: {dbServerPath}");
        Console.WriteLine($"Output: {outputPath}");

        Directory.CreateDirectory(outputPath);

        var clientConfigs = ReadClientConfigs(Path.Combine(dbServerPath, "client_config.ini")).ToArray();
        var items = ReadLines(Path.Combine(dbServerPath, "itemtype.txt"), line => ItemTypeParser.ParseLine(line, 66))
            .Select(item => new ItemSeed(item.Id, item.Name, item.Fields))
            .ToArray();
        var itemTypeSubs = ReadItemTypeSubs(Path.Combine(dbServerPath, "ItemtypeSub.txt")).ToArray();
        var itemAddBonuses = ReadItemAddBonuses(Path.Combine(dbServerPath, "ItemAdd.ini")).ToArray();
        var levelExperience = ReadLevelExperience(Path.Combine(dbServerPath, "levexp.txt")).ToArray();
        var classLevelStats = ReadClassLevelStats(Path.Combine(dbServerPath, "Stats.ini")).ToArray();
        var professionLevelUps = ReadProfessionLevelUps(Path.Combine(dbServerPath, "prof_lev_up.txt")).ToArray();
        var professionLevelBenefits = ReadProfessionLevelBenefits(Path.Combine(dbServerPath, "prof_lev_benefit.txt")).ToArray();
        var professionTitleBenefits = ReadProfessionTitleBenefits(Path.Combine(dbServerPath, "prof_title_benefit.txt")).ToArray();
        var questInfo = ReadQuestInfo(Path.Combine(dbServerPath, "Questinfo.ini"));
        var quizQuestions = QuizQuestionParser.ParseFile(Path.Combine(dbServerPath, "quizquestins.ini")).ToArray();
        var magic = ReadLines(Path.Combine(dbServerPath, "magictype.txt"), line => MagicTypeParser.ParseLine(line, 54))
            .Select(spell => new MagicSeed(spell.Id, spell.TypeId, spell.Name, spell.Fields))
            .ToArray();
        var magicEffects = MagicEffectParser.ParseFile(Path.Combine(dbServerPath, "MagicEffect.ini")).ToArray();
        var magicTypeOperations = MagicTypeOperationParser.ParseFile(Path.Combine(dbServerPath, "magictypeop.txt")).ToArray();
        var titleTypes = ReadTitleTypes(Path.Combine(dbServerPath, "title_type.txt")).ToArray();
        var combatGear = ReadCombatGear(Path.Combine(dbServerPath, "combat_gear.txt")).ToArray();
        var traps = ReadTraps(Path.Combine(dbServerPath, "Traps.txt")).ToArray();
        var npcs = ReadNpcs(Path.Combine(dbServerPath, "Npcs.txt"))
            .Select(npc => new NpcSeed(
                npc.Id,
                npc.Kind,
                npc.Lookface,
                npc.MapId,
                npc.X,
                npc.Y,
                npc.OptionalFlag,
                npc.SourceLine,
                npc.RawLine,
                npc.Fields))
            .ToArray();
        var sobNpcs = ReadSobNpcs(Path.Combine(dbServerPath, "SobNpcs.txt")).ToArray();
        var portals = PortalParser.Parse(IniDocument.Parse(File.ReadAllText(Path.Combine(dbServerPath, "portals.ini"))))
            .Select(portal => new PortalSeed(portal.SourceMapId, portal.SourceX, portal.SourceY, portal.TargetMapId, portal.TargetX, portal.TargetY))
            .ToArray();
        var maps = ReadMaps(Path.Combine(dbServerPath, "maps")).ToArray();
        var gameMapExPaths = new[] { Path.Combine(dbServerPath, "GameMapEx.ini") }
            .Concat(includedGameMapExPaths)
            .ToArray();
        var mapNames = MapNameParser.ParseFiles(gameMapExPaths).ToArray();
        var legacyGameMaps = LegacyGameMapParser.ParseGameMapDatFile(Path.Combine(dbServerPath, "ini", "GameMap.dat")).ToArray();
        var monsters = Directory.EnumerateFiles(Path.Combine(dbServerPath, "Monsters"), "*.ini")
            .Order(StringComparer.OrdinalIgnoreCase)
            .Select(path => MonsterParser.Parse(IniDocument.Parse(File.ReadAllText(path))))
            .Select(monster => new MonsterSeed(
                monster.Id,
                monster.Name,
                TryGetInt(monster.Attributes, "level"),
                monster.Attributes,
                monster.SpecialDrops.Select(drop => new SpecialDropSeed(drop.ItemTypeId, drop.ChanceOrCount)).ToArray()))
            .ToArray();
        var monsterTypeIdsByName = BuildMonsterTypeIdsByName(monsters);
        var monsterSpawns = MonsterSpawnImport.ReadMonsterSpawns(Path.Combine(dbServerPath, "MonsterSpawns"), monsterTypeIdsByName)
            .Select(spawn => new MonsterSpawnSeed(
                spawn.GeneratorId,
                spawn.MapId,
                spawn.MonsterTypeId,
                spawn.BoundX,
                spawn.BoundY,
                spawn.BoundWidth,
                spawn.BoundHeight,
                spawn.MaxNpc,
                spawn.RestSeconds,
                spawn.MaxPerGenerator,
                spawn.TimerBegin,
                spawn.TimerEnd,
                spawn.BornX,
                spawn.BornY,
                spawn.SourceMapFolderId,
                spawn.SourceMonsterName,
                spawn.SourceRelativePath,
                spawn.Attributes))
            .ToArray();
        var lotteryDictionary = ReadLotteryDictionary(Path.Combine(dbServerPath, "dict_lottery.txt")).ToArray();
        var collectionRewardTiers = ReadCollectionRewardTiers(Path.Combine(dbServerPath, "dict_collection.txt")).ToArray();
        var taskRewardTypes = ReadTaskRewardTypes(Path.Combine(dbServerPath, "task_reward_type.txt")).ToArray();
        var taskRewardTypeIniEntries = ReadTaskRewardTypeIniEntries(Path.Combine(dbServerPath, "task_reward_type.ini")).ToArray();
        var neiGong = ReadNeiGong(Path.Combine(dbServerPath, "NeiGongInfo.ini"));
        var subProfessions = ReadSubProfessions(Path.Combine(dbServerPath, "SubProfessionInfo.ini")).ToArray();
        var syndicateLevels = ReadSyndicateLevels(Path.Combine(dbServerPath, "syndicate_level.txt")).ToArray();
        var runeLevelExperience = ReadRuneLevelExperience(Path.Combine(dbServerPath, "rune_levexp.txt")).ToArray();
        var runeStorageAttributes = ReadRuneStorageAttributes(Path.Combine(dbServerPath, "rune_storage_attr.txt")).ToArray();
        var itemRefineEffects = ReadItemRefineEffects(Path.Combine(dbServerPath, "item_refine_effect.txt")).ToArray();
        var itemRefineEffectEx = ReadItemRefineEffectEx(Path.Combine(dbServerPath, "item_refine_effect_ex.txt")).ToArray();
        var itemRefineUpgrades = ReadItemRefineUpgrades(Path.Combine(dbServerPath, "item_refine_upgrade.txt")).ToArray();
        var transformInfo = ReadTransformInfo(Path.Combine(dbServerPath, "TransformInfo.txt")).ToArray();
        var furniture = ReadFurniture(Path.Combine(dbServerPath, "furnitures.txt")).ToArray();
        var hairColorTypes = ReadHairColorTypes(Path.Combine(dbServerPath, "hair_color_type.txt")).ToArray();
        var hairFaceStorageTypes = ReadHairFaceStorageTypes(Path.Combine(dbServerPath, "hairface_storage_type.txt")).ToArray();
        var coatStorageTypes = ReadCoatStorageTypes(Path.Combine(dbServerPath, "coat_storage_type.txt")).ToArray();
        var coatStorageAttributes = ReadCoatStorageAttributes(Path.Combine(dbServerPath, "coat_storage_attr.txt")).ToArray();
        var coatColorRules = ReadCoatColorRules(Path.Combine(dbServerPath, "coat_color_rule.txt")).ToArray();
        var xuanbaoAdditionAttributes = ReadXuanbaoAdditionAttributes(Path.Combine(dbServerPath, "xuanbao_addition_attr.txt")).ToArray();
        var xuanbaoComposeAttributeLimits = ReadXuanbaoComposeAttributeLimits(Path.Combine(dbServerPath, "xuanbao_compose_attr_limit.txt")).ToArray();
        var xuanbaoRandomAttributes = ReadXuanbaoRandomAttributes(Path.Combine(dbServerPath, "xuanbao_rand_attr.txt")).ToArray();
        var christmasNpcs = ReadChristmasNpcs(Path.Combine(dbServerPath, "christmasnpcs.txt")).ToArray();
        var tutorTypes = ReadTutorTypes(Path.Combine(dbServerPath, "cq_tutor_type.txt")).ToArray();
        var tutorBattleLimitTypes = ReadTutorBattleLimitTypes(Path.Combine(dbServerPath, "cq_tutor_battle_limit_type.txt")).ToArray();
        var beastAttributes = ReadBeastAttributes(Path.Combine(dbServerPath, "beasts_attr.txt")).ToArray();
        var fateExperience = ReadFateExperience(Path.Combine(dbServerPath, "fate_exp.txt")).ToArray();
        var spiritRates = ReadSpiritRates(Path.Combine(dbServerPath, "spirit_rate.txt")).ToArray();
        var hundredWeapons = ReadHundredWeapons(Path.Combine(dbServerPath, "hundred_weapon.txt")).ToArray();
        var gouyuTypes = ReadGouyuTypes(Path.Combine(dbServerPath, "gouyu_type.txt")).ToArray();
        var gouyuImmortals = ReadGouyuImmortals(Path.Combine(dbServerPath, "gouyu_immortal.txt")).ToArray();
        var specialEffectAttributes = ReadSpecialEffectAttributes(Path.Combine(dbServerPath, "speffect_attribute.txt")).ToArray();
        var daoqiDictTypes = ReadDaoqiDictTypes(Path.Combine(dbServerPath, "daoqi_dict_type.txt")).ToArray();
        var syndicateFormationTypes = ReadSyndicateFormationTypes(Path.Combine(dbServerPath, "syn_formtype.txt")).ToArray();
        var syndicateSkillTypes = ReadSyndicateSkillTypes(Path.Combine(dbServerPath, "syn_skill_type.txt")).ToArray();
        var disdainRules = ReadDisdainRules(Path.Combine(dbServerPath, "cq_disdain.txt")).ToArray();
        var jiangHuAttributes = ReadJiangHuAttributes(Path.Combine(dbServerPath, "JianghuAttributes.txt")).ToArray();
        var jiangHuCultivateStatuses = ReadJiangHuCultivateStatuses(Path.Combine(dbServerPath, "JingHuCultivateStatus.txt")).ToArray();
        var refineryBoxes = ReadRefineryBoxes(Path.Combine(dbServerPath, "databaserefineryboxes.txt")).ToArray();
        var refinerySouls = ReadRefinerySouls(dbServerPath).ToArray();
        var refineryMaterials = ReadRefineryMaterials(Path.Combine(dbServerPath, "Rifinery.txt")).ToArray();
        var stateCountFiles = ReadStateCountFiles(dbServerPath).ToArray();
        var binarySeedFiles = ReadBinarySeedFiles(dbServerPath).ToArray();
        var emptySeedFiles = ReadEmptySeedFiles(dbServerPath).ToArray();
        var backupSeedFiles = ReadBackupSeedFiles(dbServerPath).ToArray();
        var pokerTables = ReadPokerTables(Path.Combine(dbServerPath, "PokerTables.txt")).ToArray();
        var rouletteTables = ReadRouletteTables(Path.Combine(dbServerPath, "Roulettes.txt")).ToArray();
        var npcTimeEntries = ReadNpcTimeEntries(Path.Combine(dbServerPath, "NpcTime.txt")).ToArray();
        var activityTasks = ReadActivityTasks(Path.Combine(dbServerPath, "ActivityTask.ini"));
        var meltingTypeRules = ReadMeltingTypeRules(dbServerPath).ToArray();
        var lotteryPrizes = ReadLotteryPrizes(Path.Combine(dbServerPath, "lottery.ini")).ToArray();
        var arenaRecords = ReadArenaRecords(Path.Combine(dbServerPath, "Arena.ini")).ToArray();
        var teamArenaRecords = ReadTeamArenaRecords(Path.Combine(dbServerPath, "TeamArena.ini")).ToArray();
        var elitePkRecords = ReadElitePkRecords(Path.Combine(dbServerPath, "ElitePk.ini")).ToArray();
        var teamElitePkRecords = ReadTeamElitePkRecords(Path.Combine(dbServerPath, "TeamElitePK.ini")).ToArray();
        var skillTeamPkRecords = ReadSkillTeamPkRecords(Path.Combine(dbServerPath, "SkillTeamPK.ini")).ToArray();
        var classPkWarRecords = ReadClassPkWarRecords(Path.Combine(dbServerPath, "ClassPkWar.ini")).ToArray();
        var combatGearRanks = ReadCombatGearRanks(Path.Combine(dbServerPath, "CombatGearRank.txt")).ToArray();
        var prestigeRanks = ReadPrestigeRanks(Path.Combine(dbServerPath, "PrestigeRanking.txt")).ToArray();
        var innerPowerRanks = ReadInnerPowerRanks(Path.Combine(dbServerPath, "InnerPower.txt")).ToArray();
        var runeRankings = ReadRuneRankings(Path.Combine(dbServerPath, "RuneRanking.txt")).ToArray();
        var staticStatues = ReadStaticStatues(Path.Combine(dbServerPath, "StaticStatue.txt")).ToArray();
        var newPlayerRewards = ReadNewPlayerRewards(Path.Combine(dbServerPath, "NewPlayerRewords.txt")).ToArray();
        var votes = ReadVotes(Path.Combine(dbServerPath, "Votes.txt")).ToArray();
        var couplesPkRecords = ReadCouplesPkRecords(Path.Combine(dbServerPath, "CouplesPK.ini")).ToArray();
        var socialAssociations = ReadSocialAssociations(Path.Combine(dbServerPath, "Associate.txt")).ToArray();
        var guildWarInfo = ReadGuildWarInfo(Path.Combine(dbServerPath, "GuildWarInfo.ini"));
        var superGuildWarInfo = ReadSuperGuildWarInfo(Path.Combine(dbServerPath, "SuperGuildWarInfo.ini"));
        var stageGoal = ReadStageGoal(Path.Combine(dbServerPath, "StageGoal.txt"));
        var shopData = ReadShops(dbServerPath);
        var exchangeShopGoods = ReadExchangeRows(Path.Combine(dbServerPath, "exchange_shop_goods.txt")).ToArray();
        var exchangeShopGoodsEx = ReadExchangeRows(Path.Combine(dbServerPath, "exchange_shop_goods_ex.txt")).ToArray();
        var exchangeShopLevels = ReadExchangeRows(Path.Combine(dbServerPath, "exchange_shop_lev.txt")).ToArray();
        var honorShop = ReadHonorShop(dbServerPath);
        var racePointShop = ReadPointShop(dbServerPath, "RacePointShop.ini");
        var goldenLeagueShop = ReadPointShop(dbServerPath, "GoldenLeagueShop.ini");
        var booths = ReadBooths(dbServerPath);

        WriteJson(Path.Combine(outputPath, "clientConfigs.json"), clientConfigs);
        WriteJson(Path.Combine(outputPath, "items.json"), items);
        WriteJson(Path.Combine(outputPath, "itemTypeSubs.json"), itemTypeSubs);
        WriteJson(Path.Combine(outputPath, "itemAddBonuses.json"), itemAddBonuses);
        WriteJson(Path.Combine(outputPath, "levelExperience.json"), levelExperience);
        WriteJson(Path.Combine(outputPath, "classLevelStats.json"), classLevelStats);
        WriteJson(Path.Combine(outputPath, "professionLevelUps.json"), professionLevelUps);
        WriteJson(Path.Combine(outputPath, "professionLevelBenefits.json"), professionLevelBenefits);
        WriteJson(Path.Combine(outputPath, "professionTitleBenefits.json"), professionTitleBenefits);
        WriteJson(Path.Combine(outputPath, "questInfo.json"), questInfo.Metadata);
        WriteJson(Path.Combine(outputPath, "questTaskTypes.json"), questInfo.TaskTypes);
        WriteJson(Path.Combine(outputPath, "questMissions.json"), questInfo.Missions);
        WriteJson(Path.Combine(outputPath, "quizQuestions.json"), quizQuestions);
        WriteJson(Path.Combine(outputPath, "magic.json"), magic);
        WriteJson(Path.Combine(outputPath, "magicEffects.json"), magicEffects);
        WriteJson(Path.Combine(outputPath, "magicTypeOperations.json"), magicTypeOperations);
        WriteJson(Path.Combine(outputPath, "titleTypes.json"), titleTypes);
        WriteJson(Path.Combine(outputPath, "combatGear.json"), combatGear);
        WriteJson(Path.Combine(outputPath, "traps.json"), traps);
        WriteJson(Path.Combine(outputPath, "npcs.json"), npcs);
        WriteJson(Path.Combine(outputPath, "sobNpcs.json"), sobNpcs);
        WriteJson(Path.Combine(outputPath, "portals.json"), portals);
        WriteJson(Path.Combine(outputPath, "maps.json"), maps);
        WriteJson(Path.Combine(outputPath, "mapNames.json"), mapNames);
        WriteJson(Path.Combine(outputPath, "legacyGameMaps.json"), legacyGameMaps);
        WriteJson(Path.Combine(outputPath, "monsters.json"), monsters);
        WriteJson(Path.Combine(outputPath, "monsterSpawns.json"), monsterSpawns);
        WriteJson(Path.Combine(outputPath, "lotteryDictionary.json"), lotteryDictionary);
        WriteJson(Path.Combine(outputPath, "collectionRewardTiers.json"), collectionRewardTiers);
        WriteJson(Path.Combine(outputPath, "taskRewardTypes.json"), taskRewardTypes);
        WriteJson(Path.Combine(outputPath, "taskRewardTypeIniEntries.json"), taskRewardTypeIniEntries);
        WriteJson(Path.Combine(outputPath, "neiGongHeader.json"), neiGong.Header);
        WriteJson(Path.Combine(outputPath, "neiGongSecrets.json"), neiGong.Secrets);
        WriteJson(Path.Combine(outputPath, "neiGongTypes.json"), neiGong.Types);
        WriteJson(Path.Combine(outputPath, "subProfessions.json"), subProfessions);
        WriteJson(Path.Combine(outputPath, "syndicateLevels.json"), syndicateLevels);
        WriteJson(Path.Combine(outputPath, "runeLevelExperience.json"), runeLevelExperience);
        WriteJson(Path.Combine(outputPath, "runeStorageAttributes.json"), runeStorageAttributes);
        WriteJson(Path.Combine(outputPath, "itemRefineEffects.json"), itemRefineEffects);
        WriteJson(Path.Combine(outputPath, "itemRefineEffectEx.json"), itemRefineEffectEx);
        WriteJson(Path.Combine(outputPath, "itemRefineUpgrades.json"), itemRefineUpgrades);
        WriteJson(Path.Combine(outputPath, "transformInfo.json"), transformInfo);
        WriteJson(Path.Combine(outputPath, "furniture.json"), furniture);
        WriteJson(Path.Combine(outputPath, "hairColorTypes.json"), hairColorTypes);
        WriteJson(Path.Combine(outputPath, "hairFaceStorageTypes.json"), hairFaceStorageTypes);
        WriteJson(Path.Combine(outputPath, "coatStorageTypes.json"), coatStorageTypes);
        WriteJson(Path.Combine(outputPath, "coatStorageAttributes.json"), coatStorageAttributes);
        WriteJson(Path.Combine(outputPath, "coatColorRules.json"), coatColorRules);
        WriteJson(Path.Combine(outputPath, "xuanbaoAdditionAttributes.json"), xuanbaoAdditionAttributes);
        WriteJson(Path.Combine(outputPath, "xuanbaoComposeAttributeLimits.json"), xuanbaoComposeAttributeLimits);
        WriteJson(Path.Combine(outputPath, "xuanbaoRandomAttributes.json"), xuanbaoRandomAttributes);
        WriteJson(Path.Combine(outputPath, "christmasNpcs.json"), christmasNpcs);
        WriteJson(Path.Combine(outputPath, "tutorTypes.json"), tutorTypes);
        WriteJson(Path.Combine(outputPath, "tutorBattleLimitTypes.json"), tutorBattleLimitTypes);
        WriteJson(Path.Combine(outputPath, "beastAttributes.json"), beastAttributes);
        WriteJson(Path.Combine(outputPath, "fateExperience.json"), fateExperience);
        WriteJson(Path.Combine(outputPath, "spiritRates.json"), spiritRates);
        WriteJson(Path.Combine(outputPath, "hundredWeapons.json"), hundredWeapons);
        WriteJson(Path.Combine(outputPath, "gouyuTypes.json"), gouyuTypes);
        WriteJson(Path.Combine(outputPath, "gouyuImmortals.json"), gouyuImmortals);
        WriteJson(Path.Combine(outputPath, "specialEffectAttributes.json"), specialEffectAttributes);
        WriteJson(Path.Combine(outputPath, "daoqiDictTypes.json"), daoqiDictTypes);
        WriteJson(Path.Combine(outputPath, "syndicateFormationTypes.json"), syndicateFormationTypes);
        WriteJson(Path.Combine(outputPath, "syndicateSkillTypes.json"), syndicateSkillTypes);
        WriteJson(Path.Combine(outputPath, "disdainRules.json"), disdainRules);
        WriteJson(Path.Combine(outputPath, "jiangHuAttributes.json"), jiangHuAttributes);
        WriteJson(Path.Combine(outputPath, "jiangHuCultivateStatuses.json"), jiangHuCultivateStatuses);
        WriteJson(Path.Combine(outputPath, "refineryBoxes.json"), refineryBoxes);
        WriteJson(Path.Combine(outputPath, "refinerySouls.json"), refinerySouls);
        WriteJson(Path.Combine(outputPath, "refineryMaterials.json"), refineryMaterials);
        WriteJson(Path.Combine(outputPath, "stateCountFiles.json"), stateCountFiles);
        WriteJson(Path.Combine(outputPath, "binarySeedFiles.json"), binarySeedFiles);
        WriteJson(Path.Combine(outputPath, "emptySeedFiles.json"), emptySeedFiles);
        WriteJson(Path.Combine(outputPath, "backupSeedFiles.json"), backupSeedFiles);
        WriteJson(Path.Combine(outputPath, "pokerTables.json"), pokerTables);
        WriteJson(Path.Combine(outputPath, "rouletteTables.json"), rouletteTables);
        WriteJson(Path.Combine(outputPath, "npcTimeEntries.json"), npcTimeEntries);
        WriteJson(Path.Combine(outputPath, "activityTasks.json"), activityTasks);
        WriteJson(Path.Combine(outputPath, "meltingTypeRules.json"), meltingTypeRules);
        WriteJson(Path.Combine(outputPath, "lotteryPrizes.json"), lotteryPrizes);
        WriteJson(Path.Combine(outputPath, "arenaRecords.json"), arenaRecords);
        WriteJson(Path.Combine(outputPath, "teamArenaRecords.json"), teamArenaRecords);
        WriteJson(Path.Combine(outputPath, "elitePkRecords.json"), elitePkRecords);
        WriteJson(Path.Combine(outputPath, "teamElitePkRecords.json"), teamElitePkRecords);
        WriteJson(Path.Combine(outputPath, "skillTeamPkRecords.json"), skillTeamPkRecords);
        WriteJson(Path.Combine(outputPath, "classPkWarRecords.json"), classPkWarRecords);
        WriteJson(Path.Combine(outputPath, "combatGearRanks.json"), combatGearRanks);
        WriteJson(Path.Combine(outputPath, "prestigeRanks.json"), prestigeRanks);
        WriteJson(Path.Combine(outputPath, "innerPowerRanks.json"), innerPowerRanks);
        WriteJson(Path.Combine(outputPath, "runeRankings.json"), runeRankings);
        WriteJson(Path.Combine(outputPath, "staticStatues.json"), staticStatues);
        WriteJson(Path.Combine(outputPath, "newPlayerRewards.json"), newPlayerRewards);
        WriteJson(Path.Combine(outputPath, "votes.json"), votes);
        WriteJson(Path.Combine(outputPath, "couplesPkRecords.json"), couplesPkRecords);
        WriteJson(Path.Combine(outputPath, "socialAssociations.json"), socialAssociations);
        WriteJson(Path.Combine(outputPath, "guildWarInfo.json"), guildWarInfo);
        WriteJson(Path.Combine(outputPath, "superGuildWarInfo.json"), superGuildWarInfo);
        WriteJson(Path.Combine(outputPath, "stageGoalInfo.json"), stageGoal.Info);
        WriteJson(Path.Combine(outputPath, "stageGoalStages.json"), stageGoal.Stages);
        WriteJson(Path.Combine(outputPath, "stageGoalTasks.json"), stageGoal.Tasks);
        WriteJson(Path.Combine(outputPath, "shops.json"), shopData.Shops);
        WriteJson(Path.Combine(outputPath, "shopItems.json"), shopData.Items);
        WriteJson(Path.Combine(outputPath, "exchangeShopGoods.json"), exchangeShopGoods);
        WriteJson(Path.Combine(outputPath, "exchangeShopGoodsEx.json"), exchangeShopGoodsEx);
        WriteJson(Path.Combine(outputPath, "exchangeShopLevels.json"), exchangeShopLevels);
        WriteJson(Path.Combine(outputPath, "honorShopItems.json"), honorShop.Items);
        WriteJson(Path.Combine(outputPath, "honorShopRecommendations.json"), honorShop.Recommendations);
        WriteJson(Path.Combine(outputPath, "racePointShopItems.json"), racePointShop.Items);
        WriteJson(Path.Combine(outputPath, "racePointShopRecommendations.json"), racePointShop.Recommendations);
        WriteJson(Path.Combine(outputPath, "goldenLeagueShopItems.json"), goldenLeagueShop.Items);
        WriteJson(Path.Combine(outputPath, "goldenLeagueShopRecommendations.json"), goldenLeagueShop.Recommendations);
        WriteJson(Path.Combine(outputPath, "goldenLeagueShopConfig.json"), goldenLeagueShop.Config);
        WriteJson(Path.Combine(outputPath, "booths.json"), booths.Booths);
        WriteJson(Path.Combine(outputPath, "boothItems.json"), booths.Items);
        WriteJson(Path.Combine(outputPath, "manifest.json"), new SeedManifest(DateTimeOffset.UtcNow, dbServerPath, new Dictionary<string, int>
        {
            ["clientConfigs"] = clientConfigs.Length,
            ["items"] = items.Length,
            ["itemTypeSubs"] = itemTypeSubs.Length,
            ["itemAddBonuses"] = itemAddBonuses.Length,
            ["levelExperience"] = levelExperience.Length,
            ["classLevelStats"] = classLevelStats.Length,
            ["professionLevelUps"] = professionLevelUps.Length,
            ["professionLevelBenefits"] = professionLevelBenefits.Length,
            ["professionTitleBenefits"] = professionTitleBenefits.Length,
            ["questInfo"] = 1,
            ["questTaskTypes"] = questInfo.TaskTypes.Count,
            ["questMissions"] = questInfo.Missions.Count,
            ["quizQuestions"] = quizQuestions.Length,
            ["magic"] = magic.Length,
            ["magicEffects"] = magicEffects.Length,
            ["magicTypeOperations"] = magicTypeOperations.Length,
            ["titleTypes"] = titleTypes.Length,
            ["combatGear"] = combatGear.Length,
            ["traps"] = traps.Length,
            ["npcs"] = npcs.Length,
            ["sobNpcs"] = sobNpcs.Length,
            ["portals"] = portals.Length,
            ["maps"] = maps.Length,
            ["mapNames"] = mapNames.Length,
            ["legacyGameMaps"] = legacyGameMaps.Length,
            ["monsters"] = monsters.Length,
            ["monsterSpawns"] = monsterSpawns.Length,
            ["lotteryDictionary"] = lotteryDictionary.Length,
            ["collectionRewardTiers"] = collectionRewardTiers.Length,
            ["taskRewardTypes"] = taskRewardTypes.Length,
            ["taskRewardTypeIniEntries"] = taskRewardTypeIniEntries.Length,
            ["neiGongHeader"] = 1,
            ["neiGongSecrets"] = neiGong.Secrets.Count,
            ["neiGongTypes"] = neiGong.Types.Count,
            ["subProfessions"] = subProfessions.Length,
            ["syndicateLevels"] = syndicateLevels.Length,
            ["runeLevelExperience"] = runeLevelExperience.Length,
            ["runeStorageAttributes"] = runeStorageAttributes.Length,
            ["itemRefineEffects"] = itemRefineEffects.Length,
            ["itemRefineEffectEx"] = itemRefineEffectEx.Length,
            ["itemRefineUpgrades"] = itemRefineUpgrades.Length,
            ["transformInfo"] = transformInfo.Length,
            ["furniture"] = furniture.Length,
            ["hairColorTypes"] = hairColorTypes.Length,
            ["hairFaceStorageTypes"] = hairFaceStorageTypes.Length,
            ["coatStorageTypes"] = coatStorageTypes.Length,
            ["coatStorageAttributes"] = coatStorageAttributes.Length,
            ["coatColorRules"] = coatColorRules.Length,
            ["xuanbaoAdditionAttributes"] = xuanbaoAdditionAttributes.Length,
            ["xuanbaoComposeAttributeLimits"] = xuanbaoComposeAttributeLimits.Length,
            ["xuanbaoRandomAttributes"] = xuanbaoRandomAttributes.Length,
            ["christmasNpcs"] = christmasNpcs.Length,
            ["tutorTypes"] = tutorTypes.Length,
            ["tutorBattleLimitTypes"] = tutorBattleLimitTypes.Length,
            ["beastAttributes"] = beastAttributes.Length,
            ["fateExperience"] = fateExperience.Length,
            ["spiritRates"] = spiritRates.Length,
            ["hundredWeapons"] = hundredWeapons.Length,
            ["gouyuTypes"] = gouyuTypes.Length,
            ["gouyuImmortals"] = gouyuImmortals.Length,
            ["specialEffectAttributes"] = specialEffectAttributes.Length,
            ["daoqiDictTypes"] = daoqiDictTypes.Length,
            ["syndicateFormationTypes"] = syndicateFormationTypes.Length,
            ["syndicateSkillTypes"] = syndicateSkillTypes.Length,
            ["disdainRules"] = disdainRules.Length,
            ["jiangHuAttributes"] = jiangHuAttributes.Length,
            ["jiangHuCultivateStatuses"] = jiangHuCultivateStatuses.Length,
            ["refineryBoxes"] = refineryBoxes.Length,
            ["refinerySouls"] = refinerySouls.Length,
            ["refineryMaterials"] = refineryMaterials.Length,
            ["stateCountFiles"] = stateCountFiles.Length,
            ["binarySeedFiles"] = binarySeedFiles.Length,
            ["emptySeedFiles"] = emptySeedFiles.Length,
            ["backupSeedFiles"] = backupSeedFiles.Length,
            ["pokerTables"] = pokerTables.Length,
            ["rouletteTables"] = rouletteTables.Length,
            ["npcTimeEntries"] = npcTimeEntries.Length,
            ["activityTasks"] = activityTasks.Tasks.Count,
            ["meltingTypeRules"] = meltingTypeRules.Length,
            ["lotteryPrizes"] = lotteryPrizes.Length,
            ["arenaRecords"] = arenaRecords.Length,
            ["teamArenaRecords"] = teamArenaRecords.Length,
            ["elitePkRecords"] = elitePkRecords.Length,
            ["teamElitePkRecords"] = teamElitePkRecords.Length,
            ["skillTeamPkRecords"] = skillTeamPkRecords.Length,
            ["classPkWarRecords"] = classPkWarRecords.Length,
            ["combatGearRanks"] = combatGearRanks.Length,
            ["prestigeRanks"] = prestigeRanks.Length,
            ["innerPowerRanks"] = innerPowerRanks.Length,
            ["runeRankings"] = runeRankings.Length,
            ["staticStatues"] = staticStatues.Length,
            ["newPlayerRewards"] = newPlayerRewards.Length,
            ["votes"] = votes.Length,
            ["couplesPkRecords"] = couplesPkRecords.Length,
            ["socialAssociations"] = socialAssociations.Length,
            ["guildWarInfo"] = 1,
            ["superGuildWarInfo"] = 1,
            ["stageGoalInfo"] = 1,
            ["stageGoalStages"] = stageGoal.Stages.Count,
            ["stageGoalTasks"] = stageGoal.Tasks.Count,
            ["shops"] = shopData.Shops.Count,
            ["shopItems"] = shopData.Items.Count,
            ["exchangeShopGoods"] = exchangeShopGoods.Length,
            ["exchangeShopGoodsEx"] = exchangeShopGoodsEx.Length,
            ["exchangeShopLevels"] = exchangeShopLevels.Length,
            ["honorShopItems"] = honorShop.Items.Count,
            ["honorShopRecommendations"] = honorShop.Recommendations.Count,
            ["racePointShopItems"] = racePointShop.Items.Count,
            ["racePointShopRecommendations"] = racePointShop.Recommendations.Count,
            ["goldenLeagueShopItems"] = goldenLeagueShop.Items.Count,
            ["goldenLeagueShopRecommendations"] = goldenLeagueShop.Recommendations.Count,
            ["goldenLeagueShopConfig"] = goldenLeagueShop.Config.Count,
            ["booths"] = booths.Booths.Count,
            ["boothItems"] = booths.Items.Count
        }));

        Console.WriteLine($"clientConfigs: {clientConfigs.Length:N0}");
        Console.WriteLine($"items: {items.Length:N0}");
        Console.WriteLine($"itemTypeSubs: {itemTypeSubs.Length:N0}");
        Console.WriteLine($"itemAddBonuses: {itemAddBonuses.Length:N0}");
        Console.WriteLine($"levelExperience: {levelExperience.Length:N0}");
        Console.WriteLine($"classLevelStats: {classLevelStats.Length:N0}");
        Console.WriteLine($"professionLevelUps: {professionLevelUps.Length:N0}");
        Console.WriteLine($"professionLevelBenefits: {professionLevelBenefits.Length:N0}");
        Console.WriteLine($"professionTitleBenefits: {professionTitleBenefits.Length:N0}");
        Console.WriteLine($"questTaskTypes: {questInfo.TaskTypes.Count:N0}");
        Console.WriteLine($"questMissions: {questInfo.Missions.Count:N0}");
        Console.WriteLine($"quizQuestions: {quizQuestions.Length:N0}");
        Console.WriteLine($"magic: {magic.Length:N0}");
        Console.WriteLine($"magicEffects: {magicEffects.Length:N0}");
        Console.WriteLine($"magicTypeOperations: {magicTypeOperations.Length:N0}");
        Console.WriteLine($"titleTypes: {titleTypes.Length:N0}");
        Console.WriteLine($"combatGear: {combatGear.Length:N0}");
        Console.WriteLine($"traps: {traps.Length:N0}");
        Console.WriteLine($"npcs: {npcs.Length:N0}");
        Console.WriteLine($"sobNpcs: {sobNpcs.Length:N0}");
        Console.WriteLine($"portals: {portals.Length:N0}");
        Console.WriteLine($"maps: {maps.Length:N0}");
        Console.WriteLine($"mapNames: {mapNames.Length:N0}");
        Console.WriteLine($"legacyGameMaps: {legacyGameMaps.Length:N0}");
        Console.WriteLine($"monsters: {monsters.Length:N0}");
        Console.WriteLine($"monsterSpawns: {monsterSpawns.Length:N0}");
        Console.WriteLine($"lotteryDictionary: {lotteryDictionary.Length:N0}");
        Console.WriteLine($"collectionRewardTiers: {collectionRewardTiers.Length:N0}");
        Console.WriteLine($"taskRewardTypes: {taskRewardTypes.Length:N0}");
        Console.WriteLine($"taskRewardTypeIniEntries: {taskRewardTypeIniEntries.Length:N0}");
        Console.WriteLine($"neiGongSecrets: {neiGong.Secrets.Count:N0}");
        Console.WriteLine($"neiGongTypes: {neiGong.Types.Count:N0}");
        Console.WriteLine($"subProfessions: {subProfessions.Length:N0}");
        Console.WriteLine($"syndicateLevels: {syndicateLevels.Length:N0}");
        Console.WriteLine($"runeLevelExperience: {runeLevelExperience.Length:N0}");
        Console.WriteLine($"runeStorageAttributes: {runeStorageAttributes.Length:N0}");
        Console.WriteLine($"itemRefineEffects: {itemRefineEffects.Length:N0}");
        Console.WriteLine($"itemRefineEffectEx: {itemRefineEffectEx.Length:N0}");
        Console.WriteLine($"itemRefineUpgrades: {itemRefineUpgrades.Length:N0}");
        Console.WriteLine($"transformInfo: {transformInfo.Length:N0}");
        Console.WriteLine($"furniture: {furniture.Length:N0}");
        Console.WriteLine($"hairColorTypes: {hairColorTypes.Length:N0}");
        Console.WriteLine($"hairFaceStorageTypes: {hairFaceStorageTypes.Length:N0}");
        Console.WriteLine($"coatStorageTypes: {coatStorageTypes.Length:N0}");
        Console.WriteLine($"coatStorageAttributes: {coatStorageAttributes.Length:N0}");
        Console.WriteLine($"coatColorRules: {coatColorRules.Length:N0}");
        Console.WriteLine($"xuanbaoAdditionAttributes: {xuanbaoAdditionAttributes.Length:N0}");
        Console.WriteLine($"xuanbaoComposeAttributeLimits: {xuanbaoComposeAttributeLimits.Length:N0}");
        Console.WriteLine($"xuanbaoRandomAttributes: {xuanbaoRandomAttributes.Length:N0}");
        Console.WriteLine($"christmasNpcs: {christmasNpcs.Length:N0}");
        Console.WriteLine($"tutorTypes: {tutorTypes.Length:N0}");
        Console.WriteLine($"tutorBattleLimitTypes: {tutorBattleLimitTypes.Length:N0}");
        Console.WriteLine($"beastAttributes: {beastAttributes.Length:N0}");
        Console.WriteLine($"fateExperience: {fateExperience.Length:N0}");
        Console.WriteLine($"spiritRates: {spiritRates.Length:N0}");
        Console.WriteLine($"hundredWeapons: {hundredWeapons.Length:N0}");
        Console.WriteLine($"gouyuTypes: {gouyuTypes.Length:N0}");
        Console.WriteLine($"gouyuImmortals: {gouyuImmortals.Length:N0}");
        Console.WriteLine($"specialEffectAttributes: {specialEffectAttributes.Length:N0}");
        Console.WriteLine($"daoqiDictTypes: {daoqiDictTypes.Length:N0}");
        Console.WriteLine($"syndicateFormationTypes: {syndicateFormationTypes.Length:N0}");
        Console.WriteLine($"syndicateSkillTypes: {syndicateSkillTypes.Length:N0}");
        Console.WriteLine($"disdainRules: {disdainRules.Length:N0}");
        Console.WriteLine($"jiangHuAttributes: {jiangHuAttributes.Length:N0}");
        Console.WriteLine($"jiangHuCultivateStatuses: {jiangHuCultivateStatuses.Length:N0}");
        Console.WriteLine($"refineryBoxes: {refineryBoxes.Length:N0}");
        Console.WriteLine($"refinerySouls: {refinerySouls.Length:N0}");
        Console.WriteLine($"refineryMaterials: {refineryMaterials.Length:N0}");
        Console.WriteLine($"stateCountFiles: {stateCountFiles.Length:N0}");
        Console.WriteLine($"stateCountEntries: {stateCountFiles.Sum(file => file.Entries.Count):N0}");
        Console.WriteLine($"binarySeedFiles: {binarySeedFiles.Length:N0}");
        Console.WriteLine($"binarySeedBytes: {binarySeedFiles.Sum(file => file.Length):N0}");
        Console.WriteLine($"emptySeedFiles: {emptySeedFiles.Length:N0}");
        Console.WriteLine($"emptySeedBytes: {emptySeedFiles.Sum(file => file.Length):N0}");
        Console.WriteLine($"backupSeedFiles: {backupSeedFiles.Length:N0}");
        Console.WriteLine($"backupSeedBytes: {backupSeedFiles.Sum(file => file.Length):N0}");
        Console.WriteLine($"pokerTables: {pokerTables.Length:N0}");
        Console.WriteLine($"rouletteTables: {rouletteTables.Length:N0}");
        Console.WriteLine($"npcTimeEntries: {npcTimeEntries.Length:N0}");
        Console.WriteLine($"activityTasks: {activityTasks.Tasks.Count:N0}");
        Console.WriteLine($"meltingTypeRules: {meltingTypeRules.Length:N0}");
        Console.WriteLine($"lotteryPrizes: {lotteryPrizes.Length:N0}");
        Console.WriteLine($"arenaRecords: {arenaRecords.Length:N0}");
        Console.WriteLine($"teamArenaRecords: {teamArenaRecords.Length:N0}");
        Console.WriteLine($"elitePkRecords: {elitePkRecords.Length:N0}");
        Console.WriteLine($"teamElitePkRecords: {teamElitePkRecords.Length:N0}");
        Console.WriteLine($"skillTeamPkRecords: {skillTeamPkRecords.Length:N0}");
        Console.WriteLine($"classPkWarRecords: {classPkWarRecords.Length:N0}");
        Console.WriteLine($"combatGearRanks: {combatGearRanks.Length:N0}");
        Console.WriteLine($"prestigeRanks: {prestigeRanks.Length:N0}");
        Console.WriteLine($"innerPowerRanks: {innerPowerRanks.Length:N0}");
        Console.WriteLine($"runeRankings: {runeRankings.Length:N0}");
        Console.WriteLine($"staticStatues: {staticStatues.Length:N0}");
        Console.WriteLine($"newPlayerRewards: {newPlayerRewards.Length:N0}");
        Console.WriteLine($"votes: {votes.Length:N0}");
        Console.WriteLine($"couplesPkRecords: {couplesPkRecords.Length:N0}");
        Console.WriteLine($"socialAssociations: {socialAssociations.Length:N0}");
        Console.WriteLine($"guildWarInfoEntries: {guildWarInfo.Entries.Count:N0}");
        Console.WriteLine($"superGuildWarInfoEntries: {superGuildWarInfo.Entries.Count:N0}");
        Console.WriteLine($"stageGoalStages: {stageGoal.Stages.Count:N0}");
        Console.WriteLine($"stageGoalTasks: {stageGoal.Tasks.Count:N0}");
        Console.WriteLine($"shops: {shopData.Shops.Count:N0}");
        Console.WriteLine($"shopItems: {shopData.Items.Count:N0}");
        Console.WriteLine($"exchangeShopGoods: {exchangeShopGoods.Length:N0}");
        Console.WriteLine($"exchangeShopGoodsEx: {exchangeShopGoodsEx.Length:N0}");
        Console.WriteLine($"exchangeShopLevels: {exchangeShopLevels.Length:N0}");
        Console.WriteLine($"honorShopItems: {honorShop.Items.Count:N0}");
        Console.WriteLine($"honorShopRecommendations: {honorShop.Recommendations.Count:N0}");
        Console.WriteLine($"racePointShopItems: {racePointShop.Items.Count:N0}");
        Console.WriteLine($"racePointShopRecommendations: {racePointShop.Recommendations.Count:N0}");
        Console.WriteLine($"goldenLeagueShopItems: {goldenLeagueShop.Items.Count:N0}");
        Console.WriteLine($"goldenLeagueShopRecommendations: {goldenLeagueShop.Recommendations.Count:N0}");
        Console.WriteLine($"goldenLeagueShopConfig: {goldenLeagueShop.Config.Count:N0}");
        Console.WriteLine($"booths: {booths.Booths.Count:N0}");
        Console.WriteLine($"boothItems: {booths.Items.Count:N0}");
        Console.WriteLine("Export completed.");
        return 0;
    }
}
