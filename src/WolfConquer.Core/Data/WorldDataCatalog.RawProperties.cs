namespace WolfConquer.Core.Data;

public sealed partial class WorldDataCatalog
{
    public SeedManifest Manifest { get; }

    public IReadOnlyList<ClientConfigRecord> ClientConfigs { get; }

    public IReadOnlyList<ItemType> Items { get; }

    public IReadOnlyList<ItemTypeSub> ItemTypeSubs { get; }

    public IReadOnlyList<ItemAddBonus> ItemAddBonuses { get; }

    public IReadOnlyList<LevelExperience> LevelExperience { get; }

    public IReadOnlyList<RuneLevelExperience> RuneLevelExperience { get; }

    public IReadOnlyList<RuneStorageAttribute> RuneStorageAttributes { get; }

    public IReadOnlyList<ItemRefineEffect> ItemRefineEffects { get; }

    public IReadOnlyList<ItemRefineEffectEx> ItemRefineEffectEx { get; }

    public IReadOnlyList<ItemRefineUpgrade> ItemRefineUpgrades { get; }

    public IReadOnlyList<TransformInfo> TransformInfo { get; }

    public IReadOnlyList<Furniture> Furniture { get; }

    public IReadOnlyList<HairColorType> HairColorTypes { get; }

    public IReadOnlyList<HairFaceStorageType> HairFaceStorageTypes { get; }

    public IReadOnlyList<CoatStorageType> CoatStorageTypes { get; }

    public IReadOnlyList<CoatStorageAttribute> CoatStorageAttributes { get; }

    public IReadOnlyList<CoatColorRule> CoatColorRules { get; }

    public IReadOnlyList<XuanbaoAdditionAttribute> XuanbaoAdditionAttributes { get; }

    public IReadOnlyList<XuanbaoComposeAttributeLimit> XuanbaoComposeAttributeLimits { get; }

    public IReadOnlyList<XuanbaoRandomAttribute> XuanbaoRandomAttributes { get; }

    public IReadOnlyList<ChristmasNpc> ChristmasNpcs { get; }

    public IReadOnlyList<TutorType> TutorTypes { get; }

    public IReadOnlyList<TutorBattleLimitType> TutorBattleLimitTypes { get; }

    public IReadOnlyList<BeastAttribute> BeastAttributes { get; }

    public IReadOnlyList<FateExperience> FateExperience { get; }

    public IReadOnlyList<SpiritRate> SpiritRates { get; }

    public IReadOnlyList<HundredWeapon> HundredWeapons { get; }

    public IReadOnlyList<GouyuType> GouyuTypes { get; }

    public IReadOnlyList<GouyuImmortal> GouyuImmortals { get; }

    public IReadOnlyList<SpecialEffectAttribute> SpecialEffectAttributes { get; }

    public IReadOnlyList<DaoqiDictType> DaoqiDictTypes { get; }

    public IReadOnlyList<SyndicateFormationType> SyndicateFormationTypes { get; }

    public IReadOnlyList<SyndicateSkillType> SyndicateSkillTypes { get; }

    public IReadOnlyList<DisdainRule> DisdainRules { get; }

    public IReadOnlyList<JiangHuAttribute> JiangHuAttributes { get; }

    public IReadOnlyList<JiangHuCultivateStatus> JiangHuCultivateStatuses { get; }

    public IReadOnlyList<RefineryBox> RefineryBoxes { get; }

    public IReadOnlyList<RefinerySoul> RefinerySouls { get; }

    public IReadOnlyList<RefineryMaterial> RefineryMaterials { get; }

    public IReadOnlyList<StateCountFileData> StateCountFiles { get; }

    public IReadOnlyList<BinarySeedFile> BinarySeedFiles { get; }

    public IReadOnlyList<EmptySeedFile> EmptySeedFiles { get; }

    public IReadOnlyList<BackupSeedFile> BackupSeedFiles { get; }

    public IReadOnlyList<PokerTable> PokerTables { get; }

    public IReadOnlyList<RouletteTable> RouletteTables { get; }

    public IReadOnlyList<NpcTimeEntry> NpcTimeEntries { get; }

    public ActivityTaskCatalog ActivityTasks { get; }

    public IReadOnlyList<MeltingTypeRule> MeltingTypeRules { get; }

    public IReadOnlyList<LotteryPrize> LotteryPrizes { get; }

    public IReadOnlyList<ArenaRecord> ArenaRecords { get; }

    public IReadOnlyList<TeamArenaRecord> TeamArenaRecords { get; }

    public IReadOnlyList<PkTournamentRecord> ElitePkRecords { get; }

    public IReadOnlyList<PkTournamentRecord> TeamElitePkRecords { get; }

    public IReadOnlyList<PkTournamentRecord> SkillTeamPkRecords { get; }

