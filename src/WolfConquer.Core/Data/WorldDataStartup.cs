using System.Text.Json;

namespace WolfConquer.Core.Data;

public static class WorldDataStartup
{
    public static async Task<WorldDataCatalog?> TryLoadAsync(
        TextWriter log,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(log);

        var options = WorldDataOptions.FromEnvironment();
        var rootPath = Path.GetFullPath(options.RootPath);
        await log.WriteLineAsync($"World data seed root: {rootPath}").ConfigureAwait(false);

        if (!File.Exists(Path.Combine(rootPath, "manifest.json")))
        {
            await log.WriteLineAsync(
                    $"World data not loaded; manifest.json is missing. Run DataTool export or set {WorldDataOptions.SeedJsonEnvironmentVariable}.")
                .ConfigureAwait(false);
            return null;
        }

        WorldDataCatalog catalog;
        try
        {
            catalog = await new JsonWorldDataLoader(new WorldDataOptions(rootPath))
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            await log.WriteLineAsync($"World data not loaded; {ex.Message}").ConfigureAwait(false);
            return null;
        }

        await log.WriteLineAsync(
                "World data loaded: " +
                $"clientConfigs={catalog.ClientConfigs.Count}, " +
                $"items={catalog.Items.Count}, " +
                $"itemTypeSubs={catalog.ItemTypeSubs.Count}, " +
                $"itemAddBonuses={catalog.ItemAddBonuses.Count}, " +
                $"levelExperience={catalog.LevelExperience.Count}, " +
                $"runeLevelExperience={catalog.RuneLevelExperience.Count}, " +
                $"runeStorageAttributes={catalog.RuneStorageAttributes.Count}, " +
                $"itemRefineEffects={catalog.ItemRefineEffects.Count}, " +
                $"itemRefineEffectEx={catalog.ItemRefineEffectEx.Count}, " +
                $"itemRefineUpgrades={catalog.ItemRefineUpgrades.Count}, " +
                $"transformInfo={catalog.TransformInfo.Count}, " +
                $"furniture={catalog.Furniture.Count}, " +
                $"hairColorTypes={catalog.HairColorTypes.Count}, " +
                $"hairFaceStorageTypes={catalog.HairFaceStorageTypes.Count}, " +
                $"coatStorageTypes={catalog.CoatStorageTypes.Count}, " +
                $"coatStorageAttributes={catalog.CoatStorageAttributes.Count}, " +
                $"coatColorRules={catalog.CoatColorRules.Count}, " +
                $"xuanbaoAdditionAttributes={catalog.XuanbaoAdditionAttributes.Count}, " +
                $"xuanbaoComposeAttributeLimits={catalog.XuanbaoComposeAttributeLimits.Count}, " +
                $"xuanbaoRandomAttributes={catalog.XuanbaoRandomAttributes.Count}, " +
                $"christmasNpcs={catalog.ChristmasNpcs.Count}, " +
                $"tutorTypes={catalog.TutorTypes.Count}, " +
                $"tutorBattleLimitTypes={catalog.TutorBattleLimitTypes.Count}, " +
                $"beastAttributes={catalog.BeastAttributes.Count}, " +
                $"fateExperience={catalog.FateExperience.Count}, " +
                $"spiritRates={catalog.SpiritRates.Count}, " +
                $"hundredWeapons={catalog.HundredWeapons.Count}, " +
                $"gouyuTypes={catalog.GouyuTypes.Count}, " +
                $"gouyuImmortals={catalog.GouyuImmortals.Count}, " +
                $"specialEffectAttributes={catalog.SpecialEffectAttributes.Count}, " +
                $"daoqiDictTypes={catalog.DaoqiDictTypes.Count}, " +
                $"syndicateFormationTypes={catalog.SyndicateFormationTypes.Count}, " +
                $"syndicateSkillTypes={catalog.SyndicateSkillTypes.Count}, " +
                $"disdainRules={catalog.DisdainRules.Count}, " +
                $"jiangHuAttributes={catalog.JiangHuAttributes.Count}, " +
                $"jiangHuCultivateStatuses={catalog.JiangHuCultivateStatuses.Count}, " +
                $"refineryBoxes={catalog.RefineryBoxes.Count}, " +
                $"refinerySouls={catalog.RefinerySouls.Count}, " +
                $"refineryMaterials={catalog.RefineryMaterials.Count}, " +
                $"stateCountFiles={catalog.StateCountFiles.Count}, " +
                $"binarySeedFiles={catalog.BinarySeedFiles.Count}, " +
                $"emptySeedFiles={catalog.EmptySeedFiles.Count}, " +
                $"backupSeedFiles={catalog.BackupSeedFiles.Count}, " +
                $"pokerTables={catalog.PokerTables.Count}, " +
                $"rouletteTables={catalog.RouletteTables.Count}, " +
                $"npcTimeEntries={catalog.NpcTimeEntries.Count}, " +
                $"activityTasks={catalog.ActivityTasks.Tasks.Count}, " +
                $"meltingTypeRules={catalog.MeltingTypeRules.Count}, " +
                $"lotteryPrizes={catalog.LotteryPrizes.Count}, " +
                $"arenaRecords={catalog.ArenaRecords.Count}, " +
                $"teamArenaRecords={catalog.TeamArenaRecords.Count}, " +
                $"elitePkRecords={catalog.ElitePkRecords.Count}, " +
                $"teamElitePkRecords={catalog.TeamElitePkRecords.Count}, " +
                $"skillTeamPkRecords={catalog.SkillTeamPkRecords.Count}, " +
                $"classPkWarRecords={catalog.ClassPkWarRecords.Count}, " +
                $"combatGearRanks={catalog.CombatGearRanks.Count}, " +
                $"prestigeRanks={catalog.PrestigeRanks.Count}, " +
                $"innerPowerRanks={catalog.InnerPowerRanks.Count}, " +
                $"runeRankings={catalog.RuneRankings.Count}, " +
                $"staticStatues={catalog.StaticStatues.Count}, " +
                $"newPlayerRewards={catalog.NewPlayerRewards.Count}, " +
                $"votes={catalog.Votes.Count}, " +
                $"couplesPkRecords={catalog.CouplesPkRecords.Count}, " +
                $"socialAssociations={catalog.SocialAssociations.Count}, " +
                $"guildWarInfoEntries={catalog.GuildWarInfo.Entries.Count}, " +
                $"superGuildWarInfoEntries={catalog.SuperGuildWarInfo.Entries.Count}, " +
                $"classLevelStats={catalog.ClassLevelStats.Count}, " +
                $"professionLevelUps={catalog.ProfessionLevelUps.Count}, " +
                $"professionLevelBenefits={catalog.ProfessionLevelBenefits.Count}, " +
                $"professionTitleBenefits={catalog.ProfessionTitleBenefits.Count}, " +
                $"questTaskTypes={catalog.QuestTaskTypes.Count}, " +
                $"questMissions={catalog.QuestMissions.Count}, " +
                $"quizQuestions={catalog.QuizQuestions.Count}, " +
                $"magic={catalog.Magic.Count}, " +
                $"magicEffects={catalog.MagicEffects.Count}, " +
                $"magicTypeOperations={catalog.MagicTypeOperations.Count}, " +
                $"titleTypes={catalog.TitleTypes.Count}, " +
                $"combatGear={catalog.CombatGear.Count}, " +
                $"traps={catalog.Traps.Count}, " +
                $"npcs={catalog.Npcs.Count}, " +
                $"sobNpcs={catalog.SobNpcs.Count}, " +
                $"portals={catalog.Portals.Count}, " +
                $"maps={catalog.Maps.Count}, " +
                $"mapNames={catalog.MapNames.Count}, " +
                $"monsters={catalog.Monsters.Count}, " +
                $"monsterSpawns={catalog.MonsterSpawns.Count}, " +
                $"lotteryDictionary={catalog.LotteryDictionary.Count}, " +
                $"collectionRewardTiers={catalog.CollectionRewardTiers.Count}, " +
                $"taskRewardTypes={catalog.TaskRewardTypes.Count}, " +
                $"taskRewardTypeIniEntries={catalog.TaskRewardTypeIniEntries.Count}, " +
                $"neiGongSecrets={catalog.NeiGongSecrets.Count}, " +
                $"neiGongTypes={catalog.NeiGongTypes.Count}, " +
                $"subProfessions={catalog.SubProfessions.Count}, " +
                $"syndicateLevels={catalog.SyndicateLevels.Count}, " +
                $"stageGoalStages={catalog.StageGoalStages.Count}, " +
                $"stageGoalTasks={catalog.StageGoalTasks.Count}, " +
                $"shops={catalog.Shops.Count}, " +
                $"shopItems={catalog.ShopItems.Count}, " +
                $"honorShopItems={catalog.HonorShopItems.Count}, " +
                $"racePointShopItems={catalog.RacePointShopItems.Count}, " +
                $"goldenLeagueShopItems={catalog.GoldenLeagueShopItems.Count}, " +
                $"booths={catalog.Booths.Count}, " +
                $"boothItems={catalog.BoothItems.Count}")
            .ConfigureAwait(false);

        if (catalog.Diagnostics.Count > 0)
        {
            await log.WriteLineAsync($"World data diagnostics: {catalog.Diagnostics.Count}").ConfigureAwait(false);
            foreach (var diagnostic in catalog.Diagnostics)
            {
                await log.WriteLineAsync(
                        $"World data diagnostic {diagnostic.Code}: {diagnostic.Message}")
                    .ConfigureAwait(false);
            }
        }

        return catalog;
    }
}
