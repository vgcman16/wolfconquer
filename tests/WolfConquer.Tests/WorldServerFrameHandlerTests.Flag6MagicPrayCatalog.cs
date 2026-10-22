using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private static WorldDataCatalog CreateFlag6MagicPrayCatalog()
    {
        // Exact bounded metadata from preserved DBServer/magictype.txt:57.
        // Keep the trailing empty field. No private seed directory or game asset is read.
        const string prayRow = "1100@@1100@@7@@Pray@@0@@0@@0@@0@@0@@1000@@0@@0@@100@@0@@0@@12@@0@@255@@0@@0@@70@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@";
        var pray = MagicTypeParser.ParseLine(prayRow, expectedFieldCount: 54);
        // Exact scalar map metadata from preserved DBServer/maps/1015.ini.
        var island = MapMetadataParser.Parse(IniDocument.Parse("""
            [info]
            id=1015
            name=Island
            mapdoc=1015
            type=8192
            portal0_x=717
            portal0_y=577
            reborn_map=1015
            reborn_portal=0
            res_lev=0
            link_map=0
            link_x=0
            link_y=0
            color=4294967295
            """), "fixture://flag6-pray/maps/1015.ini");
        var world = WorldDataCatalog.Create(
            new SeedManifest(DateTimeOffset.UnixEpoch, "fixture://flag6-native-pray",
                new Dictionary<string, int> { ["magic"] = 1, ["maps"] = 1 }),
            clientConfigs: [],
            items: [],
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
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://flag6-pray/activity"),
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
            guildWarInfo: EmptyGuildWarData("flag6-pray/guildwar"),
            superGuildWarInfo: EmptyGuildWarData("flag6-pray/superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [pray],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [island],
            mapNames: [],
            monsters: [],
            monsterSpawns: [],
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://flag6-pray/neigong", 0, "", new Dictionary<string, string>()),
            neiGongSecrets: [],
            neiGongTypes: [],
            subProfessions: [],
            syndicateLevels: [],
            stageGoalInfo: new StageGoalInfo(0),
            stageGoalStages: [],
            stageGoalTasks: [],
            shops: [],
            shopItems: [],
            exchangeShopGoods: [],
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

        Assert.Same(pray, Assert.Single(world.Magic));
        Assert.Same(pray, world.MagicById[1100]); Assert.Same(pray, Assert.Single(world.MagicByTypeId[1100]));
        Assert.Equal(1100, pray.Id); Assert.Equal(1100, pray.TypeId); Assert.Equal("Pray", pray.Name);
        Assert.Equal("255", pray.Fields[17]); Assert.Equal("1", pray.Fields[34]);
        Assert.Equal("1", pray.Fields[52]); Assert.Equal("", pray.Fields[53]);
        Assert.True(LegacyMagicRuntime.TryResolveSpell(world, 1100, 0, out var runtime));
        Assert.Equal((ushort)1100, runtime.Id); Assert.Equal(LegacyMagicSort.DetachStatus, runtime.Sort);
        Assert.Equal((byte)0, runtime.Level); Assert.Equal((ushort)1000, runtime.UseMana);
        Assert.Equal((byte)12, runtime.Range); Assert.Equal((ushort)70, runtime.NeedLevel);
        Assert.Equal(0u, runtime.RawDamage); Assert.Equal(100u, runtime.Damage);
        Assert.Equal(0u, runtime.Duration); Assert.Equal(0u, runtime.Experience);
        Assert.Equal((byte)0, runtime.UseStamina); Assert.Equal((ushort)0, runtime.CoolDown); Assert.Equal(0, runtime.ColdTime);
        Assert.True(LegacyMagicSupportRuntime.TryResolveSupportSpell(world, 1100, 0, out var support));
        Assert.Equal(100, support.Rate); Assert.Equal(0, support.Status); Assert.Equal((byte)0, support.MaxTargets);
        var classification = LegacyMagicSupportRuntime.Classify(support);
        Assert.True(classification.Supported); Assert.Equal(LegacyMagicSupportKind.Revive, classification.Kind);
        Assert.Same(island, Assert.Single(world.Maps)); Assert.Same(island, world.MapsById[1015]);
        Assert.Equal("Island", island.Name); Assert.Equal(1015, island.MapDoc); Assert.Equal(8192L, island.Type);
        Assert.Equal(717, island.Portal0X); Assert.Equal(577, island.Portal0Y);
        Assert.Equal(1015, island.RebornMap); Assert.Equal(0, island.RebornPortal); Assert.Equal(4_294_967_295L, island.Color);
        Assert.Equal(new LegacyMapStatusPacket(1015, 1015, 8192), LegacyMapStatusPacket.FromMap(1015, world.MapsById));
        return world;
    }
}