    public IReadOnlyList<ClassPkWarRecord> ClassPkWarRecords { get; }

    public IReadOnlyList<CombatGearRankEntry> CombatGearRanks { get; }

    public IReadOnlyList<PrestigeRankEntry> PrestigeRanks { get; }

    public IReadOnlyList<InnerPowerRankEntry> InnerPowerRanks { get; }

    public IReadOnlyList<RuneRankingRecord> RuneRankings { get; }

    public IReadOnlyList<StaticStatueRecord> StaticStatues { get; }

    public IReadOnlyList<NewPlayerRewardRecord> NewPlayerRewards { get; }

    public IReadOnlyList<VoteRecord> Votes { get; }

    public IReadOnlyList<CouplesPkRecord> CouplesPkRecords { get; }

    public IReadOnlyList<SocialAssociationRecord> SocialAssociations { get; }

    public GuildWarData GuildWarInfo { get; }

    public GuildWarData SuperGuildWarInfo { get; }

    public IReadOnlyList<ClassLevelStat> ClassLevelStats { get; }

    public IReadOnlyList<ProfessionLevelUp> ProfessionLevelUps { get; }

    public IReadOnlyList<ProfessionLevelBenefit> ProfessionLevelBenefits { get; }

    public IReadOnlyList<ProfessionTitleBenefit> ProfessionTitleBenefits { get; }

    public QuestInfoMetadata QuestInfo { get; }

    public IReadOnlyList<QuestTaskType> QuestTaskTypes { get; }

    public IReadOnlyList<QuestMission> QuestMissions { get; }

    public IReadOnlyList<QuizQuestionEntry> QuizQuestions { get; }

    public IReadOnlyList<MagicType> Magic { get; }

    public IReadOnlyList<MagicEffect> MagicEffects { get; }

    public IReadOnlyList<MagicTypeOperationEntry> MagicTypeOperations { get; }

    public IReadOnlyList<TitleType> TitleTypes { get; }

    public IReadOnlyList<CombatGear> CombatGear { get; }

    public IReadOnlyList<Trap> Traps { get; }

    public IReadOnlyList<Npc> Npcs { get; }

    public IReadOnlyList<SobNpc> SobNpcs { get; }

    public IReadOnlyList<Portal> Portals { get; }

    public IReadOnlyList<MapMetadata> Maps { get; }

    public IReadOnlyList<MapName> MapNames { get; }

    public IReadOnlyList<LegacyGameMapEntry> LegacyGameMaps { get; }

    public IReadOnlyList<Monster> Monsters { get; }

    public IReadOnlyList<MonsterSpawn> MonsterSpawns { get; }

    public IReadOnlyList<LotteryDictionaryEntry> LotteryDictionary { get; }

    public IReadOnlyList<CollectionRewardTier> CollectionRewardTiers { get; }

    public IReadOnlyList<TaskRewardTypeEntry> TaskRewardTypes { get; }

    public IReadOnlyList<TaskRewardTypeIniEntry> TaskRewardTypeIniEntries { get; }

    public NeiGongHeader NeiGongHeader { get; }

    public IReadOnlyList<NeiGongSecret> NeiGongSecrets { get; }

    public IReadOnlyList<NeiGongType> NeiGongTypes { get; }

    public IReadOnlyList<SubProfessionEntry> SubProfessions { get; }

    public IReadOnlyList<SyndicateLevel> SyndicateLevels { get; }

    public StageGoalInfo StageGoalInfo { get; }

    public IReadOnlyList<StageGoalStage> StageGoalStages { get; }

    public IReadOnlyList<StageGoalTask> StageGoalTasks { get; }

    public IReadOnlyList<ShopDefinition> Shops { get; }

    public IReadOnlyList<ShopItem> ShopItems { get; }

    public IReadOnlyList<ExchangeShopRow> ExchangeShopGoods { get; }

    public IReadOnlyList<ExchangeShopRow> ExchangeShopGoodsEx { get; }

    public IReadOnlyList<ExchangeShopRow> ExchangeShopLevels { get; }

    public IReadOnlyList<HonorShopItem> HonorShopItems { get; }

    public IReadOnlyList<HonorShopRecommendation> HonorShopRecommendations { get; }

    public IReadOnlyList<PointShopItem> RacePointShopItems { get; }

    public IReadOnlyList<PointShopRecommendation> RacePointShopRecommendations { get; }

    public IReadOnlyList<PointShopItem> GoldenLeagueShopItems { get; }

    public IReadOnlyList<PointShopRecommendation> GoldenLeagueShopRecommendations { get; }

    public IReadOnlyDictionary<string, string> GoldenLeagueShopConfig { get; }

    public IReadOnlyList<BoothDefinition> Booths { get; }

    public IReadOnlyList<BoothItem> BoothItems { get; }

    public IReadOnlyList<WorldDataDiagnostic> Diagnostics { get; }
}
