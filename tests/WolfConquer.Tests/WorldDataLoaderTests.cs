using System.Globalization;
using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class WorldDataLoaderTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _rootPath = Path.Combine(Path.GetTempPath(), "wolf-world-data-" + Guid.NewGuid());

    [Fact]
    public async Task LoaderReadsAllSeedFilesAndBuildsIndexes()
    {
        WriteFixture();
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Equal(2, catalog.ClientConfigs.Count);
        Assert.Equal("TestSrv", catalog.ClientConfigsByServerId[2].ServerName);
        Assert.Equal("Realm1", catalog.ClientConfigsByServerName["realm1"].Single().ServerName);
        Assert.Equal(9220, catalog.ClientConfigsByHost["127.0.0.1"].Single().Port);
        Assert.Equal(101, catalog.ClientConfigsByPort[9221].Single().ServerId);
        Assert.Equal(2, catalog.Items.Count);
        Assert.Equal(2, catalog.ItemTypeSubs.Count);
        Assert.Equal(2, catalog.ItemTypeSubsByItemTypeId[205003].Count);
        Assert.Equal("DuplicateSubA", catalog.ItemTypeSubsByItemTypeId[205003][0].Fields[2]);
        Assert.Equal(2, catalog.ItemAddBonuses.Count);
        Assert.Equal(3, catalog.LevelExperience.Count);
        Assert.Equal(2, catalog.ClassLevelStats.Count);
        Assert.Single(catalog.ProfessionLevelUps);
        Assert.Single(catalog.ProfessionLevelBenefits);
        Assert.Single(catalog.ProfessionTitleBenefits);
        Assert.Equal(2, catalog.QuestMissions.Count);
        Assert.Equal("TwinCity", catalog.MapsById[1000].Name);
        Assert.Equal(3, catalog.MapNames.Count);
        Assert.Equal("Desert", catalog.MapNamesByMapId[1000].Name);
        Assert.Equal("VIP Resort", catalog.MapNamesByMapId[3845].Name);
        Assert.Equal(1000, catalog.MapNamesByName["Desert"].Single().MapId);
        Assert.Equal(1000, catalog.MapNamesByName["Twin City Alias"].Single().MapId);
        Assert.Equal("Pheasant", catalog.MonstersById[10].Name);
        Assert.Single(catalog.MagicByTypeId[100]);
        Assert.Equal(2, catalog.MagicEffects.Count);
        Assert.Equal(2, catalog.MagicEffectsById[5_002_142_000].Count);
        Assert.Equal("Long~id", catalog.MagicEffectByIdAndOccurrence[(5_002_142_000, 1)].DescriptionEx);
        Assert.Equal(2, catalog.MagicTypeOperations.Count);
        Assert.Equal([100, 1360, 9999], catalog.MagicTypeOperationsById[1].ReferencedMagicTypeIds);
        Assert.Equal(2, catalog.MagicTypeOperationsByMode[1].Count);
        Assert.Single(catalog.MagicTypeOperationsByReferencedMagicTypeId[100]);
        Assert.Equal(2, catalog.MagicTypeOperationsByReferencedMagicTypeId[1360].Count);
        Assert.Equal(2, catalog.TitleTypes.Count);
        Assert.Equal(500, catalog.TitleTypesById[9].Score);
        Assert.Single(catalog.TitleTypesByTypeId[8043]);
        Assert.Equal("CS Team PK Champion Halo", catalog.TitleTypesByName["CS Team PK Champion Halo"].Single().Name);
        Assert.Equal(2, catalog.CombatGear.Count);
        Assert.Equal(300, catalog.CombatGearById[1].UpExperience);
        Assert.Equal(2, catalog.CombatGearByGearTypeId[201].Count);
        Assert.Equal(480, catalog.CombatGearByGearTypeAndLevel[(201, 2)].Score);
        Assert.Equal(2, catalog.Traps.Count);
        Assert.Equal(2, catalog.TrapsByMapId[44463].Count);
        Assert.Equal(1618, catalog.TrapsByTrapTypeId[1618].Single().TrapTypeId);
        Assert.Equal(2, catalog.TrapsByMapPosition[(44463, 77, 190)].Count);
        Assert.Null(catalog.Traps.Single(trap => trap.TrapTypeId == 24).OptionalFlag);
        Assert.Single(catalog.PortalsBySourceMapId[1000]);
        Assert.Equal(2, catalog.MonsterSpawnsByGeneratorId[1].Count);
        Assert.Equal(2, catalog.LotteryDictionaryByType[2].Count);
        Assert.Equal(18007, catalog.LotteryDictionaryById[17].Value);
        Assert.Equal(3, catalog.CollectionRewardTiersByCollectionId[700].Count);
        Assert.Equal([12, 11, 11], catalog.CollectionRewardTiersByCollectionId[700].Select(tier => tier.Id).ToArray());
        Assert.Equal(1500, catalog.CollectionRewardTiersById[11].RewardValue);
        Assert.Equal(600, catalog.CollectionRewardTierByCollectionIdAndMinValue[(700, 8400)].RewardAmount);
        Assert.Equal(755, catalog.TaskRewardTypesById[755].Id);
        Assert.Equal(3651, catalog.TaskRewardTypeIniEntriesById[3651].Id);
        Assert.Equal("0002", catalog.TaskRewardTypeIniEntriesById[3651].Fields[3]);
        Assert.Equal(1, catalog.NeiGongHeader.SecretCount);
        Assert.Equal("Universal~Concept", catalog.NeiGongSecretsById[1].Name);
        Assert.Equal(1, catalog.NeiGongTypesByTypeId[1].TypeId);
        Assert.Equal("Universal~Concept~(A)", catalog.NeiGongTypeBySecretIdAndSlot[(1, 1)].Name);
        Assert.Equal("Martial Artist", catalog.SubProfessionsById[11].Title);
        Assert.Equal([41, 31], catalog.SubProfessionsById[11].RequiredProfessions);
        Assert.Single(catalog.SubProfessionsByTitle["Martial Artist"]);
        Assert.Equal(2, catalog.SubProfessionsById[11].Entries.Count(entry => entry.Key == "req_joblv"));
        Assert.Single(catalog.SyndicateLevels);
        Assert.Equal([999, 2, 8, 10, 3, 3, 3, 1, 3, 5, 5], catalog.SyndicateLevelsByLevel[1].Parameters);
        Assert.Single(catalog.RuneLevelExperience);
        Assert.Equal(341_500, catalog.RuneLevelExperienceByRuneTypeAndLevel[(402, 27)].Experience);
        Assert.Equal(2, catalog.RuneStorageAttributesByRequiredStorageScore[335_000].Count);
        Assert.Equal(new RuneStorageAttributeBonus(11, 300), catalog.RuneStorageAttributesById[21].Bonuses[0]);
        Assert.Equal(3, catalog.ItemRefineEffects.Count);
        Assert.Equal(2, catalog.ItemRefineEffectsById[648].Count);
        Assert.Equal(16_002, catalog.ItemRefineEffectsById[648][0].Values[^1]);
        Assert.Equal(3, catalog.ItemRefineEffectEx.Count);
        Assert.Equal(2, catalog.ItemRefineEffectExByItemTypeId[2_100_095].Count);
        Assert.Equal([100, 5, 7], catalog.ItemRefineEffectExById[1].Values);
        Assert.Equal(3, catalog.ItemRefineUpgrades.Count);
        Assert.Equal(2, catalog.ItemRefineUpgradesById[1].Count);
        Assert.Equal(12_000, catalog.ItemRefineUpgradeByIdAndLevel[(1, 54)].Value);
        Assert.Equal(4, catalog.TransformInfo.Count);
        Assert.Equal(2, catalog.TransformInfoByTransformId[1350].Count);
        Assert.Equal(50000, catalog.TransformInfoByTransformIdAndLevel[(1270, 7)].MaxLife);
        Assert.Equal(2, catalog.TransformInfoByLookface[267].Count);
        Assert.Equal(2, catalog.TransformInfoByName["DivineHare"].Count);
        Assert.Equal(3, catalog.Furniture.Count);
        Assert.Equal("FlowerDressingTable", catalog.FurnitureById[9000].Name);
        Assert.Single(catalog.FurnitureByItemTypeId[726083]);
        Assert.Equal(2, catalog.FurnitureByMapId[1511].Count);
        Assert.Equal(2, catalog.FurnitureByMapPosition[(1511, 10, 20)].Count);
        Assert.Equal(2, catalog.FurnitureByKind[26].Count);
        Assert.Equal(60_000, catalog.FurnitureByName["ItemBox"].Single().Price);
        Assert.Equal(2, catalog.HairColorTypes.Count);
        Assert.Equal(3326778, catalog.HairColorTypeByHairColorIdAndColorIndex[(78, 5)].ItemTypeId);
        Assert.Equal(2, catalog.HairColorTypesByItemTypeId[3326778].Count);
        Assert.Equal(2, catalog.HairFaceStorageTypes.Count);
        Assert.Equal("YouthSpark", catalog.HairFaceStorageTypeByCategoryAndId[(0, 1)].Name);
        Assert.Single(catalog.HairFaceStorageTypesByName["YouthSpark"]);
        Assert.Equal(2, catalog.CoatStorageTypes.Count);
        Assert.Equal(31, catalog.CoatStorageTypesByItemTypeId[181305].StorageType);
        Assert.Equal(2, catalog.CoatStorageTypesByStorageType[31].Count);
        Assert.Equal(2, catalog.CoatStorageAttributes.Count);
        Assert.Equal(30, catalog.CoatStorageAttributesById[17].RequiredStorageScore);
        Assert.Equal(2, catalog.CoatStorageAttributesByStorageType[1_000_000_000].Count);
        Assert.Single(catalog.CoatColorRules);
        Assert.Equal(18, catalog.CoatColorRulesByItemTypeId[200412].Parameters.Count);
        Assert.Equal(2, catalog.XuanbaoAdditionAttributes.Count);
        Assert.Equal([0, 50], catalog.XuanbaoAdditionAttributeByAttributeTypeAndLevel[(14, 1)].Values);
        Assert.Single(catalog.XuanbaoRandomAttributesByItemTypeId[4100001]);
        Assert.Equal(30, catalog.XuanbaoRandomAttributeByItemTypeIdAndAttributeType[(4100001, 1)].MinValue);
        Assert.Equal(3, catalog.ChristmasNpcs.Count);
        Assert.Equal(2, catalog.ChristmasNpcsById[52316].Count);
        Assert.Single(catalog.ChristmasNpcsByMapId[1036]);
        Assert.Equal(52316, catalog.ChristmasNpcsByMapPosition[(9999, 290, 290)].Single().Id);
        Assert.Equal(2, catalog.ChristmasNpcsByLookface[15820].Count);
        Assert.Equal(2, catalog.TutorTypes.Count);
        Assert.Equal(70, catalog.TutorTypesById[1].MinLevel);
        Assert.Equal(2, catalog.TutorBattleLimitTypes.Count);
        Assert.Equal(100, catalog.TutorBattleLimitTypesById[1].BattlePowerLimit);
        Assert.Equal(2, catalog.BeastAttributes.Count);
        Assert.Equal([4800, 300, 400], catalog.BeastAttributesByLevel[2].Parameters.Take(3).ToArray());
        Assert.Equal(2, catalog.FateExperience.Count);
        Assert.Equal(1_500, catalog.FateExperienceByTypeAndLevel[(1, 2)].Experience);
        Assert.Equal(2, catalog.FateExperienceByType[1].Count);
        Assert.Equal(2, catalog.SpiritRates.Count);
        Assert.Equal([2, 1, 4_200_008, 0, 0], catalog.SpiritRatesById[151].Parameters);
        Assert.Single(catalog.SpiritRatesByItemTypeId[4_200_001]);
        Assert.Equal(2, catalog.HundredWeapons.Count);
        Assert.Equal(410, catalog.HundredWeaponsById[1].WeaponType);
        Assert.Equal(2, catalog.HundredWeaponsByWeaponType[410].Count);
        Assert.Equal(2, catalog.GouyuTypes.Count);
        Assert.Equal([0, 37_008, 0], catalog.GouyuTypesById[315].Values.Take(3).ToArray());
        Assert.Equal(2, catalog.GouyuTypesByType[100].Count);
        Assert.Equal(2, catalog.GouyuImmortals.Count);
        Assert.Equal(72, catalog.GouyuImmortalsById[17].Values[0]);
        Assert.Equal(2, catalog.SpecialEffectAttributes.Count);
        Assert.Equal(2, catalog.SpecialEffectAttributesByType[1].Count);
        Assert.Equal(12_000, catalog.SpecialEffectAttributesById[40_708_186].Values[3]);
        Assert.Equal(2, catalog.DaoqiDictTypes.Count);
        Assert.Equal(2, catalog.DaoqiDictTypesByType[0].Count);
        Assert.Equal(21_820_009, catalog.DaoqiDictTypesById[26_907].Values[0]);
        Assert.Equal(2, catalog.SyndicateFormationTypes.Count);
        Assert.Equal(2, catalog.SyndicateFormationTypesByType[100].Count);
        Assert.Equal(120_000_000L, catalog.SyndicateFormationTypesById[1].Values[1]);
        Assert.Equal(2, catalog.SyndicateSkillTypes.Count);
        Assert.Equal(2, catalog.SyndicateSkillTypesBySkillType[1].Count);
        Assert.Equal(75_050, catalog.SyndicateSkillTypesBySkillTypeAndLevel[(1, 1)].Values[1]);
        Assert.Equal(2, catalog.DisdainRules.Count);
        Assert.Equal(-90, catalog.DisdainRulesById[1].Values[6]);
        Assert.Equal(2, catalog.JiangHuAttributes.Count);
        Assert.Equal(2, catalog.JiangHuAttributesByType[101].Count);
        Assert.Equal(500, catalog.JiangHuAttributesByTypeAndLevel[(101, 1)].Value);
        Assert.Equal(2, catalog.JiangHuCultivateStatuses.Count);
        Assert.Equal([10, 20, 30], catalog.JiangHuCultivateStatusesById[1].Values);
        Assert.Equal(2, catalog.RefineryBoxes.Count);
        Assert.Equal([1, 2, 3], catalog.RefineryBoxesByItemType[3300001].Values);
        Assert.Equal(2, catalog.RefinerySouls.Count);
        Assert.Single(catalog.RefinerySoulsByLevel[1]);
        Assert.Equal("WeaponSoul", catalog.RefinerySoulsByItemType[800001].EquipmentName);
        Assert.Equal(2, catalog.RefineryMaterials.Count);
        Assert.Equal("Necklace", catalog.RefineryMaterialsByItemType[724350].Target);
        Assert.Equal(2, catalog.RefineryMaterialsByEffectType[7].Count);
        Assert.Single(catalog.RefineryMaterialsByTarget["Necklace"]);
        Assert.Equal(2, catalog.StateCountFiles.Count);
        Assert.Equal(0, catalog.StateCountFilesBySourceFile["BanIp.txt"].DeclaredCount);
        Assert.Equal("198.51.100.15", catalog.StateCountFilesBySourceFile["Votes.txt"].Entries.Single().Fields[1]);
        Assert.Equal(2, catalog.BinarySeedFiles.Count);
        Assert.Equal(4, catalog.BinarySeedFilesBySourceFile["Insults.bin"].Length);
        Assert.Equal("AAAAAA==", catalog.BinarySeedFilesBySourceFile["Insults.bin"].Base64);
        Assert.Equal(2, catalog.EmptySeedFiles.Count);
        Assert.Equal(0, catalog.EmptySeedFilesBySourceFile["HeroRewards.txt"].Length);
        Assert.Equal("", catalog.EmptySeedFilesBySourceFile["ItemtypeSub.txt"].Base64);
        Assert.Single(catalog.BackupSeedFiles);
        Assert.Equal(4, catalog.BackupSeedFilesBySourceFile["SobNpcs.txt.bak"].Length);
        Assert.Equal("77u/MQ==", catalog.BackupSeedFilesBySourceFile["SobNpcs.txt.bak"].Base64);
        Assert.Equal(2, catalog.PokerTables.Count);
        Assert.Equal("013", catalog.PokerTablesById[1].Fields[1]);
        Assert.Equal(2, catalog.RouletteTables.Count);
        Assert.Equal(3002, catalog.RouletteTablesById[1].Values[1]);
        Assert.Equal(2, catalog.NpcTimeEntries.Count);
        Assert.Equal("Arena", catalog.NpcTimeEntriesById[1].Label);
        Assert.Equal(2, catalog.ActivityTasks.Tasks.Count);
        Assert.Equal("Daily Gift", catalog.ActivityTasksBySectionId[7].Title);
        Assert.Equal("0141", catalog.ActivityTasksById[1007].CloseLevelRaw);
        Assert.Equal("Training", catalog.ActivityTasksByType[2].Single().Title);
        Assert.Equal(1007, catalog.ActivityTasksByTitle["Daily Gift"].Single().Id);
        Assert.Equal(2, catalog.MeltingTypeRules.Count);
        Assert.Equal(2, catalog.MeltingTypeRulesByItemTypeId[700002].Count);
        Assert.Equal([0, 0, 1], catalog.MeltingTypeRulesBySourceFile["MeltingType.ini"][0].Parameters);
        Assert.Equal(2, catalog.LotteryPrizes.Count);
        Assert.Equal(2, catalog.LotteryPrizesByType[1].Count);
        Assert.Equal("WavyHeavyRing+8", catalog.LotteryPrizesByItemTypeId[151159].Single().ItemName);
        Assert.Equal(624009, catalog.LotteryPrizesByItemName["SkyFlame(+8)"].Single().ItemTypeId);
        Assert.Equal(2, catalog.ArenaRecords.Count);
        Assert.Equal("ArenaOne", catalog.ArenaRecordsByPlayerId[1001].PlayerName);
        Assert.Equal(4000, catalog.ArenaRecordsByRank[1].Prize);
        Assert.Equal(2, catalog.TeamArenaRecords.Count);
        Assert.Equal("TeamArenaOne", catalog.TeamArenaRecordsByPlayerId[2001].PlayerName);
        Assert.Equal(0, catalog.TeamArenaRecordsByRank[2].Prize);
        Assert.Equal(2, catalog.ElitePkRecords.Count);
        Assert.Equal("EliteOne", catalog.ElitePkRecordsByGroupAndSlot[(3, 7)].PlayerName);
        Assert.Equal("TeamOne", catalog.TeamElitePkRecordsByGroupAndSlot[(2, 6)].PlayerName);
        Assert.Equal("SkillOne", catalog.SkillTeamPkRecordsByGroupAndSlot[(1, 5)].PlayerName);
        Assert.Equal(7001, catalog.ClassPkWarRecordsByClassAndSlot[(10, 3)].PlayerId);
        Assert.Equal(2, catalog.CombatGearRanksByCharacterId[5001].Count);
        Assert.Equal(2, catalog.PrestigeRanksByCharacterId[5001].Count);
        Assert.Equal(2, catalog.InnerPowerRanksByCharacterId[5001].Count);
        Assert.Equal(2, catalog.RuneRankingsByCharacterId[5001].Count);
        Assert.Equal(314_800, catalog.RuneRankingsByCharacterName["RuneOne"].Single().Score);
        Assert.Single(catalog.StaticStatuesByRecordId[298]);
        Assert.Equal(179, catalog.StaticStatuesByRecordId[299].Single().Values[^1]);
        Assert.Equal(2, catalog.NewPlayerRewardsByIpAddress["10.0.0.1"].Count);
        Assert.Equal("127.0.0.1", catalog.VotesByCharacterId[7001].IpAddress);
        Assert.Equal("PartnerOne", catalog.CouplesPkRecordsByPlayerName["PlayerOne"].PartnerName);
        Assert.Equal(2, catalog.SocialAssociationsByOwnerId[1293116].Count);
        Assert.Equal(2, catalog.SocialAssociationsByRelationType[4].Count);
        Assert.Equal(2, catalog.SocialAssociationsByTargetId[1293600].Count);
        Assert.Equal("hey", catalog.SocialAssociationsByTargetName["Coder[GM]"].Single().Note);
        Assert.Equal(1363, catalog.GuildWarInfo.Info?.Id);
        Assert.Equal(8_000, catalog.GuildWarInfo.Pole?.HitPoints);
        Assert.Equal(2000, catalog.SuperGuildWarInfo.Info?.Id);
        Assert.Equal("0001", catalog.StageGoalStagesById[1].OpenLevelRaw);
        Assert.Single(catalog.StageGoalTasksByStageId[1]);
        Assert.Equal("[Novice] Level-up", catalog.StageGoalTasksByStageAndGoalId[(1, 1)].Title);
        Assert.Equal(2, catalog.NpcsById[200].Count);
        Assert.Equal(2, catalog.SobNpcsById[25005].Count);
        Assert.Single(catalog.SobNpcsByMapId[1000]);
        Assert.Equal(2, catalog.ShopsById[432].Count);
        Assert.Equal(2, catalog.ShopItemsByShopId[432].Count);
        Assert.Single(catalog.HonorShopItemsByItemTypeId[3303085]);
        Assert.Single(catalog.RacePointShopItemsByItemTypeId[3009000]);
        Assert.Single(catalog.GoldenLeagueShopItemsByItemTypeId[3303526]);
        Assert.Equal("0xffffffff", catalog.GoldenLeagueShopConfig["color.tipcolor"]);
        Assert.Equal(2, catalog.BoothsById[700050].Count);
        Assert.Equal(2, catalog.BoothItemsByBoothId[700050].Count);
        Assert.Equal(2, catalog.ItemAddBonusesByItemTypeId[205003].Count);
        Assert.Equal([0, 0, 0, 4, 0, 2, 0, 0], catalog.ItemAddBonusByItemTypeIdAndLevel[(205003, 2)].Values);
        Assert.Equal(2_641_561_501_496, catalog.LevelExperienceByTypeAndLevel[(0, 139)].Experience);
        Assert.Single(catalog.LevelExperienceByType[1]);
        Assert.Single(catalog.ClassLevelStatsByClassName["WindWalker"]);
        Assert.Equal(144, catalog.ClassLevelStatsByClassAndLevel[("Thunderstriker", 120)].Vitality);
        Assert.Equal(120, catalog.ProfessionLevelUpsByProfessionCode[16051].RequiredLevel);
        Assert.Equal(2600, catalog.ProfessionLevelBenefitsByProfessionCode[16051].BonusValues[^1]);
        Assert.Equal(100100, catalog.ProfessionTitleBenefitsByProfessionAndRank[(16, 3)].BonusValues[2]);
        Assert.Equal("Recruit Quest", catalog.QuestTaskTypesById[1].Name);
        Assert.Equal(2, catalog.QuestMissionsByMissionId[650].Count);
        Assert.Equal(2, catalog.QuestMissionsByTypeId[1].Count);
        Assert.Equal(2, catalog.QuizQuestions.Count);
        Assert.Equal("Broadcast", catalog.QuizQuestionsByPosition[1].CorrectAnswer);
        Assert.Equal(2, catalog.QuizQuestionsByQuestion["In~which~channel~can~all~the~players~in~the~same~server~see~messages?"].Count);
        Assert.Single(catalog.QuizQuestionsByCorrectOption[3]);
    }

    [Fact]
    public async Task LoaderPrefersMutableInnerPowerSourceOverExportedSeedCache()
    {
        WriteFixture();
        var liveRoot = Path.Combine(_rootPath, "live-db");
        Directory.CreateDirectory(liveRoot);
        File.WriteAllText(
            Path.Combine(liveRoot, "InnerPower.txt"),
            "Count=1\r\nLiveHero/1293600/7/1/1/1/1/11/100/1/5/0/\r\n",
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var manifest = JsonSerializer.Deserialize<SeedManifest>(
            File.ReadAllText(Path.Combine(_rootPath, "manifest.json")),
            JsonOptions);
        Assert.NotNull(manifest);
        WriteJson("manifest.json", manifest with { SourcePath = liveRoot });

        var catalog = await new JsonWorldDataLoader(new WorldDataOptions(_rootPath)).LoadAsync();

        var row = Assert.Single(catalog.InnerPowerRanks);
        Assert.Equal("LiveHero", row.CharacterName);
        Assert.Equal(1_293_600, row.CharacterId);
        Assert.Equal(13, row.Fields.Count);
        Assert.Equal("100", row.Fields[8]);
        Assert.Same(row, Assert.Single(catalog.InnerPowerRanksByCharacterId[1_293_600]));
    }

    [Fact]
    public async Task LoaderPreservesDuplicateIdsInGroupedIndexesAndReportsDiagnostics()
    {
        WriteFixture();
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Equal(2, catalog.ItemsById[205003].Count);
        Assert.Equal(2, catalog.ItemTypeSubsByItemTypeId[205003].Count);
        Assert.Equal(2, catalog.LevelExperienceByTypeAndLevel.Count);
        Assert.Equal(2, catalog.QuestMissionsByMissionId[650].Count);
        Assert.Equal(2, catalog.NpcsById[200].Count);
        Assert.Equal(2, catalog.MagicEffectsById[5_002_142_000].Count);
        Assert.Equal(2, catalog.SobNpcsById[25005].Count);
        Assert.Equal(2, catalog.MonsterSpawnsByGeneratorId[1].Count);
        Assert.Equal(2, catalog.ShopsById[432].Count);
        Assert.Equal(2, catalog.BoothsById[700050].Count);
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateItemId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateLevelExperienceKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateQuestMissionId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateMapNameId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateNpcId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateSobNpcId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateMonsterSpawnGeneratorId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateCollectionRewardTierId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateCollectionRewardTierKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateItemRefineEffectId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateItemRefineEffectExId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateItemRefineUpgradeKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateTransformInfoKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingTransformMagicType");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateFurnitureId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateHairColorTypeKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateXuanbaoAdditionAttributeKey");
        Assert.DoesNotContain(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateChristmasNpcId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateSyndicateSkillTypeId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateSyndicateSkillTypeKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateDisdainRuleId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateJiangHuAttributeId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateJiangHuAttributeKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateJiangHuCultivateStatusId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateRefineryBoxItemType");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateRefinerySoulItemType");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicatePokerTableId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateRouletteTableId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateNpcTimeEntryId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateArenaPlayerId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateArenaRank");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateTeamArenaPlayerId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateTeamArenaRank");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateElitePkKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateTeamElitePkKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateSkillTeamPkKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateClassPkWarKey");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateVoteCharacterId");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "DuplicateCouplesPkPlayerName");
    }

    [Fact]
    public async Task LoaderAllowsDuplicateLegacyLoadMapMonsterRowIdsWithoutGeneratorDiagnostic()
    {
        WriteFixture(monsterSpawnsOverride:
        [
            LoadMapMonsterSpawn(402541, 1002, 10, "Monsters1002.txt", sourceLine: 1),
            LoadMapMonsterSpawn(402541, 1002, 10, "Monsters1002.txt", sourceLine: 2),
        ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Equal(2, catalog.MonsterSpawnsByGeneratorId[402541].Count);
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "DuplicateMonsterSpawnGeneratorId");
    }

    [Fact]
    public async Task LoaderPreservesParserNormalizedLegacyHighAttackForNonBossMonsterFamilies()
    {
        var normalizedNonBoss = MonsterParser.Parse(IniDocument.Parse("""
            [cq_monstertype]
            id=20
            name=HardHitter
            attack_min=7001
            attack_max=9001
            Boss=0
            """));
        var boss = MonsterParser.Parse(IniDocument.Parse("""
            [cq_monstertype]
            id=21
            name=BossHitter
            attack_min=7001
            attack_max=9001
            Boss=1
            """));
        var threshold = MonsterParser.Parse(IniDocument.Parse("""
            [cq_monstertype]
            id=22
            name=NormalHitter
            attack_min=2000
            attack_max=3000
            Boss=0
            """));

        Assert.Equal("3500", normalizedNonBoss.Attributes["attack_min"]);
        Assert.Equal("4500", normalizedNonBoss.Attributes["attack_max"]);

        WriteFixture(monstersOverride:
        [
            new Monster(
                10,
                "Pheasant",
                new Dictionary<string, string> { ["Level"] = "7" },
                []),
            normalizedNonBoss,
            boss,
            threshold,
        ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Equal("3500", catalog.MonstersById[20].Attributes["attack_min"]);
        Assert.Equal("4500", catalog.MonstersById[20].Attributes["attack_max"]);
        Assert.Equal("7001", catalog.MonstersById[21].Attributes["attack_min"]);
        Assert.Equal("9001", catalog.MonstersById[21].Attributes["attack_max"]);
        Assert.Equal("2000", catalog.MonstersById[22].Attributes["attack_min"]);
        Assert.Equal("3000", catalog.MonstersById[22].Attributes["attack_max"]);
    }

    [Fact]
    public async Task LoaderReportsBrokenLinksWithoutFailing()
    {
        WriteFixture(
            questMissionsOverride:
            [
                QuestMission(1, 650, 1000, [205003], [608, 9999], intentAmount: 2),
                QuestMission(
                    2,
                    650,
                    9998,
                    [999999],
                    [],
                    intentAmount: 0,
                    finishNpcAnchors:
                    [
                        new QuestNpcAnchor(
                            "Finish",
                            "FinishNpcId",
                            null,
                            999999,
                            9998,
                            null,
                            21,
                            "MissingNpc",
                            "MissingMap",
                            "999999,9998,,21,MissingNpc,MissingMap",
                            SourceLine: 2,
                            Fields: ["999999", "9998", "", "21", "MissingNpc", "MissingMap"]),
                    ]),
            ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingSpawnMap");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingSpawnMonster");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingQuestPrerequisite");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingQuestMap");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingQuestPrizeItem");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingQuestAnchorMap");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "QuestIntentAmountMismatch");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingMonsterSpecialDropItem");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingNpcMap");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingSobNpcMap");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingChristmasNpcMap");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingPortalTargetMap");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingFurnitureMap");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingFurnitureItem");
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingMagicTypeOperationReference");
        Assert.DoesNotContain(catalog.Diagnostics, diagnostic => diagnostic.Code == "MapNameConflict");
    }

    [Fact]
    public async Task LoaderReportsMissingMapDiagnosticsWithSourceContext()
    {
        WriteFixture();
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        var sobNpcMap = Assert.Single(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingSobNpcMap" &&
                diagnostic.Message.Contains("GateB", StringComparison.Ordinal));
        Assert.Contains("map 9999", sobNpcMap.Message);
        Assert.Contains("(85,70)", sobNpcMap.Message);

        var portalTargetMap = Assert.Single(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingPortalTargetMap" &&
                diagnostic.Message.Contains("target map 9999", StringComparison.Ordinal));
        Assert.Contains("map 1000 at (50,50)", portalTargetMap.Message);
        Assert.Contains("map 9999 at (60,60)", portalTargetMap.Message);
    }

    [Fact]
    public async Task LoaderTreatsCompleteQuestNpcAnchorsAsNavigationTargets()
    {
        WriteFixture(
            questMissionsOverride:
            [
                QuestMission(
                    1,
                    590032,
                    1000,
                    [205003],
                    [],
                    intentAmount: 1,
                    finishNpcAnchors:
                    [
                        new QuestNpcAnchor(
                            "Finish",
                            "FinishNpcId",
                            null,
                            26228,
                            1000,
                            313,
                            451,
                            "NightBat",
                            "TwinCity",
                            "26228,1000,313,451,NightBat,TwinCity",
                            SourceLine: 10,
                            Fields: ["26228", "1000", "313", "451", "NightBat", "TwinCity"]),
                    ]),
            ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingQuestAnchorNpc" &&
                diagnostic.Message.Contains("26228", StringComparison.Ordinal));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingQuestAnchorMap" &&
                diagnostic.Message.Contains("1000", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoaderTreatsQuestInfoNavigationMapsAsQuestOwnedMapReferences()
    {
        WriteFixture(
            questMissionsOverride:
            [
                QuestMission(
                    1,
                    6852,
                    10209,
                    [205003],
                    [],
                    intentAmount: 1,
                    beginNpcAnchors:
                    [
                        new QuestNpcAnchor(
                            "Begin",
                            "BeginNpcId",
                            null,
                            0,
                            10209,
                            165,
                            239,
                            "devils",
                            "1F",
                            "0,10209,165,239,devils,1F",
                            SourceLine: 10,
                            Fields: ["0", "10209", "165", "239", "devils", "1F"]),
                    ],
                    finishNpcAnchors:
                    [
                        new QuestNpcAnchor(
                            "Finish",
                            "FinishNpcId",
                            null,
                            0,
                            10209,
                            165,
                            239,
                            "devils",
                            "1F",
                            "0,10209,165,239,devils,1F",
                            SourceLine: 11,
                            Fields: ["0", "10209", "165", "239", "devils", "1F"]),
                    ]),
                QuestMission(
                    2,
                    3920,
                    1000,
                    [205003],
                    [],
                    intentAmount: 1,
                    finishNpcAnchors:
                    [
                        new QuestNpcAnchor(
                            "Finish",
                            "FinishNpcId",
                            null,
                            11411,
                            10209,
                            150,
                            367,
                            "CavernEliminationEnvoy",
                            "TwinCity",
                            "11411,10209,150,367,CavernEliminationEnvoy,TwinCity",
                            SourceLine: 20,
                            Fields: ["11411", "10209", "150", "367", "CavernEliminationEnvoy", "TwinCity"]),
                    ]),
            ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => (diagnostic.Code is "MissingQuestMap" or "MissingQuestAnchorMap") &&
                diagnostic.Message.Contains("10209", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoaderReportsIncompleteQuestAnchorMapAsMissing()
    {
        WriteFixture(
            questMissionsOverride:
            [
                QuestMission(
                    1,
                    4301,
                    1000,
                    [205003],
                    [],
                    intentAmount: 1,
                    finishNpcAnchors:
                    [
                        new QuestNpcAnchor(
                            "Finish",
                            "FinishNpcId",
                            null,
                            0,
                            10653,
                            null,
                            190,
                            "Yiraiya",
                            "ToadMountain",
                            "0,10653,,190,Yiraiya,ToadMountain",
                            SourceLine: 10,
                            Fields: ["0", "10653", "", "190", "Yiraiya", "ToadMountain"]),
                    ]),
            ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingQuestAnchorMap" &&
                diagnostic.Message.Contains("10653", StringComparison.Ordinal));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingQuestAnchorNpc" &&
                diagnostic.Message.Contains("10653", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoaderStillReportsIncompleteMissingQuestNpcAnchors()
    {
        WriteFixture(
            questMissionsOverride:
            [
                QuestMission(
                    1,
                    590033,
                    1000,
                    [205003],
                    [],
                    intentAmount: 1,
                    finishNpcAnchors:
                    [
                        new QuestNpcAnchor(
                            "Finish",
                            "FinishNpcId",
                            null,
                            26228,
                            1000,
                            null,
                            451,
                            "NightBat",
                            "TwinCity",
                            "26228,1000,,451,NightBat,TwinCity",
                            SourceLine: 10,
                            Fields: ["26228", "1000", "", "451", "NightBat", "TwinCity"]),
                    ]),
            ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Contains(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingQuestAnchorNpc" &&
                diagnostic.Message.Contains("26228", StringComparison.Ordinal));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingQuestAnchorMap" &&
                diagnostic.Message.Contains("1000", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoaderTreatsGameMapExOnlyMapNamesAsValidMapLinks()
    {
        WriteFixture(
            mapNamesOverride:
            [
                new MapName(
                    1000,
                    "Desert",
                    SourceFile: "GameMapEx.ini",
                    SectionLine: 1,
                    NameLine: 2,
                    SourceSection: "1000",
                    RawNameLine: "Name=Desert"),
                new MapName(
                    9999,
                    "SourceOnlyPortalMap",
                    SourceFile: "GameMapEx.ini",
                    SectionLine: 4,
                    NameLine: 5,
                    SourceSection: "9999",
                    RawNameLine: "Name=SourceOnlyPortalMap"),
            ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingNpcMap" &&
                diagnostic.Message.Contains("9999", StringComparison.Ordinal));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingSobNpcMap" &&
                diagnostic.Message.Contains("9999", StringComparison.Ordinal));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingSpawnMap" &&
                diagnostic.Message.Contains("9999", StringComparison.Ordinal));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingPortalTargetMap" &&
                diagnostic.Message.Contains("9999", StringComparison.Ordinal));
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingSpawnMonster");
    }

    [Fact]
    public async Task LoaderTreatsLegacyGameMapDatEntriesAsValidMapLinks()
    {
        WriteFixture(
            mapNamesOverride:
            [
                new MapName(
                    1000,
                    "Desert",
                    SourceFile: "GameMapEx.ini",
                    SectionLine: 1,
                    NameLine: 2,
                    SourceSection: "1000",
                    RawNameLine: "Name=Desert"),
            ],
            legacyGameMapsOverride:
            [
                new LegacyGameMapEntry(9999, "map/map9999.7z", "map/map9999.dmap", 128),
            ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Equal(9999, catalog.LegacyGameMaps.Single().MapId);
        Assert.True(catalog.LegacyGameMapsByMapId.ContainsKey(9999));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingNpcMap" &&
                diagnostic.Message.Contains("9999", StringComparison.Ordinal));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingSobNpcMap" &&
                diagnostic.Message.Contains("9999", StringComparison.Ordinal));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingSpawnMap" &&
                diagnostic.Message.Contains("9999", StringComparison.Ordinal));
        Assert.DoesNotContain(
            catalog.Diagnostics,
            diagnostic => diagnostic.Code == "MissingPortalTargetMap" &&
                diagnostic.Message.Contains("9999", StringComparison.Ordinal));
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingSpawnMonster");
    }

    [Fact]
    public async Task LoaderTreatsLegacyMapAliasesAsValidWhenBaseMapsAreLoaded()
    {
        WriteFixture(
            mapNamesOverride:
            [
                new MapName(
                    1000,
                    "Desert",
                    SourceFile: "GameMapEx.ini",
                    SectionLine: 1,
                    NameLine: 2,
                    SourceSection: "1000",
                    RawNameLine: "Name=Desert"),
            ],
            legacyGameMapsOverride:
            [
                new LegacyGameMapEntry(10088, "map/family06-01_new.7z", "map/family06-01_new.dmap", 128),
                new LegacyGameMapEntry(2071, "map/bp-kingfight.7z", "map/bp-kingfight.dmap", 256),
                new LegacyGameMapEntry(3825, "map/epicmission.7z", "map/epicmission.dmap", 128),
                new LegacyGameMapEntry(3056, "map/7godmind.7z", "map/7godmind.dmap", 128),
            ],
            npcsOverride:
            [
                new Npc(200006, 2, 51854, 44455, 154, 146, 1),
            ],
            sobNpcsOverride:
            [
                new SobNpc(6539, "EliteGuildWar", 10, 1137, 6521, 125, 124, 250_000_000, 250_000_000, 17, 1, null, 1, "6539,EliteGuildWar,10,1137,6521,125,124,250000000,250000000,17,1", ["6539", "EliteGuildWar", "10", "1137", "6521", "125", "124", "250000000", "250000000", "17", "1"]),
                new SobNpc(6606, "WarOfPlayers", 10, 1137, 6729, 45, 45, 50_000_000, 50_000_000, 17, 1, null, 2, "6606,WarOfPlayers,10,1137,6729,45,45,50000000,50000000,17,1", ["6606", "WarOfPlayers", "10", "1137", "6729", "45", "45", "50000000", "50000000", "17", "1"]),
            ],
            monsterSpawnsOverride:
            [
                Spawn(1, 3056, 10),
                Spawn(2, 3046, 10),
            ]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.DoesNotContain(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingNpcMap" && diagnostic.Message.Contains("44455", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingSobNpcMap" && diagnostic.Message.Contains("6521", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingSobNpcMap" && diagnostic.Message.Contains("6729", StringComparison.Ordinal));
        Assert.DoesNotContain(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingSpawnMap" && diagnostic.Message.Contains("3056", StringComparison.Ordinal));
        Assert.Contains(catalog.Diagnostics, diagnostic => diagnostic.Code == "MissingSpawnMap" && diagnostic.Message.Contains("3046", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoaderAssignsSequentialLegacyMonsterRuntimeUidBases()
    {
        var firstSpawn = Spawn(73, 1015, 10) with
        {
            MaxNpc = 2,
            MaxPerGenerator = 3,
            SourceRelativePath = Path.Combine("1015", "Pheasant", "1.ini"),
        };
        var secondSpawn = Spawn(73, 1015, 10) with
        {
            MaxNpc = 9,
            MaxPerGenerator = 4,
            SourceRelativePath = Path.Combine("1015", "Pheasant", "5.ini"),
        };
        WriteFixture(monsterSpawnsOverride: [firstSpawn, secondSpawn]);
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Equal(400_000U, catalog.GetLegacyMonsterRuntimeUidBase(catalog.MonsterSpawns[0]));
        Assert.Equal(400_009U, catalog.GetLegacyMonsterRuntimeUidBase(catalog.MonsterSpawns[1]));
        Assert.All(
            catalog.LegacyMonsterRuntimeUidBases.Values,
            uidBase => Assert.InRange(uidBase, 400_000U, 999_999U));
    }

    [Fact]
    public async Task LoaderReportsManifestCountMismatches()
    {
        WriteFixture(countOverrides: new Dictionary<string, int> { ["mapNames"] = 99 });
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        var mismatch = Assert.Single(catalog.Diagnostics, diagnostic => diagnostic.Code == "ManifestCountMismatch");
        Assert.Contains("'mapNames'", mismatch.Message);
    }

    [Fact]
    public async Task LoaderReportsMissingSeedFileName()
    {
        WriteFixture();
        File.Delete(Path.Combine(_rootPath, "mapNames.json"));
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() => loader.LoadAsync());

        Assert.Contains("mapNames.json", ex.Message);
        Assert.EndsWith("mapNames.json", ex.FileName);
    }

    [Fact]
    public async Task LoaderNormalizesJsonAttributeDictionariesToCaseInsensitiveLookups()
    {
        WriteFixture();
        var loader = new JsonWorldDataLoader(new WorldDataOptions(_rootPath));

        var catalog = await loader.LoadAsync();

        Assert.Equal("12", catalog.MapsById[1000].Attributes["TYPE"]);
        Assert.Equal("145", catalog.MagicEffectsById[5_002_142_000][0].Attributes["status"]);
        Assert.Equal("7", catalog.MonstersById[10].Attributes["level"]);
        Assert.Equal("1", catalog.MonsterSpawnsByGeneratorId[1][0].Attributes["RETURN_MODE"]);
        Assert.Equal("fixture", catalog.ShopsById[432][0].Attributes["SOURCE"]);
        Assert.Equal("Item0", catalog.ShopItemsByShopId[432][0].Attributes["KEY"]);
        Assert.Equal("700050", catalog.BoothsById[700050][0].Attributes["id"]);
    }

    [Fact(Skip = "Requires external game fixtures omitted from the public source snapshot.")]
    public async Task LoaderReadsExportedWorkspaceSeedJsonWhenPresent()
    {
        var seedPath = Path.Combine(FindRepositoryRoot(), ".setup_work", "seed-json");
        if (!File.Exists(Path.Combine(seedPath, "manifest.json")))
        {
            return;
        }

        var catalog = await new JsonWorldDataLoader(new WorldDataOptions(seedPath)).LoadAsync();

        Assert.Equal(catalog.Manifest.Counts["clientConfigs"], catalog.ClientConfigs.Count);
        Assert.Equal(catalog.Manifest.Counts["items"], catalog.Items.Count);
        if (catalog.Manifest.Counts.TryGetValue("itemTypeSubs", out var itemTypeSubsCount))
        {
            Assert.Equal(itemTypeSubsCount, catalog.ItemTypeSubs.Count);
        }

        Assert.Equal(catalog.Manifest.Counts["itemAddBonuses"], catalog.ItemAddBonuses.Count);
        Assert.Equal(catalog.Manifest.Counts["levelExperience"], catalog.LevelExperience.Count);
        Assert.Equal(catalog.Manifest.Counts["classLevelStats"], catalog.ClassLevelStats.Count);
        Assert.Equal(catalog.Manifest.Counts["professionLevelUps"], catalog.ProfessionLevelUps.Count);
        Assert.Equal(catalog.Manifest.Counts["professionLevelBenefits"], catalog.ProfessionLevelBenefits.Count);
        Assert.Equal(catalog.Manifest.Counts["professionTitleBenefits"], catalog.ProfessionTitleBenefits.Count);
        Assert.Equal(catalog.Manifest.Counts["questTaskTypes"], catalog.QuestTaskTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["questMissions"], catalog.QuestMissions.Count);
        Assert.Equal(catalog.Manifest.Counts["quizQuestions"], catalog.QuizQuestions.Count);
        Assert.Equal(catalog.Manifest.Counts["magic"], catalog.Magic.Count);
        Assert.Equal(catalog.Manifest.Counts["magicEffects"], catalog.MagicEffects.Count);
        Assert.Equal(catalog.Manifest.Counts["magicTypeOperations"], catalog.MagicTypeOperations.Count);
        Assert.Equal(catalog.Manifest.Counts["titleTypes"], catalog.TitleTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["combatGear"], catalog.CombatGear.Count);
        Assert.Equal(catalog.Manifest.Counts["traps"], catalog.Traps.Count);
        Assert.Equal(catalog.Manifest.Counts["npcs"], catalog.Npcs.Count);
        Assert.Equal(catalog.Manifest.Counts["sobNpcs"], catalog.SobNpcs.Count);
        Assert.Equal(catalog.Manifest.Counts["portals"], catalog.Portals.Count);
        Assert.Equal(catalog.Manifest.Counts["maps"], catalog.Maps.Count);
        Assert.Equal(catalog.Manifest.Counts["mapNames"], catalog.MapNames.Count);
        if (catalog.Manifest.Counts.TryGetValue("legacyGameMaps", out var legacyGameMapsCount))
        {
            Assert.Equal(legacyGameMapsCount, catalog.LegacyGameMaps.Count);
        }

        Assert.Equal(catalog.Manifest.Counts["monsters"], catalog.Monsters.Count);
        Assert.Equal(catalog.Manifest.Counts["monsterSpawns"], catalog.MonsterSpawns.Count);
        Assert.Equal(catalog.Manifest.Counts["lotteryDictionary"], catalog.LotteryDictionary.Count);
        Assert.Equal(catalog.Manifest.Counts["collectionRewardTiers"], catalog.CollectionRewardTiers.Count);
        Assert.Equal(catalog.Manifest.Counts["taskRewardTypes"], catalog.TaskRewardTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["taskRewardTypeIniEntries"], catalog.TaskRewardTypeIniEntries.Count);
        Assert.Equal(1, catalog.Manifest.Counts["neiGongHeader"]);
        Assert.Equal(catalog.Manifest.Counts["neiGongSecrets"], catalog.NeiGongSecrets.Count);
        Assert.Equal(catalog.Manifest.Counts["neiGongTypes"], catalog.NeiGongTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["subProfessions"], catalog.SubProfessions.Count);
        Assert.Equal(catalog.Manifest.Counts["syndicateLevels"], catalog.SyndicateLevels.Count);
        Assert.Equal(catalog.Manifest.Counts["runeLevelExperience"], catalog.RuneLevelExperience.Count);
        Assert.Equal(catalog.Manifest.Counts["runeStorageAttributes"], catalog.RuneStorageAttributes.Count);
        Assert.Equal(catalog.Manifest.Counts["itemRefineEffects"], catalog.ItemRefineEffects.Count);
        Assert.Equal(catalog.Manifest.Counts["itemRefineEffectEx"], catalog.ItemRefineEffectEx.Count);
        Assert.Equal(catalog.Manifest.Counts["itemRefineUpgrades"], catalog.ItemRefineUpgrades.Count);
        Assert.Equal(catalog.Manifest.Counts["transformInfo"], catalog.TransformInfo.Count);
        Assert.Equal(catalog.Manifest.Counts["furniture"], catalog.Furniture.Count);
        Assert.Equal(catalog.Manifest.Counts["hairColorTypes"], catalog.HairColorTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["hairFaceStorageTypes"], catalog.HairFaceStorageTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["coatStorageTypes"], catalog.CoatStorageTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["coatStorageAttributes"], catalog.CoatStorageAttributes.Count);
        Assert.Equal(catalog.Manifest.Counts["coatColorRules"], catalog.CoatColorRules.Count);
        Assert.Equal(catalog.Manifest.Counts["xuanbaoAdditionAttributes"], catalog.XuanbaoAdditionAttributes.Count);
        Assert.Equal(catalog.Manifest.Counts["xuanbaoComposeAttributeLimits"], catalog.XuanbaoComposeAttributeLimits.Count);
        Assert.Equal(catalog.Manifest.Counts["xuanbaoRandomAttributes"], catalog.XuanbaoRandomAttributes.Count);
        Assert.Equal(catalog.Manifest.Counts["christmasNpcs"], catalog.ChristmasNpcs.Count);
        Assert.Equal(catalog.Manifest.Counts["tutorTypes"], catalog.TutorTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["tutorBattleLimitTypes"], catalog.TutorBattleLimitTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["beastAttributes"], catalog.BeastAttributes.Count);
        Assert.Equal(catalog.Manifest.Counts["fateExperience"], catalog.FateExperience.Count);
        Assert.Equal(catalog.Manifest.Counts["spiritRates"], catalog.SpiritRates.Count);
        Assert.Equal(catalog.Manifest.Counts["hundredWeapons"], catalog.HundredWeapons.Count);
        Assert.Equal(catalog.Manifest.Counts["gouyuTypes"], catalog.GouyuTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["gouyuImmortals"], catalog.GouyuImmortals.Count);
        Assert.Equal(catalog.Manifest.Counts["specialEffectAttributes"], catalog.SpecialEffectAttributes.Count);
        Assert.Equal(catalog.Manifest.Counts["daoqiDictTypes"], catalog.DaoqiDictTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["syndicateFormationTypes"], catalog.SyndicateFormationTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["syndicateSkillTypes"], catalog.SyndicateSkillTypes.Count);
        Assert.Equal(catalog.Manifest.Counts["disdainRules"], catalog.DisdainRules.Count);
        Assert.Equal(catalog.Manifest.Counts["jiangHuAttributes"], catalog.JiangHuAttributes.Count);
        Assert.Equal(catalog.Manifest.Counts["jiangHuCultivateStatuses"], catalog.JiangHuCultivateStatuses.Count);
        Assert.Equal(catalog.Manifest.Counts["refineryBoxes"], catalog.RefineryBoxes.Count);
        Assert.Equal(catalog.Manifest.Counts["refinerySouls"], catalog.RefinerySouls.Count);
        Assert.Equal(catalog.Manifest.Counts["refineryMaterials"], catalog.RefineryMaterials.Count);
        Assert.Equal(catalog.Manifest.Counts["pokerTables"], catalog.PokerTables.Count);
        Assert.Equal(catalog.Manifest.Counts["rouletteTables"], catalog.RouletteTables.Count);
        Assert.Equal(catalog.Manifest.Counts["npcTimeEntries"], catalog.NpcTimeEntries.Count);
        Assert.Equal(catalog.Manifest.Counts["activityTasks"], catalog.ActivityTasks.Tasks.Count);
        Assert.Equal(catalog.Manifest.Counts["meltingTypeRules"], catalog.MeltingTypeRules.Count);
        Assert.Equal(catalog.Manifest.Counts["lotteryPrizes"], catalog.LotteryPrizes.Count);
        Assert.Equal(catalog.Manifest.Counts["arenaRecords"], catalog.ArenaRecords.Count);
        Assert.Equal(catalog.Manifest.Counts["teamArenaRecords"], catalog.TeamArenaRecords.Count);
        Assert.Equal(catalog.Manifest.Counts["elitePkRecords"], catalog.ElitePkRecords.Count);
        Assert.Equal(catalog.Manifest.Counts["teamElitePkRecords"], catalog.TeamElitePkRecords.Count);
        Assert.Equal(catalog.Manifest.Counts["skillTeamPkRecords"], catalog.SkillTeamPkRecords.Count);
        Assert.Equal(catalog.Manifest.Counts["classPkWarRecords"], catalog.ClassPkWarRecords.Count);
        Assert.Equal(catalog.Manifest.Counts["combatGearRanks"], catalog.CombatGearRanks.Count);
        Assert.Equal(catalog.Manifest.Counts["prestigeRanks"], catalog.PrestigeRanks.Count);
        Assert.Equal(catalog.Manifest.Counts["innerPowerRanks"], catalog.InnerPowerRanks.Count);
        Assert.Equal(catalog.Manifest.Counts["runeRankings"], catalog.RuneRankings.Count);
        Assert.Equal(catalog.Manifest.Counts["staticStatues"], catalog.StaticStatues.Count);
        Assert.Equal(catalog.Manifest.Counts["newPlayerRewards"], catalog.NewPlayerRewards.Count);
        Assert.Equal(catalog.Manifest.Counts["votes"], catalog.Votes.Count);
        Assert.Equal(catalog.Manifest.Counts["couplesPkRecords"], catalog.CouplesPkRecords.Count);
        Assert.Equal(catalog.Manifest.Counts["socialAssociations"], catalog.SocialAssociations.Count);
        Assert.Equal(1, catalog.Manifest.Counts["guildWarInfo"]);
        Assert.Equal(1, catalog.Manifest.Counts["superGuildWarInfo"]);
        Assert.Equal(1, catalog.Manifest.Counts["stageGoalInfo"]);
        Assert.Equal(catalog.Manifest.Counts["stageGoalStages"], catalog.StageGoalStages.Count);
        Assert.Equal(catalog.Manifest.Counts["stageGoalTasks"], catalog.StageGoalTasks.Count);
        Assert.Equal(catalog.Manifest.Counts["shops"], catalog.Shops.Count);
        Assert.Equal(catalog.Manifest.Counts["shopItems"], catalog.ShopItems.Count);
        Assert.Equal(catalog.Manifest.Counts["exchangeShopGoods"], catalog.ExchangeShopGoods.Count);
        Assert.Equal(catalog.Manifest.Counts["exchangeShopGoodsEx"], catalog.ExchangeShopGoodsEx.Count);
        Assert.Equal(catalog.Manifest.Counts["exchangeShopLevels"], catalog.ExchangeShopLevels.Count);
        Assert.Equal(catalog.Manifest.Counts["honorShopItems"], catalog.HonorShopItems.Count);
        Assert.Equal(catalog.Manifest.Counts["honorShopRecommendations"], catalog.HonorShopRecommendations.Count);
        Assert.Equal(catalog.Manifest.Counts["racePointShopItems"], catalog.RacePointShopItems.Count);
        Assert.Equal(catalog.Manifest.Counts["racePointShopRecommendations"], catalog.RacePointShopRecommendations.Count);
        Assert.Equal(catalog.Manifest.Counts["goldenLeagueShopItems"], catalog.GoldenLeagueShopItems.Count);
        Assert.Equal(catalog.Manifest.Counts["goldenLeagueShopRecommendations"], catalog.GoldenLeagueShopRecommendations.Count);
        Assert.Equal(catalog.Manifest.Counts["goldenLeagueShopConfig"], catalog.GoldenLeagueShopConfig.Count);
        Assert.Equal(catalog.Manifest.Counts["booths"], catalog.Booths.Count);
        Assert.Equal(catalog.Manifest.Counts["boothItems"], catalog.BoothItems.Count);
        Assert.Equal(255, catalog.Maps.Count);
        Assert.Equal(23_424, catalog.ItemAddBonuses.Count);
        Assert.Equal(203, catalog.LevelExperience.Count);
        Assert.Equal(1_200, catalog.ClassLevelStats.Count);
        Assert.Equal(571, catalog.ProfessionLevelUps.Count);
        Assert.Equal(571, catalog.ProfessionLevelBenefits.Count);
        Assert.Equal(33, catalog.ProfessionTitleBenefits.Count);
        Assert.Equal(12, catalog.QuestTaskTypes.Count);
        Assert.Equal(1_472, catalog.QuestMissions.Count);
        Assert.Equal(1_517, catalog.QuizQuestions.Count);
        Assert.Equal(3_625, catalog.MagicEffects.Count);
        Assert.Equal(979, catalog.MagicTypeOperations.Count);
        Assert.Equal(270, catalog.TitleTypes.Count);
        Assert.Equal(642, catalog.CombatGear.Count);
        Assert.Equal(386, catalog.Traps.Count);
        Assert.Equal(287, catalog.MapNames.Count);
        Assert.Equal(1_114, catalog.SobNpcs.Count);
        Assert.Equal(36_533, catalog.MonsterSpawns.Count);
        Assert.All(catalog.MonsterSpawns, spawn => Assert.Equal("LoadMapMonsters", spawn.Attributes["legacy_source"]));
        Assert.Equal(31, catalog.LotteryDictionary.Count);
        Assert.Equal(23, catalog.CollectionRewardTiers.Count);
        Assert.Equal(261, catalog.TaskRewardTypes.Count);
        Assert.Equal(9, catalog.SyndicateLevels.Count);
        Assert.Equal(40, catalog.RuneLevelExperience.Count);
        Assert.Equal(21, catalog.RuneStorageAttributes.Count);
        Assert.Equal(648, catalog.ItemRefineEffects.Count);
        Assert.Equal(183, catalog.ItemRefineEffectEx.Count);
        Assert.Equal(702, catalog.ItemRefineUpgrades.Count);
        Assert.Equal(28, catalog.TransformInfo.Count);
        Assert.Equal(57, catalog.Furniture.Count);
        Assert.Equal(275, catalog.HairColorTypes.Count);
        Assert.Equal(477, catalog.HairFaceStorageTypes.Count);
        Assert.Equal(774, catalog.CoatStorageTypes.Count);
        Assert.Equal(157, catalog.CoatStorageAttributes.Count);
        Assert.Equal(8, catalog.CoatColorRules.Count);
        Assert.Equal(57, catalog.XuanbaoAdditionAttributes.Count);
        Assert.Equal(13, catalog.XuanbaoComposeAttributeLimits.Count);
        Assert.Equal(65, catalog.XuanbaoRandomAttributes.Count);
        Assert.Equal(17, catalog.ChristmasNpcs.Count);
        Assert.Equal(5, catalog.TutorTypes.Count);
        Assert.Equal(400, catalog.TutorBattleLimitTypes.Count);
        Assert.Equal(100, catalog.BeastAttributes.Count);
        Assert.Equal(396, catalog.FateExperience.Count);
        Assert.Equal(76, catalog.SpiritRates.Count);
        Assert.Equal(81, catalog.HundredWeapons.Count);
        Assert.Equal(315, catalog.GouyuTypes.Count);
        Assert.Equal(10, catalog.GouyuImmortals.Count);
        Assert.Equal(432, catalog.SpecialEffectAttributes.Count);
        Assert.Equal(326, catalog.DaoqiDictTypes.Count);
        Assert.Equal(187, catalog.SyndicateFormationTypes.Count);
        Assert.Equal(1_400, catalog.SyndicateSkillTypes.Count);
        Assert.Equal(801, catalog.DisdainRules.Count);
        Assert.Equal(90, catalog.JiangHuAttributes.Count);
        Assert.Equal(9, catalog.JiangHuCultivateStatuses.Count);
        Assert.Equal(185, catalog.RefineryBoxes.Count);
        Assert.Equal(214, catalog.RefinerySouls.Count);
        Assert.Equal(172, catalog.RefineryMaterials.Count);
        Assert.Equal(50, catalog.PokerTables.Count);
        Assert.Equal(20, catalog.RouletteTables.Count);
        Assert.Single(catalog.NpcTimeEntries);
        Assert.Equal(22, catalog.ActivityTasks.Tasks.Count);
        Assert.Equal(616, catalog.MeltingTypeRules.Count);
        Assert.Equal(367, catalog.LotteryPrizes.Count);
        Assert.Equal(16, catalog.ArenaRecords.Count);
        Assert.Equal(32, catalog.ElitePkRecords.Count);
        Assert.Equal(32, catalog.TeamElitePkRecords.Count);
        Assert.Equal(32, catalog.SkillTeamPkRecords.Count);
        Assert.Equal(44, catalog.ClassPkWarRecords.Count);
        Assert.Equal(7, catalog.CombatGearRanks.Count);
        Assert.Equal(8, catalog.PrestigeRanks.Count);
        Assert.Equal(20, catalog.InnerPowerRanks.Count);
        Assert.Equal(7, catalog.NewPlayerRewards.Count);
        Assert.Single(catalog.Votes);
        Assert.Single(catalog.CouplesPkRecords);
        Assert.NotEmpty(catalog.GuildWarInfo.Entries);
        Assert.NotEmpty(catalog.SuperGuildWarInfo.Entries);
        Assert.Equal(10, catalog.StageGoalStages.Count);
        Assert.Equal(78, catalog.StageGoalTasks.Count);
        Assert.Equal(620, catalog.Shops.Count);
        Assert.Equal(20_248, catalog.ShopItems.Count);
        Assert.Equal(66, catalog.HonorShopItems.Count);
        Assert.Equal(12, catalog.HonorShopRecommendations.Count);
        Assert.Equal(119, catalog.RacePointShopItems.Count);
        Assert.Equal(16, catalog.RacePointShopRecommendations.Count);
        Assert.Equal(23, catalog.GoldenLeagueShopItems.Count);
        Assert.Equal(22, catalog.GoldenLeagueShopRecommendations.Count);
        Assert.Equal(2, catalog.GoldenLeagueShopConfig.Count);
        Assert.Equal(16, catalog.Booths.Count);
        Assert.Equal(1_173, catalog.BoothItems.Count);
        Assert.DoesNotContain(catalog.Diagnostics, diagnostic => diagnostic.Code == "ManifestCountMismatch");
        Assert.Equal("8192", catalog.MapsById[1000].Attributes["TYPE"]);
        Assert.Equal("Desert", catalog.MapNamesByMapId[1000].Name);
        Assert.Equal("CentralPlain", catalog.MapNamesByMapId[1002].Name);
        Assert.Equal("Market", catalog.MapNamesByMapId[1036].Name);
        Assert.Equal("PKerJail", catalog.MapNamesByMapId[6000].Name);
        Assert.True(catalog.ItemAddBonusByItemTypeIdAndLevel.ContainsKey((111000, 9)));
        Assert.Equal(0, catalog.LevelExperienceByTypeAndLevel[(0, 140)].Experience);
        Assert.True(catalog.ClassLevelStatsByClassAndLevel.ContainsKey(("Thunderstriker", 120)));
        Assert.True(catalog.ProfessionLevelUpsByProfessionCode.ContainsKey(16051));
        Assert.True(catalog.ProfessionLevelBenefitsByProfessionCode.ContainsKey(16051));
        Assert.True(catalog.ProfessionTitleBenefitsByProfessionAndRank.ContainsKey((16, 3)));
        Assert.True(catalog.QuestMissionsByMissionId.ContainsKey(650));
        Assert.True(catalog.QuizQuestionsByQuestion.ContainsKey("In~which~channel~can~all~the~players~in~the~same~server~see~messages?"));
        Assert.True(catalog.MagicEffectsById.ContainsKey(5_002_142_000));
        Assert.True(catalog.MagicTypeOperationsById.ContainsKey(1));
        Assert.True(catalog.MagicTypeOperationsByReferencedMagicTypeId.ContainsKey(1360));
        Assert.True(catalog.TitleTypesById.ContainsKey(8044));
        Assert.True(catalog.CombatGearByGearTypeAndLevel.ContainsKey((703, 4)));
        Assert.True(catalog.TrapsByMapId.ContainsKey(44463));
        Assert.True(catalog.TrapsByTrapTypeId.ContainsKey(721262));
        Assert.True(catalog.SobNpcsById.ContainsKey(1021));
        Assert.True(catalog.LotteryDictionaryById.ContainsKey(1));
        Assert.Equal(50, catalog.CollectionRewardTierByCollectionIdAndMinValue[(700, 1)].RewardAmount);
        Assert.Equal(600, catalog.CollectionRewardTierByCollectionIdAndMinValue[(703, 3001)].RewardAmount);
        Assert.True(catalog.TaskRewardTypesById.ContainsKey(755));
        Assert.Equal([999, 2, 8, 10, 3, 3, 3, 1, 3, 5, 5], catalog.SyndicateLevelsByLevel[1].Parameters);
        Assert.Equal([999, 4, 12, 20, 15, 20, 20, 9, 20, 15, 15], catalog.SyndicateLevelsByLevel[9].Parameters);
        Assert.Equal(341_500, catalog.RuneLevelExperienceByRuneTypeAndLevel[(402, 27)].Experience);
        Assert.Equal(2, catalog.RuneStorageAttributesByRequiredStorageScore[335_000].Count);
        Assert.Equal(new RuneStorageAttributeBonus(11, 300), catalog.RuneStorageAttributesById[21].Bonuses[0]);
        Assert.Equal(3000, catalog.TransformInfoByTransformIdAndLevel[(1350, 3)].MaxLife);
        Assert.Equal(8, catalog.TransformInfoByTransformId[1270].Count);
        Assert.Equal("WaterElf", catalog.TransformInfoByName["WaterElf"].Single(entry => entry.Level == 8).Name);
        Assert.Equal(23_000, catalog.FurnitureById[8924].Price);
        Assert.Equal(2, catalog.FurnitureByItemTypeId[726086].Count);
        Assert.Equal(57, catalog.FurnitureByMapId[1511].Count);
        Assert.Equal("FlowerDressingTable", catalog.FurnitureByName["FlowerDressingTable"].Single().Name);
        Assert.True(catalog.HairColorTypeByHairColorIdAndColorIndex.ContainsKey((78, 5)));
        Assert.True(catalog.HairFaceStorageTypesByName.ContainsKey("YouthSpark"));
        Assert.True(catalog.CoatStorageTypesByItemTypeId.ContainsKey(181305));
        Assert.True(catalog.CoatStorageAttributesById.ContainsKey(17));
        Assert.True(catalog.CoatColorRulesByItemTypeId.ContainsKey(200412));
        Assert.True(catalog.XuanbaoAdditionAttributeByAttributeTypeAndLevel.ContainsKey((14, 1)));
        Assert.True(catalog.XuanbaoRandomAttributesByItemTypeId.ContainsKey(4100001));
        Assert.True(catalog.TutorTypesById.ContainsKey(1));
        Assert.True(catalog.TutorBattleLimitTypesById.ContainsKey(1));
        Assert.True(catalog.BeastAttributesByLevel.ContainsKey(100));
        Assert.True(catalog.FateExperienceByTypeAndLevel.ContainsKey((4, 99)));
        Assert.True(catalog.SpiritRatesByItemTypeId.ContainsKey(4200001));
        Assert.True(catalog.HundredWeaponsById.ContainsKey(81));
        Assert.True(catalog.GouyuTypesById.ContainsKey(315));
        Assert.True(catalog.GouyuImmortalsById.ContainsKey(17));
        Assert.True(catalog.SpecialEffectAttributesById.ContainsKey(40_708_186));
        Assert.True(catalog.DaoqiDictTypesById.ContainsKey(26_907));
        Assert.True(catalog.SyndicateFormationTypesById.ContainsKey(187));
        Assert.True(catalog.SyndicateSkillTypesBySkillTypeAndLevel.ContainsKey((14, 100)));
        Assert.True(catalog.DisdainRulesById.ContainsKey(801));
        Assert.True(catalog.JiangHuAttributesByTypeAndLevel.ContainsKey((9, 6)));
        Assert.True(catalog.JiangHuCultivateStatusesById.ContainsKey(9));
        Assert.True(catalog.RefineryBoxesByItemType.ContainsKey(3004280));
        Assert.True(catalog.RefinerySoulsByLevel.ContainsKey(7));
        Assert.True(catalog.RefineryMaterialsByItemType.ContainsKey(724350));
        Assert.True(catalog.PokerTablesById.ContainsKey(102230));
        Assert.True(catalog.RouletteTablesById.ContainsKey(102095));
        Assert.True(catalog.NpcTimeEntriesById.ContainsKey(78156));
        Assert.True(catalog.ActivityTasksBySectionId.ContainsKey(22));
        Assert.True(catalog.ActivityTasksById.ContainsKey(20));
        Assert.True(catalog.ActivityTasksByType.ContainsKey(7));
        Assert.True(catalog.ActivityTasksByTitle.ContainsKey("Champion Points"));
        Assert.True(catalog.MeltingTypeRulesBySourceFile.ContainsKey("MeltingTYpe_3.ini"));
        Assert.True(catalog.MeltingTypeRulesByItemTypeId.ContainsKey(700002));
        Assert.True(catalog.MeltingTypeRulesByRuleType.ContainsKey(2));
        Assert.True(catalog.LotteryPrizesByItemTypeId.ContainsKey(151159));
        Assert.True(catalog.LotteryPrizesByItemName.ContainsKey("WavyHeavyRing+8"));
        Assert.True(catalog.LotteryPrizesByLevel.ContainsKey(70));
        Assert.True(catalog.ArenaRecordsByPlayerId.ContainsKey(1296459));
        Assert.True(catalog.ArenaRecordsByRank.ContainsKey(1));
        Assert.True(catalog.TeamArenaRecordsByPlayerId.ContainsKey(1293600));
        Assert.True(catalog.TeamArenaRecordsByRank.ContainsKey(0));
        Assert.True(catalog.ElitePkRecordsByGroupAndSlot.ContainsKey((3, 7)));
        Assert.True(catalog.TeamElitePkRecordsByGroupAndSlot.ContainsKey((3, 7)));
        Assert.True(catalog.SkillTeamPkRecordsByGroupAndSlot.ContainsKey((3, 7)));
        Assert.True(catalog.ClassPkWarRecordsByClassAndSlot.ContainsKey((10, 3)));
        Assert.True(catalog.CombatGearRanksByCharacterId.ContainsKey(1293600));
        Assert.True(catalog.PrestigeRanksByCharacterId.ContainsKey(1293600));
        Assert.True(catalog.InnerPowerRanksByCharacterId.ContainsKey(1296459));
        Assert.True(catalog.NewPlayerRewardsByIpAddress.ContainsKey("198.51.100.12"));
        Assert.True(catalog.VotesByCharacterId.ContainsKey(1296463));
        Assert.True(catalog.CouplesPkRecordsByPlayerName.ContainsKey("The_ScorpiooN"));
        Assert.Equal(0, catalog.GuildWarInfo.Info?.Id);
        Assert.Equal(1204, catalog.SuperGuildWarInfo.Info?.Id);
        Assert.True(catalog.StageGoalStagesById.ContainsKey(1));
        Assert.True(catalog.StageGoalTasksByStageAndGoalId.ContainsKey((1, 1)));
        Assert.True(catalog.ShopsById.ContainsKey(432));
        Assert.True(catalog.HonorShopItemsByItemTypeId.ContainsKey(3303085));
        Assert.True(catalog.RacePointShopRecommendationsByItemTypeId.Count > 0);
        Assert.True(catalog.GoldenLeagueShopRecommendationsByItemTypeId.Count > 0);
        Assert.True(catalog.BoothsById.ContainsKey(700050));
        Assert.Equal(catalog.MonsterSpawns.Count, catalog.LegacyMonsterRuntimeUidBases.Count);
        var maxMonsterRuntimeUid = catalog.MonsterSpawns
            .Select(spawn =>
                catalog.GetLegacyMonsterRuntimeUidBase(spawn) +
                checked((uint)Math.Max(1, Math.Max(spawn.MaxNpc, spawn.MaxPerGenerator))) -
                1U)
            .Max();
        Assert.Equal(436_532U, maxMonsterRuntimeUid);
        Assert.InRange(maxMonsterRuntimeUid, 400_000U, 999_999U);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private void WriteFixture(
        IReadOnlyDictionary<string, int>? countOverrides = null,
        IReadOnlyList<MapName>? mapNamesOverride = null,
        IReadOnlyList<LegacyGameMapEntry>? legacyGameMapsOverride = null,
        IReadOnlyList<Npc>? npcsOverride = null,
        IReadOnlyList<SobNpc>? sobNpcsOverride = null,
        IReadOnlyList<MonsterSpawn>? monsterSpawnsOverride = null,
        IReadOnlyList<QuestMission>? questMissionsOverride = null,
        IReadOnlyList<Monster>? monstersOverride = null)
    {
        Directory.CreateDirectory(_rootPath);

        var clientConfigs = new[]
        {
            new ClientConfigRecord(
                2,
                "TestSrv",
                3935,
                "0222",
                222,
                "0214",
                214,
                "0001",
                1,
                "0000",
                0,
                1,
                3,
                "127.0.0.1",
                9220,
                "client_config.ini",
                1,
                "2 TestSrv 3935 0222 0214 0001 0000 1 3 127.0.0.1 9220",
                ["2", "TestSrv", "3935", "0222", "0214", "0001", "0000", "1", "3", "127.0.0.1", "9220"]),
            new ClientConfigRecord(
                101,
                "Realm1",
                3935,
                "0328",
                328,
                "0198",
                198,
                "0003",
                3,
                "0003",
                3,
                1,
                0,
                "198.51.100.16",
                9221,
                "client_config.ini",
                2,
                "101 Realm1 3935 0328 0198 0003 0003 1 0 198.51.100.16 9221",
                ["101", "Realm1", "3935", "0328", "0198", "0003", "0003", "1", "0", "198.51.100.16", "9221"]),
        };
        var items = new[]
        {
            new ItemType(205003, "DuplicateItemA", ["205003", "DuplicateItemA"]),
            new ItemType(205003, "DuplicateItemB", ["205003", "DuplicateItemB"]),
        };
        var itemTypeSubs = new[]
        {
            new ItemTypeSub(
                205003,
                "ItemtypeSub.txt",
                LineNumber: 1,
                RawLine: "205003@@1@@DuplicateSubA@@",
                Fields: ["205003", "1", "DuplicateSubA", ""]),
            new ItemTypeSub(
                205003,
                "ItemtypeSub.txt",
                LineNumber: 2,
                RawLine: "205003@@2@@DuplicateSubB@@",
                Fields: ["205003", "2", "DuplicateSubB", ""]),
        };
        var itemAddBonuses = new[]
        {
            new ItemAddBonus(205003, 1, [0, 0, 0, 2, 0, 1, 0, 0], SourceLine: 1, RawLine: "205003 1 0 0 0 2 0 1 0 0"),
            new ItemAddBonus(205003, 2, [0, 0, 0, 4, 0, 2, 0, 0], SourceLine: 2, RawLine: "205003 2 0 0 0 4 0 2 0 0"),
        };
        var levelExperience = new[]
        {
            new LevelExperience(0, 139, 2_641_561_501_496, 3_925_301, 600, SourceLine: 1, RawLine: "0 139 2641561501496 3925301 600", Fields: ["0", "139", "2641561501496", "3925301", "600"]),
            new LevelExperience(0, 139, 2_641_561_501_496, 3_925_301, 600, SourceLine: 2, RawLine: "0 139 2641561501496 3925301 600", Fields: ["0", "139", "2641561501496", "3925301", "600"]),
            new LevelExperience(1, 9, 0, 0, 0, SourceLine: 3, RawLine: "1 9 0 0 0", Fields: ["1", "9", "0", "0", "0"]),
        };
        var classLevelStats = new[]
        {
            new ClassLevelStat(
                "WindWalker",
                Level: 1,
                Strength: 5,
                Agility: 3,
                Vitality: 2,
                Spirit: 0,
                SourceFile: "Stats.ini",
                LineNumber: 2,
                RawKey: "WindWalker[1]",
                RawValue: "5,3,2,0",
                RawLine: "WindWalker[1]=5,3,2,0",
                Fields: ["5", "3", "2", "0"]),
            new ClassLevelStat(
                "Thunderstriker",
                Level: 120,
                Strength: 91,
                Agility: 132,
                Vitality: 144,
                Spirit: 0,
                SourceFile: "Stats.ini",
                LineNumber: 1201,
                RawKey: "Thunderstriker[120]",
                RawValue: "91,144,132,0",
                RawLine: "Thunderstriker[120]=91,144,132,0",
                Fields: ["91", "144", "132", "0"]),
        };
        var professionLevelUps = new[]
        {
            new ProfessionLevelUp(
                571,
                16051,
                RequiredLevel: 120,
                RequiredMetempsychosis: 0,
                RequiredMoney: 920_000,
                RequiredProficiency: 800_000,
                SourceFile: "prof_lev_up.txt",
                LineNumber: 571,
                RawLine: "571@@16051@@120@@0@@920000@@800000@@",
                Fields: ["571", "16051", "120", "0", "920000", "800000", ""]),
        };
        var professionLevelBenefits = new[]
        {
            new ProfessionLevelBenefit(
                571,
                16051,
                Rank: 5,
                BonusValues: [62600, 71200, 0, 0, 2600],
                SourceFile: "prof_lev_benefit.txt",
                LineNumber: 571,
                RawLine: "571@@16051@@5@@62600@@71200@@0@@0@@2600@@",
                Fields: ["571", "16051", "5", "62600", "71200", "0", "0", "2600", ""]),
        };
        var professionTitleBenefits = new[]
        {
            new ProfessionTitleBenefit(
                33,
                16,
                Rank: 3,
                BonusValues: [60500, 70100, 100100, 120100, 0],
                SourceFile: "prof_title_benefit.txt",
                LineNumber: 33,
                RawLine: "33@@16@@3@@60500@@70100@@100100@@120100@@0@@",
                Fields: ["33", "16", "3", "60500", "70100", "100100", "120100", "0", ""]),
        };
        var questInfo = new QuestInfoMetadata(20_000_000, 300, 2);
        var questTaskTypes = new[]
        {
            new QuestTaskType(1, "Recruit Quest", "0xFFCFC058", 1, 1, "TypeId1", "TypeName1"),
        };
        var questMissions = questMissionsOverride?.ToArray() ?? new[]
        {
            QuestMission(1, 650, 1000, [205003], [608, 9999], intentAmount: 2),
            QuestMission(2, 650, 9999, [999999], [], intentAmount: 0),
        };
        var quizQuestions = new[]
        {
            new QuizQuestionEntry(
                1,
                "In~which~channel~can~all~the~players~in~the~same~server~see~messages?",
                ["Team", "Broadcast", "Guild", "Talk"],
                CorrectOption: 2,
                CorrectAnswer: "Broadcast",
                SourceFile: "quizquestins.ini",
                LineNumber: 1,
                RawLine: "In~which~channel~can~all~the~players~in~the~same~server~see~messages?#Team#Broadcast#Guild#Talk#2",
                Fields: ["In~which~channel~can~all~the~players~in~the~same~server~see~messages?", "Team", "Broadcast", "Guild", "Talk", "2"]),
            new QuizQuestionEntry(
                2,
                "In~which~channel~can~all~the~players~in~the~same~server~see~messages?",
                ["Talk", "Whisper", "Guild", "Team"],
                CorrectOption: 3,
                CorrectAnswer: "Guild",
                SourceFile: "quizquestins.ini",
                LineNumber: 2,
                RawLine: "In~which~channel~can~all~the~players~in~the~same~server~see~messages?#Talk#Whisper#Guild#Team#3",
                Fields: ["In~which~channel~can~all~the~players~in~the~same~server~see~messages?", "Talk", "Whisper", "Guild", "Team", "3"]),
        };
        var magic = new[]
        {
            new MagicType(1, 100, "Thunder", ["1", "100", "Thunder"]),
            new MagicType(2, 1350, "DivineHare", ["2", "1350", "DivineHare"]),
            new MagicType(3, 1270, "Golem", ["3", "1270", "Golem"]),
        };
        var magicEffects = new[]
        {
            new MagicEffect(
                5_002_142_000,
                Occurrence: 1,
                Name: "TenDigit",
                Description: "",
                DescriptionEx: "Long~id",
                SourceFile: "MagicEffect.ini",
                SourceLine: 1,
                SourceSection: "5002142000",
                Entries:
                [
                    new MagicEffectEntry("Status", "5", "MagicEffect.ini", 2, "Status=5"),
                    new MagicEffectEntry("Status", "145", "MagicEffect.ini", 3, "Status=145"),
                    new MagicEffectEntry("Desc", "", "MagicEffect.ini", 4, "Desc="),
                    new MagicEffectEntry("DescEx", "Long~id", "MagicEffect.ini", 5, "DescEx=Long~id"),
                ],
                Attributes: new Dictionary<string, string>
                {
                    ["Status"] = "145",
                    ["Desc"] = "",
                    ["DescEx"] = "Long~id",
                }),
            new MagicEffect(
                5_002_142_000,
                Occurrence: 2,
                Name: null,
                Description: "Second",
                DescriptionEx: "Second~effect",
                SourceFile: "MagicEffect.ini",
                SourceLine: 7,
                SourceSection: "5002142000",
                Entries:
                [
                    new MagicEffectEntry("Desc", "Second", "MagicEffect.ini", 8, "Desc=Second"),
                    new MagicEffectEntry("DescEx", "Second~effect", "MagicEffect.ini", 9, "DescEx=Second~effect"),
                ],
                Attributes: new Dictionary<string, string>
                {
                    ["Desc"] = "Second",
                    ["DescEx"] = "Second~effect",
                }),
        };
        var magicTypeOperations = new[]
        {
            new MagicTypeOperationEntry(
                1,
                Mode: 1,
                Group: 1,
                Level: 1,
                Flag: 2,
                ReferencedMagicTypeIds: [100, 1360, 9999],
                Values: [1, 1, 1, 1, 2, 100, 1360, 9999, 0],
                SourceFile: "magictypeop.txt",
                LineNumber: 1,
                RawLine: "1,1,1,1,2,100,1360,9999,0",
                Fields: ["1", "1", "1", "1", "2", "100", "1360", "9999", "0"]),
            new MagicTypeOperationEntry(
                2,
                Mode: 1,
                Group: 1,
                Level: 2,
                Flag: 0,
                ReferencedMagicTypeIds: [1360],
                Values: [2, 1, 1, 2, 0, 1360, 0, 0, 0],
                SourceFile: "magictypeop.txt",
                LineNumber: 2,
                RawLine: "2,1,1,2,0,1360,0,0,0",
                Fields: ["2", "1", "1", "2", "0", "1360", "0", "0", "0"]),
        };
        var titleTypes = new[]
        {
            new TitleType(
                9,
                TypeId: 1,
                Name: "Chosen~One",
                Unknown0: 0,
                Unknown1: 0,
                Unknown2: 0,
                Unknown3: 0,
                Score: 500,
                SourceFile: "title_type.txt",
                LineNumber: 2,
                RawLine: "9@@1@@Chosen~One@@0@@0@@0@@0@@500@@",
                Fields: ["9", "1", "Chosen~One", "0", "0", "0", "0", "500", ""]),
            new TitleType(
                8043,
                TypeId: 8043,
                Name: "CS Team PK Champion Halo",
                Unknown0: 0,
                Unknown1: 0,
                Unknown2: 0,
                Unknown3: 0,
                Score: 400,
                SourceFile: "title_type.txt",
                LineNumber: 269,
                RawLine: "8043@@8043@@CS Team PK Champion Halo@@0@@0@@0@@0@@400@@",
                Fields: ["8043", "8043", "CS Team PK Champion Halo", "0", "0", "0", "0", "400", ""]),
        };
        var combatGear = new[]
        {
            new CombatGear(
                1,
                GearTypeId: 201,
                Level: 1,
                UpExperience: 300,
                Life: 60,
                Attack: 30,
                Defense: 0,
                UnknownStat0: 0,
                MagicDefense: 0,
                MagicTypeIds: [17640000, 0, 0, 17650000, 0, 0, 0, 0, 0],
                Score: 240,
                Unknown0: 0,
                Unknown1: 0,
                Unknown2: 0,
                SourceFile: "combat_gear.txt",
                LineNumber: 1,
                RawLine: "1@@201@@1@@300@@60@@30@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@240@@0@@0@@0@@",
                Fields: ["1", "201", "1", "300", "60", "30", "0", "0", "0", "17640000", "0", "0", "17650000", "0", "0", "0", "0", "0", "240", "0", "0", "0", ""]),
            new CombatGear(
                2,
                GearTypeId: 201,
                Level: 2,
                UpExperience: 1200,
                Life: 120,
                Attack: 60,
                Defense: 0,
                UnknownStat0: 0,
                MagicDefense: 0,
                MagicTypeIds: [17640000, 0, 0, 17650000, 0, 0, 0, 0, 0],
                Score: 480,
                Unknown0: 0,
                Unknown1: 0,
                Unknown2: 0,
                SourceFile: "combat_gear.txt",
                LineNumber: 2,
                RawLine: "2@@201@@2@@1200@@120@@60@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@480@@0@@0@@0@@",
                Fields: ["2", "201", "2", "1200", "120", "60", "0", "0", "0", "17640000", "0", "0", "17650000", "0", "0", "0", "0", "0", "480", "0", "0", "0", ""]),
        };
        var traps = new[]
        {
            new Trap(
                44463,
                X: 77,
                Y: 190,
                TrapTypeId: 1618,
                OptionalFlag: 1,
                SourceFile: "Traps.txt",
                LineNumber: 1,
                RawLine: "44463,77,190,1618,1",
                Fields: ["44463", "77", "190", "1618", "1"]),
            new Trap(
                44463,
                X: 77,
                Y: 190,
                TrapTypeId: 24,
                OptionalFlag: null,
                SourceFile: "Traps.txt",
                LineNumber: 93,
                RawLine: "44463,77,190,24",
                Fields: ["44463", "77", "190", "24"]),
        };
        var npcs = new[]
        {
            new Npc(200, 2, 51700, 1000, 10, 20, null),
            new Npc(200, 2, 51700, 9999, 11, 21, null),
        };
        if (npcsOverride is not null)
        {
            npcs = npcsOverride.ToArray();
        }

        var sobNpcs = new[]
        {
            new SobNpc(25005, "GateA", 21, 0, 1000, 85, 70, 0, 0, 1, 0, "Gate A", 1, "25005,GateA,21,0,1000,85,70,0,0,1,0,Gate A", ["25005", "GateA", "21", "0", "1000", "85", "70", "0", "0", "1", "0", "Gate A"]),
            new SobNpc(25005, "GateB", 21, 0, 9999, 85, 70, 0, 0, 1, 0, null, 2, "25005,GateB,21,0,9999,85,70,0,0,1,0", ["25005", "GateB", "21", "0", "9999", "85", "70", "0", "0", "1", "0"]),
        };
        if (sobNpcsOverride is not null)
        {
            sobNpcs = sobNpcsOverride.ToArray();
        }

        var portals = new[]
        {
            new Portal(1000, 50, 50, 9999, 60, 60),
        };
        var maps = new[]
        {
            new MapMetadata(
                1000,
                "1000.ini",
                "TwinCity",
                1000,
                "12",
                12,
                1,
                2,
                1000,
                0,
                0,
                0,
                0,
                0,
                "4294967295",
                4_294_967_295,
                new Dictionary<string, string> { ["Type"] = "12" }),
        };
        var mapNames = new[]
        {
            new MapName(
                1000,
                "Desert",
                SourceFile: "GameMapEx.ini",
                SectionLine: 1,
                NameLine: 2,
                SourceSection: "1000",
                RawNameLine: "Name=Desert"),
            new MapName(
                3845,
                "VIP Resort",
                SourceFile: "GameMapEx.ini",
                SectionLine: 4,
                NameLine: 5,
                SourceSection: "3845",
                RawNameLine: "Name=VIP Resort"),
            new MapName(
                1000,
                "Twin City Alias",
                SourceFile: "GameMapEx.ini",
                SectionLine: 7,
                NameLine: 8,
                SourceSection: "1000",
                RawNameLine: "Name=Twin City Alias"),
        };
        if (mapNamesOverride is not null)
        {
            mapNames = mapNamesOverride.ToArray();
        }

        var legacyGameMaps = legacyGameMapsOverride?.ToArray() ?? [];
        var monsters = new[]
        {
            new Monster(
                10,
                "Pheasant",
                new Dictionary<string, string> { ["Level"] = "7" },
                [
                    new SpecialDrop(999999, 4),
                ]),
        };
        if (monstersOverride is not null)
        {
            monsters = monstersOverride.ToArray();
        }

        var monsterSpawns = new[]
        {
            Spawn(1, 1000, 10),
            Spawn(1, 9999, 9999),
        };
        if (monsterSpawnsOverride is not null)
        {
            monsterSpawns = monsterSpawnsOverride.ToArray();
        }

        var lotteryDictionary = new[]
        {
            new LotteryDictionaryEntry(
                17,
                Type: 2,
                SubType: 3,
                Value: 18007,
                Unknown0: 0,
                Weight: 2,
                SourceFile: "dict_lottery.txt",
                LineNumber: 17,
                RawLine: "17@@2@@3@@18007@@0@@2@@",
                Fields: ["17", "2", "3", "18007", "0", "2", ""]),
            new LotteryDictionaryEntry(
                18,
                Type: 2,
                SubType: 3,
                Value: 19007,
                Unknown0: 0,
                Weight: 2,
                SourceFile: "dict_lottery.txt",
                LineNumber: 18,
                RawLine: "18@@2@@3@@19007@@0@@2@@",
                Fields: ["18", "2", "3", "19007", "0", "2", ""]),
        };
        var collectionRewardTiers = new[]
        {
            new CollectionRewardTier(
                11,
                CollectionId: 700,
                MinInclusive: 8400,
                MaxInclusive: 2_147_483_647,
                RewardType: 1,
                RewardValue: 1500,
                RewardAmount: 600,
                SourceFile: "dict_collection.txt",
                LineNumber: 11,
                RawLine: "11@@700@@8400@@2147483647@@1@@1500@@600@@",
                Fields: ["11", "700", "8400", "2147483647", "1", "1500", "600", ""]),
            new CollectionRewardTier(
                12,
                CollectionId: 700,
                MinInclusive: 1,
                MaxInclusive: 1000,
                RewardType: 1,
                RewardValue: 100,
                RewardAmount: 50,
                SourceFile: "dict_collection.txt",
                LineNumber: 12,
                RawLine: "12@@700@@1@@1000@@1@@100@@50@@",
                Fields: ["12", "700", "1", "1000", "1", "100", "50", ""]),
            new CollectionRewardTier(
                11,
                CollectionId: 700,
                MinInclusive: 8400,
                MaxInclusive: 2_147_483_647,
                RewardType: 1,
                RewardValue: 1500,
                RewardAmount: 700,
                SourceFile: "dict_collection.txt",
                LineNumber: 13,
                RawLine: "11@@700@@8400@@2147483647@@1@@1500@@700@@",
                Fields: ["11", "700", "8400", "2147483647", "1", "1500", "700", ""]),
        };
        var taskRewardTypes = new[]
        {
            new TaskRewardTypeEntry(
                755,
                Parameters: [2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                RewardValues: [3301408, 3301409, 3308803, 3301410, 3301411, 3301412, 3301413, 3301414],
                RewardWeights: [3000, 3000, 3000, 600, 280, 100, 10, 10],
                Extra0: 0,
                Extra1: 0,
                SourceFile: "task_reward_type.txt",
                LineNumber: 1,
                RawLine: "755@@2@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@3301408@@3301409@@3308803@@3301410@@3301411@@3301412@@3301413@@3301414@@3000@@3000@@3000@@600@@280@@100@@10@@10@@0@@0@@",
                Fields: ["755", "2", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "3301408", "3301409", "3308803", "3301410", "3301411", "3301412", "3301413", "3301414", "3000", "3000", "3000", "600", "280", "100", "10", "10", "0", "0", ""]),
        };
        var taskRewardTypeIniEntries = new[]
        {
            new TaskRewardTypeIniEntry(
                3651,
                Parameters: [2, 1, 2, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0],
                RewardValues: [3007285, 3007284, 3007386, 3008731, 3007311, 3008729, 3600012, 3008732],
                RewardWeights: [1000, 1000, 1000, 1000, 1500, 1500, 1500, 1500],
                Extra0: 98_579_160,
                Extra1: 98_579_161,
                SourceFile: "task_reward_type.ini",
                LineNumber: 2,
                RawLine: "3651 2 1 0002 0000 0002 0 0 0 0 0 0 0 0 3007285 3007284 3007386 3008731 3007311 3008729 3600012 3008732 1000 1000 1000 1000 1500 1500 1500 1500 98579160 98579161",
                Fields: ["3651", "2", "1", "0002", "0000", "0002", "0", "0", "0", "0", "0", "0", "0", "0", "3007285", "3007284", "3007386", "3008731", "3007311", "3008729", "3600012", "3008732", "1000", "1000", "1000", "1000", "1500", "1500", "1500", "1500", "98579160", "98579161"]),
        };
        var neiGongHeader = new NeiGongHeader(
            SecretCount: 1,
            AttributeTips:
            [
                new NeiGongAttributeTip(0, "Potency~Cost"),
                new NeiGongAttributeTip(14, "Anti-break"),
            ],
            SourceFile: "NeiGongInfo.ini",
            LineNumber: 1,
            RawSectionName: "NeiGong",
            Fields: new Dictionary<string, string>
            {
                ["Num"] = "1",
                ["Attri0Tip"] = "Potency~Cost",
                ["Attri14Tip"] = "Anti-break",
            });
        var neiGongSecrets = new[]
        {
            new NeiGongSecret(
                1,
                Name: "Universal~Concept",
                AniSection: "NeiGong_HeartSkill14Btn",
                NeiGongCount: 1,
                AttributeTypes: [7, 9],
                AttributeValues: [100, 100],
                GetWayContent: "PS:~Claim~the~book~[Universal~Concept]~from~the~Celestial~Sage~at~(TwinCity~202,416).",
                SourceFile: "NeiGongInfo.ini",
                LineNumber: 6,
                RawSectionName: "1",
                Fields: new Dictionary<string, string>
                {
                    ["id"] = "1",
                    ["Name"] = "Universal~Concept",
                    ["AniSection"] = "NeiGong_HeartSkill14Btn",
                    ["NeiGongNum"] = "1",
                    ["AttriType"] = "7-9",
                    ["AttriValue"] = "100-100",
                    ["GetWayContent"] = "PS:~Claim~the~book~[Universal~Concept]~from~the~Celestial~Sage~at~(TwinCity~202,416).",
                }),
        };
        var neiGongTypes = new[]
        {
            new NeiGongType(
                SecretId: 1,
                Slot: 1,
                TypeId: 1,
                Name: "Universal~Concept~(A)",
                AniSection: "NeiGong_HeartSkill14Btn",
                MaxLevel: 5,
                AttributeTypes: [2, 4],
                AttributeValues: [100, 100],
                CultureValues: [1500, 1500, 1500, 1500, 1500],
                Condition: "<item 3005365>[Universal~Concept~(A)]</item>",
                RequiredLevel: 2015,
                RequiredNeiGongValue: 0,
                RequiredPrerequisiteNeiGong: 0,
                RequiredItemTypeId: 3_005_365,
                SourceFile: "NeiGongInfo.ini",
                LineNumber: 16,
                RawSectionName: "1-1",
                Fields: new Dictionary<string, string>
                {
                    ["Type"] = "1",
                    ["Name"] = "Universal~Concept~(A)",
                    ["AniSection"] = "NeiGong_HeartSkill14Btn",
                    ["MaxLev"] = "5",
                    ["AttriType"] = "2-4",
                    ["AttriValue"] = "100-100",
                    ["NeiGongValue"] = "1500-1500-1500-1500-1500",
                    ["Condition"] = "<item 3005365>[Universal~Concept~(A)]</item>",
                    ["ReqLev"] = "2015",
                    ["ReqNeiGongValue"] = "0",
                    ["ReqPreNeiGong"] = "0",
                    ["ReqItemType"] = "3005365",
                }),
        };
        var subProfessions = new[]
        {
            new SubProfessionEntry(
                Id: 11,
                Title: "Martial Artist",
                Intro: "Critical Strike +1%",
                RequiredLevel: 70,
                RequiredMetempsychosis: 0,
                RequiredProfessions: [41, 31],
                Promotion: "or 800 StudyPoints",
                Suit: 0,
                LevelLimit: 9,
                RequiredJobLevel: 1,
                EffectProportion: 1,
                UpLevelEffect: "LevelUp",
                UpStepEffect: "LevelUp",
                UpStepScreenEffect: null,
                UpEffect: null,
                SubSpecial: "None",
                SuitPower: null,
                BigClass: null,
                AccessionRoute: null,
                StepUpRoute: null,
                AnimationSection: null,
                SourceFile: "SubProfessionInfo.ini",
                LineNumber: 54,
                RawSectionName: "11",
                Entries:
                [
                    new SubProfessionRawEntry("req_joblv", "0", "SubProfessionInfo.ini", 61, "req_joblv=0"),
                    new SubProfessionRawEntry("req_joblv", "1", "SubProfessionInfo.ini", 64, "req_joblv=1"),
                ]),
        };
        var syndicateLevels = new[]
        {
            new SyndicateLevel(
                1,
                Parameters: [999, 2, 8, 10, 3, 3, 3, 1, 3, 5, 5],
                SourceFile: "syndicate_level.txt",
                LineNumber: 1,
                RawLine: "1@@999@@2@@8@@10@@3@@3@@3@@1@@3@@5@@5@@",
                Fields: ["1", "999", "2", "8", "10", "3", "3", "3", "1", "3", "5", "5", ""]),
        };
        var runeLevelExperience = new[]
        {
            new RuneLevelExperience(
                28,
                RuneType: 402,
                Level: 27,
                Experience: 341_500,
                SourceFile: "rune_levexp.txt",
                LineNumber: 28,
                RawLine: "28@@402@@27@@341500@@",
                Fields: ["28", "402", "27", "341500", ""]),
        };
        var runeStorageAttributes = new[]
        {
            new RuneStorageAttribute(
                20,
                RequiredStorageScore: 335_000,
                Bonuses:
                [
                    new RuneStorageAttributeBonus(1, 1300),
                    new RuneStorageAttributeBonus(2, 900),
                    new RuneStorageAttributeBonus(10, 300),
                ],
                SourceFile: "rune_storage_attr.txt",
                LineNumber: 20,
                RawLine: "20@@335000@@1@@1300@@2@@900@@10@@300@@",
                Fields: ["20", "335000", "1", "1300", "2", "900", "10", "300", ""]),
            new RuneStorageAttribute(
                21,
                RequiredStorageScore: 335_000,
                Bonuses:
                [
                    new RuneStorageAttributeBonus(11, 300),
                    new RuneStorageAttributeBonus(12, 300),
                    new RuneStorageAttributeBonus(0, 0),
                ],
                SourceFile: "rune_storage_attr.txt",
                LineNumber: 21,
                RawLine: "21@@335000@@11@@300@@12@@300@@0@@0@@",
                Fields: ["21", "335000", "11", "300", "12", "300", "0", "0", ""]),
        };
        var itemRefineEffects = new[]
        {
            new ItemRefineEffect(
                648,
                Values: [6480, 6480, 8080, 2020, 1010, 2007, 3007, 4008, 5005, 6005, 7008, 8008, 9005, 10005, 11008, 12008, 13008, 14006, 15006, 16002],
                SourceFile: "item_refine_effect.txt",
                LineNumber: 648,
                RawLine: "648@@6480@@6480@@8080@@2020@@1010@@2007@@3007@@4008@@5005@@6005@@7008@@8008@@9005@@10005@@11008@@12008@@13008@@14006@@15006@@16002@@",
                Fields: ["648", "6480", "6480", "8080", "2020", "1010", "2007", "3007", "4008", "5005", "6005", "7008", "8008", "9005", "10005", "11008", "12008", "13008", "14006", "15006", "16002", ""]),
            new ItemRefineEffect(
                648,
                Values: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1],
                SourceFile: "item_refine_effect.txt",
                LineNumber: 649,
                RawLine: "648@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@",
                Fields: ["648", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "1", ""]),
            new ItemRefineEffect(
                12,
                Values: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20],
                SourceFile: "item_refine_effect.txt",
                LineNumber: 12,
                RawLine: "12@@1@@2@@3@@4@@5@@6@@7@@8@@9@@10@@11@@12@@13@@14@@15@@16@@17@@18@@19@@20@@",
                Fields: ["12", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20", ""]),
        };
        var itemRefineEffectEx = new[]
        {
            new ItemRefineEffectEx(
                1,
                ItemTypeId: 2_100_095,
                Values: [100, 5, 7],
                SourceFile: "item_refine_effect_ex.txt",
                LineNumber: 1,
                RawLine: "1@@2100095@@100@@5@@7@@",
                Fields: ["1", "2100095", "100", "5", "7", ""]),
            new ItemRefineEffectEx(
                1,
                ItemTypeId: 2_100_095,
                Values: [300, 19, 300],
                SourceFile: "item_refine_effect_ex.txt",
                LineNumber: 2,
                RawLine: "1@@2100095@@300@@19@@300@@",
                Fields: ["1", "2100095", "300", "19", "300", ""]),
            new ItemRefineEffectEx(
                2,
                ItemTypeId: 2_169_345,
                Values: [150, 9, 1],
                SourceFile: "item_refine_effect_ex.txt",
                LineNumber: 183,
                RawLine: "2@@2169345@@150@@9@@1@@",
                Fields: ["2", "2169345", "150", "9", "1", ""]),
        };
        var itemRefineUpgrades = new[]
        {
            new ItemRefineUpgrade(
                1,
                Level: 54,
                Value: 12_000,
                SourceFile: "item_refine_upgrade.txt",
                LineNumber: 54,
                RawLine: "1@@54@@12000@@",
                Fields: ["1", "54", "12000", ""]),
            new ItemRefineUpgrade(
                1,
                Level: 54,
                Value: 12_000,
                SourceFile: "item_refine_upgrade.txt",
                LineNumber: 53,
                RawLine: "1@@54@@12000@@",
                Fields: ["1", "54", "12000", ""]),
            new ItemRefineUpgrade(
                22,
                Level: 54,
                Value: 12_000,
                SourceFile: "item_refine_upgrade.txt",
                LineNumber: 702,
                RawLine: "22@@54@@12000@@",
                Fields: ["22", "54", "12000", ""]),
        };
        var transformInfo = new[]
        {
            new TransformInfo(
                1350,
                Level: 3,
                Name: "DivineHare",
                Lookface: 267,
                MaxLife: 3000,
                SourceFile: "TransformInfo.txt",
                LineNumber: 4,
                RawLine: "1350 3 DivineHare 267 3000",
                Fields: ["1350", "3", "DivineHare", "267", "3000"]),
            new TransformInfo(
                1270,
                Level: 7,
                Name: "Golem",
                Lookface: 274,
                MaxLife: 50000,
                SourceFile: "TransformInfo.txt",
                LineNumber: 13,
                RawLine: "1270 7 Golem 274 50000",
                Fields: ["1270", "7", "Golem", "274", "50000"]),
            new TransformInfo(
                1350,
                Level: 3,
                Name: "DivineHare",
                Lookface: 267,
                MaxLife: 3500,
                SourceFile: "TransformInfo.txt",
                LineNumber: 14,
                RawLine: "1350 3 DivineHare 267 3500",
                Fields: ["1350", "3", "DivineHare", "267", "3500"]),
            new TransformInfo(
                9999,
                Level: 0,
                Name: "MissingMagicForm",
                Lookface: 999,
                MaxLife: 1,
                SourceFile: "TransformInfo.txt",
                LineNumber: 15,
                RawLine: "9999 0 MissingMagicForm 999 1",
                Fields: ["9999", "0", "MissingMagicForm", "999", "1"]),
        };
        var furniture = new[]
        {
            new Furniture(
                9000,
                Kind: 26,
                Lookface: 837,
                MapId: 1511,
                X: 10,
                Y: 20,
                Name: "FlowerDressingTable",
                ItemTypeId: 726083,
                Price: 23_000,
                SourceFile: "furnitures.txt",
                LineNumber: 1,
                RawLine: "9000,26,837,1511,10,20,FlowerDressingTable,726083,23000",
                Fields: ["9000", "26", "837", "1511", "10", "20", "FlowerDressingTable", "726083", "23000"]),
            new Furniture(
                9001,
                Kind: 2,
                Lookface: 8200,
                MapId: 9999,
                X: 11,
                Y: 21,
                Name: "ItemBox",
                ItemTypeId: 722559,
                Price: 60_000,
                SourceFile: "furnitures.txt",
                LineNumber: 2,
                RawLine: "9001,2,8200,9999,11,21,ItemBox,722559,60000",
                Fields: ["9001", "2", "8200", "9999", "11", "21", "ItemBox", "722559", "60000"]),
            new Furniture(
                9000,
                Kind: 26,
                Lookface: 867,
                MapId: 1511,
                X: 10,
                Y: 20,
                Name: "CarvedRosewoodScreen",
                ItemTypeId: 726086,
                Price: 27_000,
                SourceFile: "furnitures.txt",
                LineNumber: 3,
                RawLine: "9000,26,867,1511,10,20,CarvedRosewoodScreen,726086,27000",
                Fields: ["9000", "26", "867", "1511", "10", "20", "CarvedRosewoodScreen", "726086", "27000"]),
        };
        var hairColorTypes = new[]
        {
            new HairColorType(
                78,
                ColorIndex: 5,
                UnlockType: 1,
                ItemTypeId: 3326778,
                Cost: 1,
                SourceFile: "hair_color_type.txt",
                LineNumber: 1,
                RawLine: "78@@5@@1@@3326778@@1@@",
                Fields: ["78", "5", "1", "3326778", "1", ""]),
            new HairColorType(
                78,
                ColorIndex: 5,
                UnlockType: 1,
                ItemTypeId: 3326778,
                Cost: 2,
                SourceFile: "hair_color_type.txt",
                LineNumber: 2,
                RawLine: "78@@5@@1@@3326778@@2@@",
                Fields: ["78", "5", "1", "3326778", "2", ""]),
        };
        var hairFaceStorageTypes = new[]
        {
            new HairFaceStorageType(
                0,
                Id: 1,
                Unknown0: 0,
                Name: "YouthSpark",
                UnlockType: 0,
                RequiredValue: 0,
                Unknown1: 0,
                Cost: 10_000,
                Unknown2: 0,
                LinkedColorIdsRaw: "",
                Sort: 0,
                SourceFile: "hairface_storage_type.txt",
                LineNumber: 1,
                RawLine: "0@@1@@0@@YouthSpark@@0@@0@@0@@10000@@0@@@@0@@",
                Fields: ["0", "1", "0", "YouthSpark", "0", "0", "0", "10000", "0", "", "0", ""]),
            new HairFaceStorageType(
                1,
                Id: 2,
                Unknown0: 0,
                Name: "WindCut",
                UnlockType: 1,
                RequiredValue: 50,
                Unknown1: 0,
                Cost: 20_000,
                Unknown2: 0,
                LinkedColorIdsRaw: "78",
                Sort: 1,
                SourceFile: "hairface_storage_type.txt",
                LineNumber: 2,
                RawLine: "1@@2@@0@@WindCut@@1@@50@@0@@20000@@0@@78@@1@@",
                Fields: ["1", "2", "0", "WindCut", "1", "50", "0", "20000", "0", "78", "1", ""]),
        };
        var coatStorageTypes = new[]
        {
            new CoatStorageType(
                181305,
                StorageType: 31,
                Parameters: [1, 0, 0, 0, 0, 1],
                SourceFile: "coat_storage_type.txt",
                LineNumber: 1,
                RawLine: "181305@@31@@1@@0@@0@@0@@0@@1@@",
                Fields: ["181305", "31", "1", "0", "0", "0", "0", "1", ""]),
            new CoatStorageType(
                181315,
                StorageType: 31,
                Parameters: [1, 0, 0, 0, 0, 1],
                SourceFile: "coat_storage_type.txt",
                LineNumber: 2,
                RawLine: "181315@@31@@1@@0@@0@@0@@0@@1@@",
                Fields: ["181315", "31", "1", "0", "0", "0", "0", "1", ""]),
        };
        var coatStorageAttributes = new[]
        {
            new CoatStorageAttribute(
                17,
                StorageType: 1_000_000_000,
                RequiredStorageScore: 30,
                Bonuses:
                [
                    new CosmeticAttributeBonus(3, 300),
                    new CosmeticAttributeBonus(5, 100),
                    new CosmeticAttributeBonus(0, 0),
                    new CosmeticAttributeBonus(0, 0),
                ],
                Reserved0: 0,
                SourceFile: "coat_storage_attr.txt",
                LineNumber: 3,
                RawLine: "17@@1000000000@@30@@3@@300@@5@@100@@0@@0@@0@@0@@0@@",
                Fields: ["17", "1000000000", "30", "3", "300", "5", "100", "0", "0", "0", "0", "0", ""]),
            new CoatStorageAttribute(
                18,
                StorageType: 1_000_000_000,
                RequiredStorageScore: 50,
                Bonuses:
                [
                    new CosmeticAttributeBonus(3, 500),
                    new CosmeticAttributeBonus(5, 200),
                    new CosmeticAttributeBonus(0, 0),
                    new CosmeticAttributeBonus(0, 0),
                ],
                Reserved0: 0,
                SourceFile: "coat_storage_attr.txt",
                LineNumber: 4,
                RawLine: "18@@1000000000@@50@@3@@500@@5@@200@@0@@0@@0@@0@@0@@",
                Fields: ["18", "1000000000", "50", "3", "500", "5", "200", "0", "0", "0", "0", "0", ""]),
        };
        var coatColorRules = new[]
        {
            new CoatColorRule(
                200412,
                Parameters: [1, 5, 499, 3326059, 1, 3328359, 5, 300, 232503150, 232503150, 232503150, 232503150, 0, 0, 0, 0, 250200, 50],
                SourceFile: "coat_color_rule.txt",
                LineNumber: 1,
                RawLine: "200412@@1@@5@@499@@3326059@@1@@3328359@@5@@300@@232503150@@232503150@@232503150@@232503150@@0@@0@@0@@0@@250200@@50@@",
                Fields: ["200412", "1", "5", "499", "3326059", "1", "3328359", "5", "300", "232503150", "232503150", "232503150", "232503150", "0", "0", "0", "0", "250200", "50", ""]),
        };
        var xuanbaoAdditionAttributes = new[]
        {
            new XuanbaoAdditionAttribute(
                1,
                AttributeType: 14,
                Level: 1,
                Values: [0, 50],
                SourceFile: "xuanbao_addition_attr.txt",
                LineNumber: 1,
                RawLine: "1@@14@@1@@0@@50@@",
                Fields: ["1", "14", "1", "0", "50", ""]),
            new XuanbaoAdditionAttribute(
                2,
                AttributeType: 14,
                Level: 1,
                Values: [10, 60],
                SourceFile: "xuanbao_addition_attr.txt",
                LineNumber: 2,
                RawLine: "2@@14@@1@@10@@60@@",
                Fields: ["2", "14", "1", "10", "60", ""]),
        };
        var xuanbaoComposeAttributeLimits = new[]
        {
            new XuanbaoComposeAttributeLimit(
                1,
                Values: [0, 4, 400],
                SourceFile: "xuanbao_compose_attr_limit.txt",
                LineNumber: 1,
                RawLine: "1@@0@@4@@400@@",
                Fields: ["1", "0", "4", "400", ""]),
        };
        var xuanbaoRandomAttributes = new[]
        {
            new XuanbaoRandomAttribute(
                4100001,
                AttributeType: 1,
                MinValue: 30,
                MaxValue: 101,
                SourceFile: "xuanbao_rand_attr.txt",
                LineNumber: 1,
                RawLine: "4100001@@1@@30@@101@@",
                Fields: ["4100001", "1", "30", "101", ""]),
        };
        var christmasNpcs = new[]
        {
            new ChristmasNpc(
                52301,
                Name: null,
                Kind: 2,
                Lookface: 15820,
                MapId: 1002,
                X: 313,
                Y: 231,
                Parameters: [0, 0, 1, 1],
                SourceFile: "christmasnpcs.txt",
                LineNumber: 1,
                RawLine: "52301,,2,15820,1002,313,231,0,0,1,1",
                Fields: ["52301", "", "2", "15820", "1002", "313", "231", "0", "0", "1", "1"]),
            new ChristmasNpc(
                52316,
                Name: null,
                Kind: 2,
                Lookface: 15820,
                MapId: 1036,
                X: 199,
                Y: 238,
                Parameters: [0, 0, 1, 1],
                SourceFile: "christmasnpcs.txt",
                LineNumber: 16,
                RawLine: "52316,,2,15820,1036,199,238,0,0,1,1",
                Fields: ["52316", "", "2", "15820", "1036", "199", "238", "0", "0", "1", "1"]),
            new ChristmasNpc(
                52316,
                Name: null,
                Kind: 2,
                Lookface: 15830,
                MapId: 9999,
                X: 290,
                Y: 290,
                Parameters: [0, 0, 1, 1],
                SourceFile: "christmasnpcs.txt",
                LineNumber: 17,
                RawLine: "52316,,2,15830,9999,290,290,0,0,1,1",
                Fields: ["52316", "", "2", "15830", "9999", "290", "290", "0", "0", "1", "1"]),
        };
        var tutorTypes = new[]
        {
            new TutorType(
                1,
                MinLevel: 70,
                MaxLevel: 109,
                Values: [2, 10],
                SourceFile: "cq_tutor_type.txt",
                LineNumber: 1,
                RawLine: "1,70,109,2,10",
                Fields: ["1", "70", "109", "2", "10"]),
            new TutorType(
                2,
                MinLevel: 110,
                MaxLevel: 129,
                Values: [3, 15],
                SourceFile: "cq_tutor_type.txt",
                LineNumber: 2,
                RawLine: "2,110,129,3,15",
                Fields: ["2", "110", "129", "3", "15"]),
        };
        var tutorBattleLimitTypes = new[]
        {
            new TutorBattleLimitType(
                1,
                BattlePowerLimit: 100,
                SourceFile: "cq_tutor_battle_limit_type.txt",
                LineNumber: 1,
                RawLine: "1,100",
                Fields: ["1", "100"]),
            new TutorBattleLimitType(
                2,
                BattlePowerLimit: 120,
                SourceFile: "cq_tutor_battle_limit_type.txt",
                LineNumber: 2,
                RawLine: "2,120",
                Fields: ["2", "120"]),
        };
        var beastAttributes = new[]
        {
            new BeastAttribute(
                1,
                Parameters: [2400, 200, 200, 500, 100, 100, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                SourceFile: "beasts_attr.txt",
                LineNumber: 1,
                RawLine: "1@@2400@@200@@200@@500@@100@@100@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@",
                Fields: ["1", "2400", "200", "200", "500", "100", "100", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""]),
            new BeastAttribute(
                2,
                Parameters: [4800, 300, 400, 700, 150, 150, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                SourceFile: "beasts_attr.txt",
                LineNumber: 2,
                RawLine: "2@@4800@@300@@400@@700@@150@@150@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@",
                Fields: ["2", "4800", "300", "400", "700", "150", "150", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""]),
        };
        var fateExperience = new[]
        {
            new FateExperience(
                1,
                Type: 1,
                Level: 1,
                Experience: 500,
                Values: [60_000, 120_000, 130_000, 90_000, 0, 0, 0, 10, 0, 0, 3_300_576],
                SourceFile: "fate_exp.txt",
                LineNumber: 1,
                RawLine: "1@@1@@1@@500@@60000@@120000@@130000@@90000@@0@@0@@0@@10@@0@@0@@3300576@@",
                Fields: ["1", "1", "1", "500", "60000", "120000", "130000", "90000", "0", "0", "0", "10", "0", "0", "3300576", ""]),
            new FateExperience(
                2,
                Type: 1,
                Level: 2,
                Experience: 1_500,
                Values: [60_100, 120_100, 130_100, 90_100, 0, 0, 0, 20, 0, 0, 3_300_577],
                SourceFile: "fate_exp.txt",
                LineNumber: 2,
                RawLine: "2@@1@@2@@1500@@60100@@120100@@130100@@90100@@0@@0@@0@@20@@0@@0@@3300577@@",
                Fields: ["2", "1", "2", "1500", "60100", "120100", "130100", "90100", "0", "0", "0", "20", "0", "0", "3300577", ""]),
        };
        var spiritRates = new[]
        {
            new SpiritRate(
                76,
                Type: 1,
                ItemTypeId: 4_200_001,
                Parameters: [0, 0, 5, 0, 0],
                SourceFile: "spirit_rate.txt",
                LineNumber: 1,
                RawLine: "76@@1@@4200001@@0@@0@@5@@0@@0@@",
                Fields: ["76", "1", "4200001", "0", "0", "5", "0", "0", ""]),
            new SpiritRate(
                151,
                Type: 0,
                ItemTypeId: 4_200_007,
                Parameters: [2, 1, 4_200_008, 0, 0],
                SourceFile: "spirit_rate.txt",
                LineNumber: 76,
                RawLine: "151@@0@@4200007@@2@@1@@4200008@@0@@0@@",
                Fields: ["151", "0", "4200007", "2", "1", "4200008", "0", "0", ""]),
        };
        var hundredWeapons = new[]
        {
            new HundredWeapon(
                1,
                WeaponType: 410,
                Level: 1,
                Values: [0, 100, 10100, 20050, 30030, 15870, 0, 0],
                SourceFile: "hundred_weapon.txt",
                LineNumber: 1,
                RawLine: "1@@410@@1@@0@@100@@10100@@20050@@30030@@15870@@0@@0@@",
                Fields: ["1", "410", "1", "0", "100", "10100", "20050", "30030", "15870", "0", "0", ""]),
            new HundredWeapon(
                2,
                WeaponType: 410,
                Level: 2,
                Values: [12, 0, 10500, 20300, 30200, 15900, 3, 1],
                SourceFile: "hundred_weapon.txt",
                LineNumber: 2,
                RawLine: "2@@410@@2@@12@@0@@10500@@20300@@30200@@15900@@3@@1@@",
                Fields: ["2", "410", "2", "12", "0", "10500", "20300", "30200", "15900", "3", "1", ""]),
        };
        var gouyuTypes = new[]
        {
            new GouyuType(
                1,
                Type: 100,
                Level: 1,
                Values: [50, 10_537, 0, 16_520, 16_530, 0, 0, 0, 100, 10],
                SourceFile: "gouyu_type.txt",
                LineNumber: 1,
                RawLine: "1@@100@@1@@50@@10537@@0@@16520@@16530@@0@@0@@0@@100@@10@@",
                Fields: ["1", "100", "1", "50", "10537", "0", "16520", "16530", "0", "0", "0", "100", "10", ""]),
            new GouyuType(
                315,
                Type: 100,
                Level: 9,
                Values: [0, 37_008, 0, 0, 16_390, 0, 0, 0, 2_000, 8_370],
                SourceFile: "gouyu_type.txt",
                LineNumber: 2,
                RawLine: "315@@100@@9@@0@@37008@@0@@0@@16390@@0@@0@@0@@2000@@8370@@",
                Fields: ["315", "100", "9", "0", "37008", "0", "0", "16390", "0", "0", "0", "2000", "8370", ""]),
        };
        var gouyuImmortals = new[]
        {
            new GouyuImmortal(
                8,
                Values: [8, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
                SourceFile: "gouyu_immortal.txt",
                LineNumber: 1,
                RawLine: "8@@8@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@",
                Fields: ["8", "8", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", ""]),
            new GouyuImmortal(
                17,
                Values: [72, 4000, 1000, 1000, 600, 5800, 10, 5000, 5000, 0, 0, 3000, 2000, 1500, 9],
                SourceFile: "gouyu_immortal.txt",
                LineNumber: 2,
                RawLine: "17@@72@@4000@@1000@@1000@@600@@5800@@10@@5000@@5000@@0@@0@@3000@@2000@@1500@@9@@",
                Fields: ["17", "72", "4000", "1000", "1000", "600", "5800", "10", "5000", "5000", "0", "0", "3000", "2000", "1500", "9", ""]),
        };
        var specialEffectAttributes = new[]
        {
            new SpecialEffectAttribute(
                40_701_011,
                Type: 1,
                Values: [10, 10, 100, 10, 60, 5],
                SourceFile: "speffect_attribute.txt",
                LineNumber: 1,
                RawLine: "40701011@@1@@10@@10@@100@@10@@60@@5@@",
                Fields: ["40701011", "1", "10", "10", "100", "10", "60", "5", ""]),
            new SpecialEffectAttribute(
                40_708_186,
                Type: 1,
                Values: [50, 100, 0, 12_000, 72_000, 10],
                SourceFile: "speffect_attribute.txt",
                LineNumber: 2,
                RawLine: "40708186@@1@@50@@100@@0@@12000@@72000@@10@@",
                Fields: ["40708186", "1", "50", "100", "0", "12000", "72000", "10", ""]),
        };
        var daoqiDictTypes = new[]
        {
            new DaoqiDictType(
                113,
                Type: 0,
                Level: 1,
                Values: [100, 0, 0, 100, 1000, 900, 113, 1, 0, 0, 0, 0],
                SourceFile: "daoqi_dict_type.txt",
                LineNumber: 1,
                RawLine: "113@@0@@1@@100@@0@@0@@100@@1000@@900@@113@@1@@0@@0@@0@@0@@",
                Fields: ["113", "0", "1", "100", "0", "0", "100", "1000", "900", "113", "1", "0", "0", "0", "0", ""]),
            new DaoqiDictType(
                26_907,
                Type: 0,
                Level: 24,
                Values: [21_820_009, 0, 0, 600, 0, 0, 26_007, 10, 0, 0, 10, 0],
                SourceFile: "daoqi_dict_type.txt",
                LineNumber: 2,
                RawLine: "26907@@0@@24@@21820009@@0@@0@@600@@0@@0@@26007@@10@@0@@0@@10@@0@@",
                Fields: ["26907", "0", "24", "21820009", "0", "0", "600", "0", "0", "26007", "10", "0", "0", "10", "0", ""]),
        };
        var syndicateFormationTypes = new[]
        {
            new SyndicateFormationType(
                1,
                Type: 100,
                Level: 1,
                Values: [100_000_000, 120_000_000, 700, 1000, 0],
                SourceFile: "syn_formtype.txt",
                LineNumber: 1,
                RawLine: "1@@100@@1@@100000000@@120000000@@700@@1000@@0@@",
                Fields: ["1", "100", "1", "100000000", "120000000", "700", "1000", "0", ""]),
            new SyndicateFormationType(
                187,
                Type: 100,
                Level: 15,
                Values: [0, 0, 0, 0, 0],
                SourceFile: "syn_formtype.txt",
                LineNumber: 2,
                RawLine: "187@@100@@15@@0@@0@@0@@0@@0@@",
                Fields: ["187", "100", "15", "0", "0", "0", "0", "0", ""]),
        };
        var syndicateSkillTypes = new[]
        {
            new SyndicateSkillType(
                1,
                SkillType: 1,
                Level: 1,
                Values: [500, 75_050, 50_000, 50],
                SourceFile: "syn_skill_type.txt",
                LineNumber: 1,
                RawLine: "1@@1@@1@@500@@75050@@50000@@50@@",
                Fields: ["1", "1", "1", "500", "75050", "50000", "50", ""]),
            new SyndicateSkillType(
                1,
                SkillType: 1,
                Level: 1,
                Values: [600, 75_051, 60_000, 60],
                SourceFile: "syn_skill_type.txt",
                LineNumber: 2,
                RawLine: "1@@1@@1@@600@@75051@@60000@@60@@",
                Fields: ["1", "1", "1", "600", "75051", "60000", "60", ""]),
        };
        var disdainRules = new[]
        {
            new DisdainRule(
                1,
                Values: [0, 400, 2800, 0, 60000, 60000, -90, -90, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100],
                SourceFile: "cq_disdain.txt",
                LineNumber: 1,
                RawLine: "1,0,400,2800,0,60000,60000,-90,-90,1,2000,100,1,2000,100,1,2000,100,1,2000,100",
                Fields: ["1", "0", "400", "2800", "0", "60000", "60000", "-90", "-90", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100"]),
            new DisdainRule(
                1,
                Values: [0, 401, 2800, 0, 60000, 60000, -91, -91, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100, 1, 2000, 100],
                SourceFile: "cq_disdain.txt",
                LineNumber: 2,
                RawLine: "1,0,401,2800,0,60000,60000,-91,-91,1,2000,100,1,2000,100,1,2000,100,1,2000,100",
                Fields: ["1", "0", "401", "2800", "0", "60000", "60000", "-91", "-91", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100", "1", "2000", "100"]),
        };
        var jiangHuAttributes = new[]
        {
            new JiangHuAttribute(
                1,
                AttributeType: 101,
                Level: 1,
                Value: 500,
                SourceFile: "JianghuAttributes.txt",
                LineNumber: 2,
                RawLine: "1 101 1 500",
                Fields: ["1", "101", "1", "500"]),
            new JiangHuAttribute(
                1,
                AttributeType: 101,
                Level: 1,
                Value: 600,
                SourceFile: "JianghuAttributes.txt",
                LineNumber: 3,
                RawLine: "1 101 1 600",
                Fields: ["1", "101", "1", "600"]),
        };
        var jiangHuCultivateStatuses = new[]
        {
            new JiangHuCultivateStatus(
                1,
                DeclaredValueCount: 3,
                Values: [10, 20, 30],
                SourceFile: "JingHuCultivateStatus.txt",
                LineNumber: 2,
                RawLine: "1 3 10 20 30",
                Fields: ["1", "3", "10", "20", "30"]),
            new JiangHuCultivateStatus(
                1,
                DeclaredValueCount: 3,
                Values: [11, 21, 31],
                SourceFile: "JingHuCultivateStatus.txt",
                LineNumber: 3,
                RawLine: "1 3 11 21 31",
                Fields: ["1", "3", "11", "21", "31"]),
        };
        var refineryBoxes = new[]
        {
            new RefineryBox(
                3_300_001,
                Values: [1, 2, 3],
                SourceFile: "databaserefineryboxes.txt",
                LineNumber: 2,
                RawLine: "3300001,1,2,3",
                Fields: ["3300001", "1", "2", "3"]),
            new RefineryBox(
                3_300_001,
                Values: [4, 5, 6],
                SourceFile: "databaserefineryboxes.txt",
                LineNumber: 3,
                RawLine: "3300001,4,5,6",
                Fields: ["3300001", "4", "5", "6"]),
        };
        var refinerySouls = new[]
        {
            new RefinerySoul(
                1,
                "WeaponSoul",
                ItemType: 800001,
                SourceFile: "Souls1.ini",
                LineNumber: 2,
                RawLine: "WeaponSoul 800001",
                Fields: ["WeaponSoul", "800001"]),
            new RefinerySoul(
                2,
                "BetterWeaponSoul",
                ItemType: 800001,
                SourceFile: "Souls2.ini",
                LineNumber: 2,
                RawLine: "BetterWeaponSoul 800001",
                Fields: ["BetterWeaponSoul", "800001"]),
        };
        var refineryMaterials = new[]
        {
            new RefineryMaterial(
                724_350,
                "Detoxication(Unique)Material",
                PrimaryValue: 11,
                EffectType: 7,
                Parameters: [11, 7, 0],
                Target: "Necklace",
                Description: "Level~3~Refinery~item.",
                TailValues: [7, 0, 0, 0, 13_010, 500],
                SourceFile: "Rifinery.txt",
                LineNumber: 1,
                RawLine: "724350 Detoxication(Unique)Material 11 7",
                Fields: ["724350", "Detoxication(Unique)Material", "11", "7"]),
            new RefineryMaterial(
                724_351,
                "Detoxication(Elite)Material",
                PrimaryValue: 13,
                EffectType: 7,
                Parameters: [13, 7, 0],
                Target: "Ring",
                Description: "Level~4~Refinery~item.",
                TailValues: [8, 0, 0, 0, 13_010, 500],
                SourceFile: "Rifinery.txt",
                LineNumber: 2,
                RawLine: "724351 Detoxication(Elite)Material 13 7",
                Fields: ["724351", "Detoxication(Elite)Material", "13", "7"]),
        };
        var stateCountFiles = new[]
        {
            new StateCountFileData(
                "BanIp.txt",
                DeclaredCount: 0,
                Entries: [],
                RawLines: ["Count=0"]),
            new StateCountFileData(
                "Votes.txt",
                DeclaredCount: 1,
                Entries:
                [
                    new StateCountFileEntry(
                        2,
                        "1296463/198.51.100.15/638379291346047487/",
                        ["1296463", "198.51.100.15", "638379291346047487", ""]),
                ],
                RawLines: ["Count=1", "1296463/198.51.100.15/638379291346047487/"]),
        };
        var binarySeedFiles = new[]
        {
            new BinarySeedFile(
                "Insults.bin",
                Length: 4,
                Sha256: "df3f619804a92fdb4057192dc43dd748ea778adc52bc498ce80524c014b81119",
                Base64: "AAAAAA=="),
            new BinarySeedFile(
                "RedeemContainer.bin",
                Length: 4,
                Sha256: "df3f619804a92fdb4057192dc43dd748ea778adc52bc498ce80524c014b81119",
                Base64: "AAAAAA=="),
        };
        var emptySeedFiles = new[]
        {
            new EmptySeedFile(
                "HeroRewards.txt",
                Length: 0,
                Sha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                Base64: ""),
            new EmptySeedFile(
                "ItemtypeSub.txt",
                Length: 0,
                Sha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                Base64: ""),
        };
        var backupSeedFiles = new[]
        {
            new BackupSeedFile(
                "SobNpcs.txt.bak",
                Length: 4,
                Sha256: "5abdb6175f820f0ac3d8647fbb1f7a0bcc91757a782a8a145570944ca6a00c96",
                Base64: "77u/MQ=="),
        };
        var pokerTables = new[]
        {
            new PokerTable(
                1,
                Values: [13, 60, 3, 2, 1, 1000, 2000, 3000],
                SourceFile: "PokerTables.txt",
                LineNumber: 1,
                RawLine: "1 013 060 3 2 1 1000 2000 3000",
                Fields: ["1", "013", "060", "3", "2", "1", "1000", "2000", "3000"]),
            new PokerTable(
                1,
                Values: [14, 61, 3, 2, 1, 1000, 2000, 3000],
                SourceFile: "PokerTables.txt",
                LineNumber: 2,
                RawLine: "1 014 061 3 2 1 1000 2000 3000",
                Fields: ["1", "014", "061", "3", "2", "1", "1000", "2000", "3000"]),
        };
        var rouletteTables = new[]
        {
            new RouletteTable(
                1,
                Values: [3001, 3002, 3003, 1, 2, 3],
                SourceFile: "Roulettes.txt",
                LineNumber: 1,
                RawLine: "1,3001,3002,3003,1,2,3",
                Fields: ["1", "3001", "3002", "3003", "1", "2", "3"]),
            new RouletteTable(
                1,
                Values: [4001, 4002, 4003, 4, 5, 6],
                SourceFile: "Roulettes.txt",
                LineNumber: 2,
                RawLine: "1,4001,4002,4003,4,5,6",
                Fields: ["1", "4001", "4002", "4003", "4", "5", "6"]),
        };
        var npcTimeEntries = new[]
        {
            new NpcTimeEntry(
                1,
                "Arena",
                Values: [1, 2, 3, 4, 5, 6, 7, 8, 9],
                SourceFile: "NpcTime.txt",
                LineNumber: 1,
                RawLine: "1,Arena,1,2,3,4,5,6,7,8,9",
                Fields: ["1", "Arena", "1", "2", "3", "4", "5", "6", "7", "8", "9"]),
            new NpcTimeEntry(
                1,
                "ArenaAgain",
                Values: [9, 8, 7, 6, 5, 4, 3, 2, 1],
                SourceFile: "NpcTime.txt",
                LineNumber: 2,
                RawLine: "1,ArenaAgain,9,8,7,6,5,4,3,2,1",
                Fields: ["1", "ArenaAgain", "9", "8", "7", "6", "5", "4", "3", "2", "1"]),
        };
        var activityTasks = new ActivityTaskCatalog(
            new ActivityTaskMetadata(
                2,
                "ActivityTask",
                SourceLine: 1,
                Entries:
                [
                    new ActivityTaskRawEntry("Num", "2", SourceLine: 2, RawLine: "Num=2"),
                ]),
            Tasks:
            [
                new ActivityTaskDefinition(
                    7,
                    1007,
                    "Daily Gift",
                    Type: 1,
                    Progress: 3,
                    ActiveValue: 25,
                    CloseLevelRaw: "0141",
                    CloseLevel: 141,
                    Tip: "Claim it from Welfare.",
                    OpenLevelRaw: "0005",
                    OpenLevel: 5,
                    PreviewRaw: "1",
                    Preview: 1,
                    Intro: "Finish three rounds.",
                    AnimationSection: "Sparkle",
                    Param: "[npc,Guide,9534,1002,301,529]",
                    SourceSection: "7",
                    SourceLine: 5,
                    Entries:
                    [
                        new ActivityTaskRawEntry("CloseLev", "0141", SourceLine: 11, RawLine: "CloseLev=0141"),
                    ]),
                new ActivityTaskDefinition(
                    2,
                    1002,
                    "Training",
                    Type: 2,
                    Progress: 1,
                    ActiveValue: 10,
                    CloseLevelRaw: "120",
                    CloseLevel: 120,
                    Tip: "Defeat a trainer.",
                    OpenLevelRaw: null,
                    OpenLevel: null,
                    PreviewRaw: null,
                    Preview: null,
                    Intro: null,
                    AnimationSection: null,
                    Param: null,
                    SourceSection: "2",
                    SourceLine: 19,
                    Entries: []),
            ],
            SourceFile: "ActivityTask.ini");
        var meltingTypeRules = new[]
        {
            new MeltingTypeRule(
                1,
                700002,
                Parameters: [0, 0, 1],
                SourceFile: "MeltingType.ini",
                LineNumber: 3,
                RawLine: "1 700002 0 0 1",
                Fields: ["1", "700002", "0", "0", "1"]),
            new MeltingTypeRule(
                1,
                700002,
                Parameters: [1, 0, 1],
                SourceFile: "MeltingType_0.ini",
                LineNumber: 4,
                RawLine: "1 700002 1 0 1",
                Fields: ["1", "700002", "1", "0", "1"]),
        };
        var lotteryPrizes = new[]
        {
            new LotteryPrize(
                1,
                70,
                "WavyHeavyRing+8",
                "151159",
                151159,
                SocketOne: 2,
                SocketTwo: 0,
                PlusLevel: 8,
                SourceFile: "lottery.ini",
                LineNumber: 2,
                RawLine: "1 70 WavyHeavyRing+8 151159 2 0 8",
                Fields: ["1", "70", "WavyHeavyRing+8", "151159", "2", "0", "8"]),
            new LotteryPrize(
                1,
                70,
                "SkyFlame(+8)",
                "624009",
                624009,
                SocketOne: 2,
                SocketTwo: 0,
                PlusLevel: 8,
                SourceFile: "lottery.ini",
                LineNumber: 3,
                RawLine: "1 70 SkyFlame(+8) 624009 2 0 8",
                Fields: ["1", "70", "SkyFlame(+8)", "624009", "2", "0", "8"]),
        };
        var arenaRecords = new[]
        {
            new ArenaRecord(
                1001,
                "ArenaOne",
                Values: [120, 8000, 100001, 4000, 0, 0, 0, 0, 0, 0, 4000, 0, 0, 1],
                SourceFile: "Arena.ini",
                LineNumber: 2,
                RawLine: "1001/ArenaOne/120/8000/100001/4000/0/0/0/0/0/0/4000/0/0/1/",
                Fields: ["1001", "ArenaOne", "120", "8000", "100001", "4000", "0", "0", "0", "0", "0", "0", "4000", "0", "0", "1", ""]),
            new ArenaRecord(
                1001,
                "ArenaDuplicate",
                Values: [121, 8001, 100002, 4100, 1, 1, 1, 1, 1, 1, 4100, 0, 0, 1],
                SourceFile: "Arena.ini",
                LineNumber: 3,
                RawLine: "1001/ArenaDuplicate/121/8001/100002/4100/1/1/1/1/1/1/4100/0/0/1/",
                Fields: ["1001", "ArenaDuplicate", "121", "8001", "100002", "4100", "1", "1", "1", "1", "1", "1", "4100", "0", "0", "1", ""]),
        };
        var teamArenaRecords = new[]
        {
            new TeamArenaRecord(
                2001,
                "TeamArenaOne",
                Values: [140, 7051, 1541008, 4000, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2],
                SourceFile: "TeamArena.ini",
                LineNumber: 2,
                RawLine: "2001/TeamArenaOne/140/7051/1541008/4000/0/0/0/0/0/0/0/0/0/2/",
                Fields: ["2001", "TeamArenaOne", "140", "7051", "1541008", "4000", "0", "0", "0", "0", "0", "0", "0", "0", "0", "2", ""]),
            new TeamArenaRecord(
                2001,
                "TeamArenaDuplicate",
                Values: [141, 7052, 1541009, 4100, 1, 1, 1, 1, 1, 1, 0, 0, 0, 2],
                SourceFile: "TeamArena.ini",
                LineNumber: 3,
                RawLine: "2001/TeamArenaDuplicate/141/7052/1541009/4100/1/1/1/1/1/1/0/0/0/2/",
                Fields: ["2001", "TeamArenaDuplicate", "141", "7052", "1541009", "4100", "1", "1", "1", "1", "1", "1", "0", "0", "0", "2", ""]),
        };
        var elitePkRecords = new[]
        {
            new PkTournamentRecord(3, 7, 140, "EliteOne", 100001, 1, 7001, "ElitePk.ini", 2, "3/7/140/EliteOne/100001/1/7001/", ["3", "7", "140", "EliteOne", "100001", "1", "7001", ""]),
            new PkTournamentRecord(3, 7, 141, "EliteDuplicate", 100002, 1, 7002, "ElitePk.ini", 3, "3/7/141/EliteDuplicate/100002/1/7002/", ["3", "7", "141", "EliteDuplicate", "100002", "1", "7002", ""]),
        };
        var teamElitePkRecords = new[]
        {
            new PkTournamentRecord(2, 6, 130, "TeamOne", 100003, 1, 7003, "TeamElitePK.ini", 2, "2/6/130/TeamOne/100003/1/7003/", ["2", "6", "130", "TeamOne", "100003", "1", "7003", ""]),
            new PkTournamentRecord(2, 6, 131, "TeamDuplicate", 100004, 1, 7004, "TeamElitePK.ini", 3, "2/6/131/TeamDuplicate/100004/1/7004/", ["2", "6", "131", "TeamDuplicate", "100004", "1", "7004", ""]),
        };
        var skillTeamPkRecords = new[]
        {
            new PkTournamentRecord(1, 5, 120, "SkillOne", 100005, 1, 7005, "SkillTeamPK.ini", 2, "1/5/120/SkillOne/100005/1/7005/", ["1", "5", "120", "SkillOne", "100005", "1", "7005", ""]),
            new PkTournamentRecord(1, 5, 121, "SkillDuplicate", 100006, 1, 7006, "SkillTeamPK.ini", 3, "1/5/121/SkillDuplicate/100006/1/7006/", ["1", "5", "121", "SkillDuplicate", "100006", "1", "7006", ""]),
        };
        var classPkWarRecords = new[]
        {
            new ClassPkWarRecord(10, 3, 7001, 100, "ClassPkWar.ini", 2, "10/3/7001/100/", ["10", "3", "7001", "100", ""]),
            new ClassPkWarRecord(10, 3, 7002, 200, "ClassPkWar.ini", 3, "10/3/7002/200/", ["10", "3", "7002", "200", ""]),
        };
        var combatGearRanks = new[]
        {
            new CombatGearRankEntry(5001, "RankOne", RankingFields: ["gear", "100"], SourceFile: "CombatGearRank.txt", LineNumber: 2, RawLine: "5001/RankOne/gear/100/", Fields: ["5001", "RankOne", "gear", "100", ""]),
            new CombatGearRankEntry(5001, "RankDuplicate", RankingFields: ["gear", "200"], SourceFile: "CombatGearRank.txt", LineNumber: 3, RawLine: "5001/RankDuplicate/gear/200/", Fields: ["5001", "RankDuplicate", "gear", "200", ""]),
        };
        var prestigeRanks = new[]
        {
            new PrestigeRankEntry(5001, "PrestigeOne", RankingFields: ["prestige", "100"], SourceFile: "PrestigeRanking.txt", LineNumber: 2, RawLine: "5001/PrestigeOne/prestige/100/", Fields: ["5001", "PrestigeOne", "prestige", "100", ""]),
            new PrestigeRankEntry(5001, "PrestigeDuplicate", RankingFields: ["prestige", "200"], SourceFile: "PrestigeRanking.txt", LineNumber: 3, RawLine: "5001/PrestigeDuplicate/prestige/200/", Fields: ["5001", "PrestigeDuplicate", "prestige", "200", ""]),
        };
        var innerPowerRanks = new[]
        {
            new InnerPowerRankEntry("InnerOne", 5001, RankingFields: ["inner", "100"], SourceFile: "InnerPower.txt", LineNumber: 2, RawLine: "InnerOne/5001/inner/100/", Fields: ["InnerOne", "5001", "inner", "100", ""]),
            new InnerPowerRankEntry("InnerDuplicate", 5001, RankingFields: ["inner", "200"], SourceFile: "InnerPower.txt", LineNumber: 3, RawLine: "InnerDuplicate/5001/inner/200/", Fields: ["InnerDuplicate", "5001", "inner", "200", ""]),
        };
        var runeRankings = new[]
        {
            new RuneRankingRecord("RuneOne", 314_800, 5001, "RuneRanking.txt", 2, "RuneOne/314800/5001/", ["RuneOne", "314800", "5001", ""]),
            new RuneRankingRecord("RuneDuplicate", 271_200, 5001, "RuneRanking.txt", 3, "RuneDuplicate/271200/5001/", ["RuneDuplicate", "271200", "5001", ""]),
        };
        var staticStatues = new[]
        {
            new StaticStatueRecord(298, Values: [298, 34, 1, 179], SourceFile: "StaticStatue.txt", LineNumber: 2, RawLine: "298/34/1/179/", Fields: ["298", "34", "1", "179", ""]),
            new StaticStatueRecord(299, Values: [299, 35, 2, 179], SourceFile: "StaticStatue.txt", LineNumber: 3, RawLine: "299/35/2/179/", Fields: ["299", "35", "2", "179", ""]),
        };
        var newPlayerRewards = new[]
        {
            new NewPlayerRewardRecord("10.0.0.1", "device-a", "NewPlayerRewords.txt", 2, "10.0.0.1/device-a/", ["10.0.0.1", "device-a", ""]),
            new NewPlayerRewardRecord("10.0.0.1", "device-b", "NewPlayerRewords.txt", 3, "10.0.0.1/device-b/", ["10.0.0.1", "device-b", ""]),
        };
        var votes = new[]
        {
            new VoteRecord(7001, "127.0.0.1", "638379291346047487", "Votes.txt", 2, "7001/127.0.0.1/638379291346047487/", ["7001", "127.0.0.1", "638379291346047487", ""]),
            new VoteRecord(7001, "127.0.0.2", "638379291346047488", "Votes.txt", 3, "7001/127.0.0.2/638379291346047488/", ["7001", "127.0.0.2", "638379291346047488", ""]),
        };
        var couplesPkRecords = new[]
        {
            new CouplesPkRecord("PlayerOne", "PartnerOne", "CouplesPK.ini", 2, "PlayerOne/PartnerOne/", ["PlayerOne", "PartnerOne", ""]),
            new CouplesPkRecord("PlayerOne", "PartnerTwo", "CouplesPK.ini", 3, "PlayerOne/PartnerTwo/", ["PlayerOne", "PartnerTwo", ""]),
        };
        var socialAssociations = new[]
        {
            new SocialAssociationRecord(
                1293116,
                4,
                1293600,
                "Coder[GM]",
                Values: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1541008, 100, 0],
                Note: "hey",
                TailValue: 7000,
                SourceFile: "Associate.txt",
                LineNumber: 2,
                RawLine: "1293116/4/0/0/0/1293600/0/0/0/0/Coder[GM]/0/0/0/0/1541008/100/0/hey/7000/",
                Fields: ["1293116", "4", "0", "0", "0", "1293600", "0", "0", "0", "0", "Coder[GM]", "0", "0", "0", "0", "1541008", "100", "0", "hey", "7000", ""]),
            new SocialAssociationRecord(
                1293116,
                4,
                1293600,
                "FriendTwo",
                Values: [600, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1671008, 140, 5],
                Note: "",
                TailValue: 14048,
                SourceFile: "Associate.txt",
                LineNumber: 3,
                RawLine: "1293116/4/600/0/0/1293600/0/0/0/0/FriendTwo/0/0/0/0/1671008/140/5//14048/",
                Fields: ["1293116", "4", "600", "0", "0", "1293600", "0", "0", "0", "0", "FriendTwo", "0", "0", "0", "0", "1671008", "140", "5", "", "14048", ""]),
        };
        var guildWarInfo = CreateGuildWarData("GuildWarInfo.ini", 1363, "GuildFixture", 8_000);
        var superGuildWarInfo = CreateGuildWarData("SuperGuildWarInfo.ini", 2000, "SuperGuildFixture", 16_000);
        var stageGoalInfo = new StageGoalInfo(1);
        var stageGoalStages = new[]
        {
            new StageGoalStage(
                1,
                "0001",
                OpenLevel: 1,
                StageAnimation: "Stage_01goalBtn",
                GoalCount: 1,
                Rewards:
                [
                    new StageGoalReward(
                        1,
                        3005107,
                        Amount: 1,
                        BoundFlag: 0,
                        Key: "Prize1",
                        RawValue: "3005107@@1@@0",
                        SourceLine: 5,
                        Fields: ["3005107", "1", "0"]),
                ],
                SourceLine: 2,
                Entries: []),
        };
        var stageGoalTasks = new[]
        {
            new StageGoalTask(
                1,
                1,
                "[Novice] Level-up",
                "Upgrade to Level 20+.",
                Progress: 20,
                Rewards:
                [
                    new StageGoalReward(
                        1,
                        723700,
                        Amount: 1,
                        BoundFlag: 1,
                        Key: "Prize1",
                        RawValue: "723700@@1@@1",
                        SourceLine: 12,
                        Fields: ["723700", "1", "1"]),
                ],
                TaskType: 1,
                RequireRaw: null,
                Require: null,
                SourceLine: 8,
                Entries: []),
        };
        var shops = new[]
        {
            new ShopDefinition(
                432,
                Name: null,
                Type: null,
                MoneyType: 1,
                SourceFile: "emoneyshop.ini",
                SourceDialect: "EmoneyShop",
                Attributes: new Dictionary<string, string> { ["source"] = "fixture" }),
            new ShopDefinition(
                432,
                Name: "DuplicateShop",
                Type: null,
                MoneyType: 1,
                SourceFile: "emoneyShopV2.ini",
                SourceDialect: "EmoneyShopV2",
                Attributes: new Dictionary<string, string>()),
        };
        var shopItems = new[]
        {
            new ShopItem(
                432,
                205003,
                Position: 0,
                Category: "item",
                Status: "new",
                IsBound: false,
                Price: null,
                Description: null,
                Flags: ["new"],
                SourceFile: "emoneyshop.ini",
                SourceDialect: "EmoneyShop",
                RawLine: "Item0=205003 new",
                Attributes: new Dictionary<string, string> { ["key"] = "Item0" }),
            new ShopItem(
                432,
                1000000,
                Position: 1,
                Category: "recommend",
                Status: null,
                IsBound: false,
                Price: null,
                Description: "Fixture~description.",
                Flags: [],
                SourceFile: "emoneyshop.ini",
                SourceDialect: "EmoneyShop",
                RawLine: "Item1=1000000 null Fixture~description.",
                Attributes: new Dictionary<string, string> { ["key"] = "Item1" }),
        };
        var exchangeShopGoods = new[]
        {
            new ExchangeShopRow("exchange_shop_goods.txt", 1, ["444", "3303526", ""]),
        };
        var exchangeShopGoodsEx = new[]
        {
            new ExchangeShopRow("exchange_shop_goods_ex.txt", 1, ["1", "3007386", ""]),
        };
        var exchangeShopLevels = new[]
        {
            new ExchangeShopRow("exchange_shop_lev.txt", 1, ["1", "131", "0", "15", "0", ""]),
        };
        var honorShopItems = new[]
        {
            new HonorShopItem(
                3303085,
                Cost: 3000,
                Unknown0: 0,
                Category: "item",
                IsRecommended: false,
                Status: "new",
                Position: 0,
                SourceFile: "HonorShop.ini",
                RawLine: "3303085,3000,0,item,,new",
                Fields: ["3303085", "3000", "0", "item", "", "new"]),
        };
        var honorShopRecommendations = new[]
        {
            new HonorShopRecommendation(
                3303085,
                "Open~to~receive~a~Class~6~House~Pack.",
                Position: 0,
                SourceFile: "HonorShop.ini",
                RawLine: "3303085=Open~to~receive~a~Class~6~House~Pack."),
        };
        var racePointShopItems = new[]
        {
            new PointShopItem(
                3009000,
                Cost: 20,
                ItemClass: "pills",
                Category: "item",
                Status: null,
                Section: "Normal",
                Position: 0,
                SourceFile: "RacePointShop.ini",
                RawLine: "3009000,20,pills,item",
                Fields: ["3009000", "20", "pills", "item"]),
        };
        var racePointShopRecommendations = new[]
        {
            new PointShopRecommendation(
                3009000,
                "Open~to~get~5~pills.",
                Section: "Recommend",
                Position: 0,
                SourceFile: "RacePointShop.ini",
                RawLine: "3009000=Open~to~get~5~pills."),
        };
        var goldenLeagueShopItems = new[]
        {
            new PointShopItem(
                3303526,
                Cost: 500,
                ItemClass: "rune",
                Category: "equip",
                Status: "other",
                Section: "Recommend",
                Position: 0,
                SourceFile: "GoldenLeagueShop.ini",
                RawLine: "3303526,500,rune,equip,other",
                Fields: ["3303526", "500", "rune", "equip", "other"]),
        };
        var goldenLeagueShopRecommendations = new[]
        {
            new PointShopRecommendation(
                3303526,
                "Open~to~select~a~red~Rune~(B).",
                Section: "Recommend",
                Position: 0,
                SourceFile: "GoldenLeagueShop.ini",
                RawLine: "3303526=Open~to~select~a~red~Rune~(B)."),
        };
        var goldenLeagueShopConfig = new Dictionary<string, string>
        {
            ["Color.TipColor"] = "0xffffffff",
            ["PointLimit.GoldenLeaguePointLimit"] = "304400",
        };
        var booths = new[]
        {
            new BoothDefinition(
                700050,
                "Promoted",
                38194,
                1004,
                Type: null,
                X: 43,
                Y: 58,
                DeclaredItemAmount: 99,
                SourceSection: "booth700050",
                SourceLine: 2,
                Position: 0,
                SourceFile: "Booths.txt",
                Attributes: new Dictionary<string, string> { ["ID"] = "700050" }),
            new BoothDefinition(
                700050,
                "DuplicateBooth",
                38195,
                1004,
                Type: 16,
                X: 44,
                Y: 59,
                DeclaredItemAmount: 1,
                SourceSection: "booth700050",
                SourceLine: 12,
                Position: 1,
                SourceFile: "Booths.txt",
                Attributes: new Dictionary<string, string>()),
        };
        var boothItems = new[]
        {
            new BoothItem(
                700050,
                721080,
                Price: 1_000,
                Unknown0: 0,
                Tail: "0@0@0@0",
                ExtraField: null,
                ItemKey: 1,
                Position: 0,
                SourceFile: "Booths.txt",
                SourceLine: 10,
                RawLine: "Item1=721080@@1000@@0@@0@0@0@0",
                Fields: ["721080", "1000", "0", "0@0@0@0"]),
            new BoothItem(
                700050,
                1080001,
                Price: 2_000,
                Unknown0: 0,
                Tail: "0@0@0@0",
                ExtraField: "",
                ItemKey: 1,
                Position: 1,
                SourceFile: "Booths.txt",
                SourceLine: 11,
                RawLine: "Item1=1080001@@2000@@0@@0@0@0@0@@",
                Fields: ["1080001", "2000", "0", "0@0@0@0", ""]),
        };
        var counts = new Dictionary<string, int>
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
            ["questTaskTypes"] = questTaskTypes.Length,
            ["questMissions"] = questMissions.Length,
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
            ["neiGongSecrets"] = neiGongSecrets.Length,
            ["neiGongTypes"] = neiGongTypes.Length,
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
            ["stageGoalStages"] = stageGoalStages.Length,
            ["stageGoalTasks"] = stageGoalTasks.Length,
            ["shops"] = shops.Length,
            ["shopItems"] = shopItems.Length,
            ["exchangeShopGoods"] = exchangeShopGoods.Length,
            ["exchangeShopGoodsEx"] = exchangeShopGoodsEx.Length,
            ["exchangeShopLevels"] = exchangeShopLevels.Length,
            ["honorShopItems"] = honorShopItems.Length,
            ["honorShopRecommendations"] = honorShopRecommendations.Length,
            ["racePointShopItems"] = racePointShopItems.Length,
            ["racePointShopRecommendations"] = racePointShopRecommendations.Length,
            ["goldenLeagueShopItems"] = goldenLeagueShopItems.Length,
            ["goldenLeagueShopRecommendations"] = goldenLeagueShopRecommendations.Length,
            ["goldenLeagueShopConfig"] = goldenLeagueShopConfig.Count,
            ["booths"] = booths.Length,
            ["boothItems"] = boothItems.Length,
        };

        if (countOverrides is not null)
        {
            foreach (var pair in countOverrides)
            {
                counts[pair.Key] = pair.Value;
            }
        }

        WriteJson("manifest.json", new SeedManifest(DateTimeOffset.Parse("2026-06-02T04:30:41Z"), "fixture://db", counts));
        WriteJson("clientConfigs.json", clientConfigs);
        WriteJson("items.json", items);
        WriteJson("itemTypeSubs.json", itemTypeSubs);
        WriteJson("itemAddBonuses.json", itemAddBonuses);
        WriteJson("levelExperience.json", levelExperience);
        WriteJson("classLevelStats.json", classLevelStats);
        WriteJson("professionLevelUps.json", professionLevelUps);
        WriteJson("professionLevelBenefits.json", professionLevelBenefits);
        WriteJson("professionTitleBenefits.json", professionTitleBenefits);
        WriteJson("questInfo.json", questInfo);
        WriteJson("questTaskTypes.json", questTaskTypes);
        WriteJson("questMissions.json", questMissions);
        WriteJson("quizQuestions.json", quizQuestions);
        WriteJson("magic.json", magic);
        WriteJson("magicEffects.json", magicEffects);
        WriteJson("magicTypeOperations.json", magicTypeOperations);
        WriteJson("titleTypes.json", titleTypes);
        WriteJson("combatGear.json", combatGear);
        WriteJson("traps.json", traps);
        WriteJson("npcs.json", npcs);
        WriteJson("sobNpcs.json", sobNpcs);
        WriteJson("portals.json", portals);
        WriteJson("maps.json", maps);
        WriteJson("mapNames.json", mapNames);
        WriteJson("legacyGameMaps.json", legacyGameMaps);
        WriteJson("monsters.json", monsters);
        WriteJson("monsterSpawns.json", monsterSpawns);
        WriteJson("lotteryDictionary.json", lotteryDictionary);
        WriteJson("collectionRewardTiers.json", collectionRewardTiers);
        WriteJson("taskRewardTypes.json", taskRewardTypes);
        WriteJson("taskRewardTypeIniEntries.json", taskRewardTypeIniEntries);
        WriteJson("neiGongHeader.json", neiGongHeader);
        WriteJson("neiGongSecrets.json", neiGongSecrets);
        WriteJson("neiGongTypes.json", neiGongTypes);
        WriteJson("subProfessions.json", subProfessions);
        WriteJson("syndicateLevels.json", syndicateLevels);
        WriteJson("runeLevelExperience.json", runeLevelExperience);
        WriteJson("runeStorageAttributes.json", runeStorageAttributes);
        WriteJson("itemRefineEffects.json", itemRefineEffects);
        WriteJson("itemRefineEffectEx.json", itemRefineEffectEx);
        WriteJson("itemRefineUpgrades.json", itemRefineUpgrades);
        WriteJson("transformInfo.json", transformInfo);
        WriteJson("furniture.json", furniture);
        WriteJson("hairColorTypes.json", hairColorTypes);
        WriteJson("hairFaceStorageTypes.json", hairFaceStorageTypes);
        WriteJson("coatStorageTypes.json", coatStorageTypes);
        WriteJson("coatStorageAttributes.json", coatStorageAttributes);
        WriteJson("coatColorRules.json", coatColorRules);
        WriteJson("xuanbaoAdditionAttributes.json", xuanbaoAdditionAttributes);
        WriteJson("xuanbaoComposeAttributeLimits.json", xuanbaoComposeAttributeLimits);
        WriteJson("xuanbaoRandomAttributes.json", xuanbaoRandomAttributes);
        WriteJson("christmasNpcs.json", christmasNpcs);
        WriteJson("tutorTypes.json", tutorTypes);
        WriteJson("tutorBattleLimitTypes.json", tutorBattleLimitTypes);
        WriteJson("beastAttributes.json", beastAttributes);
        WriteJson("fateExperience.json", fateExperience);
        WriteJson("spiritRates.json", spiritRates);
        WriteJson("hundredWeapons.json", hundredWeapons);
        WriteJson("gouyuTypes.json", gouyuTypes);
        WriteJson("gouyuImmortals.json", gouyuImmortals);
        WriteJson("specialEffectAttributes.json", specialEffectAttributes);
        WriteJson("daoqiDictTypes.json", daoqiDictTypes);
        WriteJson("syndicateFormationTypes.json", syndicateFormationTypes);
        WriteJson("syndicateSkillTypes.json", syndicateSkillTypes);
        WriteJson("disdainRules.json", disdainRules);
        WriteJson("jiangHuAttributes.json", jiangHuAttributes);
        WriteJson("jiangHuCultivateStatuses.json", jiangHuCultivateStatuses);
        WriteJson("refineryBoxes.json", refineryBoxes);
        WriteJson("refinerySouls.json", refinerySouls);
        WriteJson("refineryMaterials.json", refineryMaterials);
        WriteJson("stateCountFiles.json", stateCountFiles);
        WriteJson("binarySeedFiles.json", binarySeedFiles);
        WriteJson("emptySeedFiles.json", emptySeedFiles);
        WriteJson("backupSeedFiles.json", backupSeedFiles);
        WriteJson("pokerTables.json", pokerTables);
        WriteJson("rouletteTables.json", rouletteTables);
        WriteJson("npcTimeEntries.json", npcTimeEntries);
        WriteJson("activityTasks.json", activityTasks);
        WriteJson("meltingTypeRules.json", meltingTypeRules);
        WriteJson("lotteryPrizes.json", lotteryPrizes);
        WriteJson("arenaRecords.json", arenaRecords);
        WriteJson("teamArenaRecords.json", teamArenaRecords);
        WriteJson("elitePkRecords.json", elitePkRecords);
        WriteJson("teamElitePkRecords.json", teamElitePkRecords);
        WriteJson("skillTeamPkRecords.json", skillTeamPkRecords);
        WriteJson("classPkWarRecords.json", classPkWarRecords);
        WriteJson("combatGearRanks.json", combatGearRanks);
        WriteJson("prestigeRanks.json", prestigeRanks);
        WriteJson("innerPowerRanks.json", innerPowerRanks);
        WriteJson("runeRankings.json", runeRankings);
        WriteJson("staticStatues.json", staticStatues);
        WriteJson("newPlayerRewards.json", newPlayerRewards);
        WriteJson("votes.json", votes);
        WriteJson("couplesPkRecords.json", couplesPkRecords);
        WriteJson("socialAssociations.json", socialAssociations);
        WriteJson("guildWarInfo.json", guildWarInfo);
        WriteJson("superGuildWarInfo.json", superGuildWarInfo);
        WriteJson("stageGoalInfo.json", stageGoalInfo);
        WriteJson("stageGoalStages.json", stageGoalStages);
        WriteJson("stageGoalTasks.json", stageGoalTasks);
        WriteJson("shops.json", shops);
        WriteJson("shopItems.json", shopItems);
        WriteJson("exchangeShopGoods.json", exchangeShopGoods);
        WriteJson("exchangeShopGoodsEx.json", exchangeShopGoodsEx);
        WriteJson("exchangeShopLevels.json", exchangeShopLevels);
        WriteJson("honorShopItems.json", honorShopItems);
        WriteJson("honorShopRecommendations.json", honorShopRecommendations);
        WriteJson("racePointShopItems.json", racePointShopItems);
        WriteJson("racePointShopRecommendations.json", racePointShopRecommendations);
        WriteJson("goldenLeagueShopItems.json", goldenLeagueShopItems);
        WriteJson("goldenLeagueShopRecommendations.json", goldenLeagueShopRecommendations);
        WriteJson("goldenLeagueShopConfig.json", goldenLeagueShopConfig);
        WriteJson("booths.json", booths);
        WriteJson("boothItems.json", boothItems);
    }

    private static GuildWarData CreateGuildWarData(string sourceFile, int id, string name, long hitPoints)
    {
        var infoId = new GuildWarEntry("Info", "ID", id.ToString(), "ID", id.ToString(), sourceFile, 2, $"ID={id}");
        var infoName = new GuildWarEntry("Info", "Name", name, "Name", name, sourceFile, 3, $"Name={name}");
        var leaderReward = new GuildWarEntry("Info", "LeaderReward", "0", "LeaderReward", "0", sourceFile, 4, "LeaderReward=0");
        var deputyReward = new GuildWarEntry("Info", "DeputiLeaderReward", "7", "DeputiLeaderReward", "7", sourceFile, 5, "DeputiLeaderReward=7");
        var poleName = new GuildWarEntry("Pole", "Name", "AttackPole", "Name", "AttackPole", sourceFile, 7, "Name=AttackPole");
        var poleHitPoints = new GuildWarEntry("Pole", "HitPoints", hitPoints.ToString(), "HitPoints", hitPoints.ToString(), sourceFile, 8, $"HitPoints={hitPoints}");
        var conductor = new GuildWarEntry("Condutors", "GuildConductor1", "101620,307,279,1000,", "GuildConductor1", "101620,307,279,1000,", sourceFile, 10, "GuildConductor1=101620,307,279,1000,");
        var infoEntries = new[] { infoId, infoName, leaderReward, deputyReward };
        var poleEntries = new[] { poleName, poleHitPoints };
        var conductorEntries = new[] { conductor };
        var entries = infoEntries.Concat(poleEntries).Concat(conductorEntries).ToArray();
        var sections = new[]
        {
            new GuildWarSection("Info", sourceFile, 1, "[Info]", infoEntries),
            new GuildWarSection("Pole", sourceFile, 6, "[Pole]", poleEntries),
            new GuildWarSection("Condutors", sourceFile, 9, "[Condutors]", conductorEntries),
        };

        return new GuildWarData(
            sourceFile,
            sections,
            entries,
            Array.Empty<WorldDataDiagnostic>(),
            new GuildWarInfoData(
                id,
                name,
                LeaderReward: 0,
                DeputiLeaderReward: 7,
                LeaderTop: new Dictionary<int, int> { [1] = 100 },
                DeputiTop: new Dictionary<int, int> { [1] = 50 }),
            new GuildWarPoleData("AttackPole", hitPoints),
            [
                new GuildWarConductorRow(
                    "GuildConductor1",
                    ["101620", "307", "279", "1000", ""],
                    sourceFile,
                    10,
                    "GuildConductor1=101620,307,279,1000,",
                    conductor),
            ]);
    }

    private void WriteJson<T>(string fileName, T value)
    {
        File.WriteAllText(Path.Combine(_rootPath, fileName), JsonSerializer.Serialize(value, JsonOptions));
    }

    private static MonsterSpawn Spawn(int generatorId, int mapId, int monsterTypeId)
    {
        return new MonsterSpawn(
            generatorId,
            mapId,
            monsterTypeId,
            BoundX: 1,
            BoundY: 2,
            BoundWidth: 3,
            BoundHeight: 4,
            MaxNpc: 5,
            RestSeconds: 6,
            MaxPerGenerator: 7,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: mapId,
            SourceMonsterName: "Pheasant",
            SourceRelativePath: Path.Combine(mapId.ToString(), "Pheasant", generatorId + ".ini"),
            Attributes: new Dictionary<string, string> { ["return_mode"] = "1" });
    }

    private static MonsterSpawn LoadMapMonsterSpawn(
        int rowId,
        int mapId,
        int monsterTypeId,
        string sourceRelativePath,
        int sourceLine)
    {
        return new MonsterSpawn(
            rowId,
            mapId,
            monsterTypeId,
            BoundX: 1,
            BoundY: 2,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 0,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: null,
            SourceMonsterName: "Pheasant",
            SourceRelativePath: sourceRelativePath,
            Attributes: new Dictionary<string, string>
            {
                ["legacy_source"] = "LoadMapMonsters",
                ["legacy_uid"] = rowId.ToString(CultureInfo.InvariantCulture),
                ["source_line"] = sourceLine.ToString(CultureInfo.InvariantCulture),
            });
    }

    private static QuestMission QuestMission(
        int sectionId,
        int missionId,
        int mapId,
        IReadOnlyList<int> prizeItemIds,
        IReadOnlyList<int> prerequisiteMissionIds,
        int intentAmount,
        IReadOnlyList<QuestNpcAnchor>? beginNpcAnchors = null,
        IReadOnlyList<QuestNpcAnchor>? finishNpcAnchors = null)
    {
        return new QuestMission(
            sectionId,
            SourceLine: sectionId,
            TypeId: 1,
            missionId,
            Name: "Fixture Quest",
            LevelMin: 1,
            LevelMax: 140,
            Auto: 0,
            First: 1,
            PrerequisiteRaw: string.Join('|', prerequisiteMissionIds),
            PrerequisiteMissionIds: prerequisiteMissionIds,
            MapId: mapId,
            Profession: "0",
            Sex: 0,
            FinishTime: 0,
            ActivityBeginTime: "0",
            ActivityEndTime: "0",
            CompleteFlag: 2,
            Prize: string.Concat(prizeItemIds.Select(itemId => $"[item {itemId},0,0,0,0,0,0]")),
            IntentionDescription: "Fixture intention",
            IntentAmount: intentAmount,
            Content: "Fixture content",
            BeginNpcAnchors: beginNpcAnchors ??
            [
                new QuestNpcAnchor("Begin", "BeginNpcId", null, 200, 1000, 10, 20, "KnownNpc", "TwinCity", "200,1000,10,20,KnownNpc,TwinCity", SourceLine: 1, Fields: ["200", "1000", "10", "20", "KnownNpc", "TwinCity"]),
            ],
            FinishNpcAnchors: finishNpcAnchors ??
            [
                new QuestNpcAnchor("Finish", "FinishNpcId", null, 999999, 9999, 11, 21, "MissingNpc", "MissingMap", "999999,9999,11,21,MissingNpc,MissingMap", SourceLine: 2, Fields: ["999999", "9999", "11", "21", "MissingNpc", "MissingMap"]),
            ],
            Intentions:
            [
                new QuestIntention(1, null, "Intention1", "Fixture intention", SourceLine: 3),
            ],
            PrizeItems: prizeItemIds.Select((itemId, index) => new QuestPrizeItemRef(itemId, ",0,0,0,0,0,0", index)).ToArray(),
            Entries:
            [
                new QuestRawEntry("ActivityType", "0", SourceLine: 4, RawLine: "ActivityType=0"),
                new QuestRawEntry("ActivityType", "1", SourceLine: 5, RawLine: "ActivityType=1"),
            ]);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WolfConquer.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate WolfConquer.sln from the test output directory.");
    }
}
