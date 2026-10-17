namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    public IReadOnlyDictionary<int, ClientConfigRecord> ClientConfigsByServerId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<ClientConfigRecord>> ClientConfigsByServerName { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<ClientConfigRecord>> ClientConfigsByHost { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ClientConfigRecord>> ClientConfigsByPort { get; }

    public IReadOnlyDictionary<int, MapMetadata> MapsById { get; }

    public IReadOnlyDictionary<int, MapName> MapNamesByMapId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<MapName>> MapNamesByName { get; }

    public IReadOnlyDictionary<int, LegacyGameMapEntry> LegacyGameMapsByMapId { get; }

    public IReadOnlyDictionary<int, Monster> MonstersById { get; }

    public IReadOnlyDictionary<int, TitleType> TitleTypesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<TitleType>> TitleTypesByTypeId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<TitleType>> TitleTypesByName { get; }

    public IReadOnlyDictionary<int, CombatGear> CombatGearById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<CombatGear>> CombatGearByGearTypeId { get; }

    public IReadOnlyDictionary<(int GearTypeId, int Level), CombatGear> CombatGearByGearTypeAndLevel { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<Trap>> TrapsByMapId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<Trap>> TrapsByTrapTypeId { get; }

    public IReadOnlyDictionary<(int MapId, int X, int Y), IReadOnlyList<Trap>> TrapsByMapPosition { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ItemType>> ItemsById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ItemTypeSub>> ItemTypeSubsByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ItemAddBonus>> ItemAddBonusesByItemTypeId { get; }

    public IReadOnlyDictionary<(int ItemTypeId, int Level), ItemAddBonus> ItemAddBonusByItemTypeIdAndLevel { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<LevelExperience>> LevelExperienceByType { get; }

    public IReadOnlyDictionary<(int Type, int Level), LevelExperience> LevelExperienceByTypeAndLevel { get; }

    public IReadOnlyDictionary<int, RuneLevelExperience> RuneLevelExperienceById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<RuneLevelExperience>> RuneLevelExperienceByRuneType { get; }

    public IReadOnlyDictionary<(int RuneType, int Level), RuneLevelExperience> RuneLevelExperienceByRuneTypeAndLevel { get; }

    public IReadOnlyDictionary<int, RuneStorageAttribute> RuneStorageAttributesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<RuneStorageAttribute>> RuneStorageAttributesByRequiredStorageScore { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ItemRefineEffect>> ItemRefineEffectsById { get; }

    public IReadOnlyDictionary<int, ItemRefineEffectEx> ItemRefineEffectExById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ItemRefineEffectEx>> ItemRefineEffectExByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ItemRefineUpgrade>> ItemRefineUpgradesById { get; }

    public IReadOnlyDictionary<(int Id, int Level), ItemRefineUpgrade> ItemRefineUpgradeByIdAndLevel { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<TransformInfo>> TransformInfoByTransformId { get; }

    public IReadOnlyDictionary<(int TransformId, int Level), TransformInfo> TransformInfoByTransformIdAndLevel { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<TransformInfo>> TransformInfoByLookface { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<TransformInfo>> TransformInfoByName { get; }

    public IReadOnlyDictionary<int, Furniture> FurnitureById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<Furniture>> FurnitureByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<Furniture>> FurnitureByMapId { get; }

    public IReadOnlyDictionary<(int MapId, int X, int Y), IReadOnlyList<Furniture>> FurnitureByMapPosition { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<Furniture>> FurnitureByKind { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<Furniture>> FurnitureByName { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HairColorType>> HairColorTypesByHairColorId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HairColorType>> HairColorTypesByColorIndex { get; }

    public IReadOnlyDictionary<(int HairColorId, int ColorIndex), HairColorType> HairColorTypeByHairColorIdAndColorIndex { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HairColorType>> HairColorTypesByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HairFaceStorageType>> HairFaceStorageTypesByCategoryType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HairFaceStorageType>> HairFaceStorageTypesById { get; }

    public IReadOnlyDictionary<(int CategoryType, int Id), HairFaceStorageType> HairFaceStorageTypeByCategoryAndId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<HairFaceStorageType>> HairFaceStorageTypesByName { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HairFaceStorageType>> HairFaceStorageTypesByUnlockType { get; }

    public IReadOnlyDictionary<int, CoatStorageType> CoatStorageTypesByItemTypeId { get; }

    public IReadOnlyDictionary<long, IReadOnlyList<CoatStorageType>> CoatStorageTypesByStorageType { get; }

    public IReadOnlyDictionary<int, CoatStorageAttribute> CoatStorageAttributesById { get; }

    public IReadOnlyDictionary<long, IReadOnlyList<CoatStorageAttribute>> CoatStorageAttributesByStorageType { get; }

    public IReadOnlyDictionary<int, CoatColorRule> CoatColorRulesByItemTypeId { get; }

    public IReadOnlyDictionary<int, XuanbaoAdditionAttribute> XuanbaoAdditionAttributesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<XuanbaoAdditionAttribute>> XuanbaoAdditionAttributesByAttributeType { get; }

    public IReadOnlyDictionary<(int AttributeType, int Level), XuanbaoAdditionAttribute> XuanbaoAdditionAttributeByAttributeTypeAndLevel { get; }

    public IReadOnlyDictionary<int, XuanbaoComposeAttributeLimit> XuanbaoComposeAttributeLimitsById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<XuanbaoRandomAttribute>> XuanbaoRandomAttributesByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<XuanbaoRandomAttribute>> XuanbaoRandomAttributesByAttributeType { get; }

    public IReadOnlyDictionary<(int ItemTypeId, int AttributeType), XuanbaoRandomAttribute> XuanbaoRandomAttributeByItemTypeIdAndAttributeType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ChristmasNpc>> ChristmasNpcsById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ChristmasNpc>> ChristmasNpcsByMapId { get; }

    public IReadOnlyDictionary<(int MapId, int X, int Y), IReadOnlyList<ChristmasNpc>> ChristmasNpcsByMapPosition { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ChristmasNpc>> ChristmasNpcsByLookface { get; }

    public IReadOnlyDictionary<int, TutorType> TutorTypesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<TutorType>> TutorTypesByMinLevel { get; }

    public IReadOnlyDictionary<int, TutorBattleLimitType> TutorBattleLimitTypesById { get; }

    public IReadOnlyDictionary<int, BeastAttribute> BeastAttributesByLevel { get; }

    public IReadOnlyDictionary<int, FateExperience> FateExperienceById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<FateExperience>> FateExperienceByType { get; }

    public IReadOnlyDictionary<(int Type, int Level), FateExperience> FateExperienceByTypeAndLevel { get; }

    public IReadOnlyDictionary<int, SpiritRate> SpiritRatesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SpiritRate>> SpiritRatesByType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SpiritRate>> SpiritRatesByItemTypeId { get; }

    public IReadOnlyDictionary<int, HundredWeapon> HundredWeaponsById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HundredWeapon>> HundredWeaponsByWeaponType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HundredWeapon>> HundredWeaponsByLevel { get; }

    public IReadOnlyDictionary<int, GouyuType> GouyuTypesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<GouyuType>> GouyuTypesByType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<GouyuType>> GouyuTypesByLevel { get; }

    public IReadOnlyDictionary<int, GouyuImmortal> GouyuImmortalsById { get; }

    public IReadOnlyDictionary<int, SpecialEffectAttribute> SpecialEffectAttributesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SpecialEffectAttribute>> SpecialEffectAttributesByType { get; }

    public IReadOnlyDictionary<int, DaoqiDictType> DaoqiDictTypesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<DaoqiDictType>> DaoqiDictTypesByType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<DaoqiDictType>> DaoqiDictTypesByLevel { get; }

    public IReadOnlyDictionary<int, SyndicateFormationType> SyndicateFormationTypesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SyndicateFormationType>> SyndicateFormationTypesByType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SyndicateFormationType>> SyndicateFormationTypesByLevel { get; }

    public IReadOnlyDictionary<int, SyndicateSkillType> SyndicateSkillTypesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SyndicateSkillType>> SyndicateSkillTypesBySkillType { get; }

    public IReadOnlyDictionary<(int SkillType, int Level), SyndicateSkillType> SyndicateSkillTypesBySkillTypeAndLevel { get; }

    public IReadOnlyDictionary<int, DisdainRule> DisdainRulesById { get; }

    public IReadOnlyDictionary<int, JiangHuAttribute> JiangHuAttributesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<JiangHuAttribute>> JiangHuAttributesByType { get; }

    public IReadOnlyDictionary<(int AttributeType, int Level), JiangHuAttribute> JiangHuAttributesByTypeAndLevel { get; }

    public IReadOnlyDictionary<int, JiangHuCultivateStatus> JiangHuCultivateStatusesById { get; }

    public IReadOnlyDictionary<int, RefineryBox> RefineryBoxesByItemType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<RefinerySoul>> RefinerySoulsByLevel { get; }

    public IReadOnlyDictionary<int, RefinerySoul> RefinerySoulsByItemType { get; }

    public IReadOnlyDictionary<int, RefineryMaterial> RefineryMaterialsByItemType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<RefineryMaterial>> RefineryMaterialsByEffectType { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<RefineryMaterial>> RefineryMaterialsByTarget { get; }

    public IReadOnlyDictionary<string, StateCountFileData> StateCountFilesBySourceFile { get; }

    public IReadOnlyDictionary<string, BinarySeedFile> BinarySeedFilesBySourceFile { get; }

    public IReadOnlyDictionary<string, EmptySeedFile> EmptySeedFilesBySourceFile { get; }

    public IReadOnlyDictionary<string, BackupSeedFile> BackupSeedFilesBySourceFile { get; }

    public IReadOnlyDictionary<int, PokerTable> PokerTablesById { get; }

    public IReadOnlyDictionary<int, RouletteTable> RouletteTablesById { get; }

    public IReadOnlyDictionary<int, NpcTimeEntry> NpcTimeEntriesById { get; }

    public IReadOnlyDictionary<int, ActivityTaskDefinition> ActivityTasksBySectionId { get; }

    public IReadOnlyDictionary<int, ActivityTaskDefinition> ActivityTasksById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ActivityTaskDefinition>> ActivityTasksByType { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<ActivityTaskDefinition>> ActivityTasksByTitle { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<MeltingTypeRule>> MeltingTypeRulesBySourceFile { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<MeltingTypeRule>> MeltingTypeRulesByRuleType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<MeltingTypeRule>> MeltingTypeRulesByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<LotteryPrize>> LotteryPrizesByType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<LotteryPrize>> LotteryPrizesByLevel { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<LotteryPrize>> LotteryPrizesByItemTypeId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<LotteryPrize>> LotteryPrizesByItemName { get; }

    public IReadOnlyDictionary<int, ArenaRecord> ArenaRecordsByPlayerId { get; }

    public IReadOnlyDictionary<int, ArenaRecord> ArenaRecordsByRank { get; }

    public IReadOnlyDictionary<int, TeamArenaRecord> TeamArenaRecordsByPlayerId { get; }

    public IReadOnlyDictionary<int, TeamArenaRecord> TeamArenaRecordsByRank { get; }

    public IReadOnlyDictionary<(int Group, int Slot), PkTournamentRecord> ElitePkRecordsByGroupAndSlot { get; }

    public IReadOnlyDictionary<(int Group, int Slot), PkTournamentRecord> TeamElitePkRecordsByGroupAndSlot { get; }

    public IReadOnlyDictionary<(int Group, int Slot), PkTournamentRecord> SkillTeamPkRecordsByGroupAndSlot { get; }

    public IReadOnlyDictionary<(int ClassId, int Slot), ClassPkWarRecord> ClassPkWarRecordsByClassAndSlot { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<CombatGearRankEntry>> CombatGearRanksByCharacterId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<PrestigeRankEntry>> PrestigeRanksByCharacterId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<InnerPowerRankEntry>> InnerPowerRanksByCharacterId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<RuneRankingRecord>> RuneRankingsByCharacterId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<RuneRankingRecord>> RuneRankingsByCharacterName { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<StaticStatueRecord>> StaticStatuesByRecordId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<NewPlayerRewardRecord>> NewPlayerRewardsByIpAddress { get; }

    public IReadOnlyDictionary<int, VoteRecord> VotesByCharacterId { get; }

    public IReadOnlyDictionary<string, CouplesPkRecord> CouplesPkRecordsByPlayerName { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SocialAssociationRecord>> SocialAssociationsByOwnerId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SocialAssociationRecord>> SocialAssociationsByRelationType { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SocialAssociationRecord>> SocialAssociationsByTargetId { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<SocialAssociationRecord>> SocialAssociationsByTargetName { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<ClassLevelStat>> ClassLevelStatsByClassName { get; }

    public IReadOnlyDictionary<(string ClassName, int Level), ClassLevelStat> ClassLevelStatsByClassAndLevel { get; }

    public IReadOnlyDictionary<int, ProfessionLevelUp> ProfessionLevelUpsByProfessionCode { get; }

    public IReadOnlyDictionary<int, ProfessionLevelBenefit> ProfessionLevelBenefitsByProfessionCode { get; }

    public IReadOnlyDictionary<(int ProfessionGroup, int Rank), ProfessionTitleBenefit> ProfessionTitleBenefitsByProfessionAndRank { get; }

    public IReadOnlyDictionary<int, QuestTaskType> QuestTaskTypesById { get; }

    public IReadOnlyDictionary<int, QuestMission> QuestMissionsBySectionId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<QuestMission>> QuestMissionsByMissionId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<QuestMission>> QuestMissionsByTypeId { get; }

    public IReadOnlyDictionary<int, QuizQuestionEntry> QuizQuestionsByPosition { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<QuizQuestionEntry>> QuizQuestionsByQuestion { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<QuizQuestionEntry>> QuizQuestionsByCorrectOption { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<Npc>> NpcsById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<Npc>> NpcsByMapId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SobNpc>> SobNpcsById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<SobNpc>> SobNpcsByMapId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<Portal>> PortalsBySourceMapId { get; }

    public IReadOnlyDictionary<int, LotteryDictionaryEntry> LotteryDictionaryById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<LotteryDictionaryEntry>> LotteryDictionaryByType { get; }

    public IReadOnlyDictionary<int, CollectionRewardTier> CollectionRewardTiersById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<CollectionRewardTier>> CollectionRewardTiersByCollectionId { get; }

    public IReadOnlyDictionary<(int CollectionId, int MinInclusive), CollectionRewardTier> CollectionRewardTierByCollectionIdAndMinValue { get; }

    public IReadOnlyDictionary<int, TaskRewardTypeEntry> TaskRewardTypesById { get; }

    public IReadOnlyDictionary<int, TaskRewardTypeIniEntry> TaskRewardTypeIniEntriesById { get; }

    public IReadOnlyDictionary<int, NeiGongSecret> NeiGongSecretsById { get; }

    public IReadOnlyDictionary<int, NeiGongType> NeiGongTypesByTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<NeiGongType>> NeiGongTypesBySecretId { get; }

    public IReadOnlyDictionary<(int SecretId, int Slot), NeiGongType> NeiGongTypeBySecretIdAndSlot { get; }

    public IReadOnlyDictionary<int, SubProfessionEntry> SubProfessionsById { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<SubProfessionEntry>> SubProfessionsByTitle { get; }

    public IReadOnlyDictionary<int, SyndicateLevel> SyndicateLevelsByLevel { get; }

    public IReadOnlyDictionary<int, StageGoalStage> StageGoalStagesById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<StageGoalTask>> StageGoalTasksByStageId { get; }

    public IReadOnlyDictionary<(int StageId, int GoalId), StageGoalTask> StageGoalTasksByStageAndGoalId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ShopDefinition>> ShopsById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ShopItem>> ShopItemsByShopId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<ShopItem>> ShopItemsByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HonorShopItem>> HonorShopItemsByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<HonorShopRecommendation>> HonorShopRecommendationsByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<PointShopItem>> RacePointShopItemsByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<PointShopRecommendation>> RacePointShopRecommendationsByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<PointShopItem>> GoldenLeagueShopItemsByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<PointShopRecommendation>> GoldenLeagueShopRecommendationsByItemTypeId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<BoothDefinition>> BoothsById { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<BoothItem>> BoothItemsByBoothId { get; }

    public IReadOnlyDictionary<int, IReadOnlyList<BoothItem>> BoothItemsByItemTypeId { get; }
}
