using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyKineticSparkPassiveMonsterRuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const ushort KineticSparkSpellId = 11_590;
    private const ushort ArcherPreProcSpellId = 18_750;
    private const ushort CombatGearAttackSpellId = 17_400;
    private const uint CombatGearStoneCrackerItemId = 401;
    private const byte KineticSparkSort = 67;
    private const byte BombSort = 5;
    private const int KineticSparkStatusFlag = 0x93;
    private const int ArcherClassCode = 4_005;
    private const int NinjaClassCode = 5_005;
    private const int NinjaSageSpeedModeFlag = 0x111;
    private const string RuntimeLegacyKineticSparkPassiveRateRollAttribute = "LegacyKineticSparkPassiveRateRoll";
    private const string RuntimeLegacyArcherPreProcRateRollAttribute = "LegacyArcherPreProcRateRoll";
    private const string RuntimeLegacyNinjaSageModeRollAttribute = "LegacyNinjaSageModeRoll";
    private const string RuntimeLegacyNinjaLevelsAttribute = "LegacyNinjaLevels";
    private const int MapId = 1_015;
    private const ushort PlayerX = 205;
    private const ushort PlayerY = 230;
    private const ushort PrimaryMonsterX = 210;
    private const ushort PrimaryMonsterY = 230;
    private const ushort NearbyMonsterX = 213;
    private const ushort NearbyMonsterY = 230;
    private const ushort OutsideMonsterX = 215;
    private const ushort OutsideMonsterY = 230;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task HandlerMonsterArcherAttackWithActiveKineticSparkChainsToNearbyMonsters()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var monsterFamily = CreateTrainingMonsterFamily();
        var spawns = new[]
        {
            CreateMonsterSpawn(3, PrimaryMonsterX, PrimaryMonsterY),
            CreateMonsterSpawn(4, NearbyMonsterX, NearbyMonsterY),
            CreateMonsterSpawn(5, OutsideMonsterX, OutsideMonsterY),
        };
        var worldData = CreateWorldData([CreateKineticSparkMagicRow()], [monsterFamily], spawns);
        var primary = CreateMonsterInstance(worldData, spawns[0], monsterFamily);
        var nearby = CreateMonsterInstance(worldData, spawns[1], monsterFamily);
        var outside = CreateMonsterInstance(worldData, spawns[2], monsterFamily);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession();
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(CreateMonsterAttack(primary, attackType)).ToFrameBytes()));

        var attackResponses = ParseAttacks(sent);

        var spark = Assert.Single(
            ParseSpellAnimations(sent),
            animation => animation.SpellId == KineticSparkSpellId);
        Assert.Equal(PlayerUid, spark.AttackerUid);
        Assert.Equal(0U, spark.AttackedUid);
        Assert.Equal(PrimaryMonsterX, spark.X);
        Assert.Equal(PrimaryMonsterY, spark.Y);
        Assert.Contains(spark.Targets, target => target.Uid == primary.Uid);
        Assert.Contains(spark.Targets, target => target.Uid == nearby.Uid);
        Assert.DoesNotContain(spark.Targets, target => target.Uid == outside.Uid);
        Assert.All(spark.Targets, target => Assert.True(target.Hit));
        Assert.DoesNotContain(
            attackResponses,
            attack => attack.Query.AttackType == attackType &&
                attack.Query.OpponentUid == primary.Uid);
        Assert.True(
            sent.Count > attackResponses.Count,
            $"Expected a KineticSpark passive effect beyond ordinary MsgAttack responses. SentFrames={sent.Count}. Log={log}");
    }

    [Fact]
    public async Task HandlerMonsterArcherAttackWithActiveKineticSparkTriggersReceiveAttackMonsterNinjaSageMode()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        const uint duration = 10;
        var monsterFamily = CreateTrainingMonsterFamily();
        var spawn = CreateMonsterSpawn(12, PrimaryMonsterX, PrimaryMonsterY);
        var worldData = CreateWorldData(
            [CreateKineticSparkMagicRow()],
            [monsterFamily],
            [spawn],
            [CreateGouyuImmortal(level: 17, chance: 0, rate: 5_000, seconds: checked((int)duration))]);
        var primary = CreateMonsterInstance(worldData, spawn, monsterFamily);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
                [RuntimeLegacyNinjaSageModeRollAttribute] = "0",
                [RuntimeLegacyNinjaLevelsAttribute] = "17",
            },
            classCode: NinjaClassCode);
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(CreateMonsterAttack(primary, attackType)).ToFrameBytes()));

        var spark = Assert.Single(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        var sparkTarget = Assert.Single(spark.Targets, target => target.Uid == primary.Uid);
        Assert.True(sparkTarget.Hit);
        Assert.True(sparkTarget.Damage > 0);
        Assert.True(monsterStates.TryGetVisibleInstance(primary, DateTimeOffset.UtcNow, out var damagedMonster));
        Assert.True(damagedMonster.Hitpoints < primary.MaxLife);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, NinjaSageSpeedModeFlag));

        AssertLegacyStatusFlagUpdate(sent, PlayerUid, NinjaSageSpeedModeFlag);
        AssertLegacyArchiveSkillUpdate(sent, PlayerUid, NinjaSageSpeedModeFlag, duration);

        var output = log.ToString();
        Assert.Contains("ReceiveAttack.Monster", output);
        Assert.Contains("Ninja SageMode", output);
        Assert.Contains("WorldServer handled legacy KineticSpark passive monster proc", output);
    }

    [Fact]
    public async Task HandlerMonsterArcherAttackWithCloseArcherPreProcFiresBeforeKineticSparkAndSuppressesFallback()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var monsterFamily = CreateTrainingMonsterFamily();
        var spawns = new[]
        {
            CreateMonsterSpawn(6, PlayerX + 2, PlayerY),
            CreateMonsterSpawn(7, PlayerX + 4, PlayerY),
        };
        var worldData = CreateWorldData(
            [CreateKineticSparkMagicRow(), CreateVisibleMonsterMagicRow(ArcherPreProcSpellId)],
            [monsterFamily],
            spawns);
        var primary = CreateMonsterInstance(worldData, spawns[0], monsterFamily);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(ArcherPreProcSpellId),
            ],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyArcherPreProcRateRollAttribute + "." + ArcherPreProcSpellId] = "0",
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(CreateMonsterAttack(primary, attackType)).ToFrameBytes()));

        var animations = ParseSpellAnimations(sent);
        var preProc = Assert.Single(animations, animation => animation.SpellId == ArcherPreProcSpellId);
        Assert.Equal(PlayerUid, preProc.AttackerUid);
        Assert.Equal(primary.X, preProc.X);
        Assert.Equal(primary.Y, preProc.Y);
        Assert.Contains(preProc.Targets, target => target.Uid == primary.Uid);
        Assert.DoesNotContain(animations, animation => animation.SpellId == KineticSparkSpellId);
        Assert.DoesNotContain(
            ParseAttacks(sent),
            attack => attack.Query.AttackType == attackType &&
                attack.Query.OpponentUid == primary.Uid);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(0, updatedSession.LegacyRandomSpell);
        Assert.Contains("WorldServer handled legacy Archer pre-Kinetic monster proc", log.ToString());
    }

    [Fact]
    public async Task HandlerMonsterArcherAttackWithCombatGearSuppressesPreProcsKineticSparkAndFallback()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var monsterFamily = CreateTrainingMonsterFamily();
        var spawn = CreateMonsterSpawn(11, PlayerX + 2, PlayerY);
        var worldData = CreateWorldData(
            [
                CreateCombatGearMagicRow(),
                CreateKineticSparkMagicRow(),
                CreateVisibleMonsterMagicRow(ArcherPreProcSpellId),
            ],
            [monsterFamily],
            [spawn]);
        var primary = CreateMonsterInstance(worldData, spawn, monsterFamily);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(ArcherPreProcSpellId),
            ],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyCombatGearAttackSpellIndex"] = "0",
                [RuntimeLegacyArcherPreProcRateRollAttribute + "." + ArcherPreProcSpellId] = "0",
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            },
            classCode: ArcherClassCode,
            combatGear: CreateEquippedCombatGear());
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(CreateMonsterAttack(primary, attackType)).ToFrameBytes()));

        var animations = ParseSpellAnimations(sent);
        var combatGear = Assert.Single(animations, animation => animation.SpellId == CombatGearAttackSpellId);
        Assert.Equal(PlayerUid, combatGear.AttackerUid);
        Assert.Contains(combatGear.Targets, target => target.Uid == primary.Uid && target.Damage > 0);
        Assert.DoesNotContain(animations, animation => animation.SpellId is ArcherPreProcSpellId or KineticSparkSpellId);
        Assert.DoesNotContain(
            ParseAttacks(sent),
            attack => attack.Query.AttackType == attackType &&
                attack.Query.OpponentUid == primary.Uid);

        var output = log.ToString();
        Assert.Contains("WorldServer replaced legacy monster physical attack with CombatGear spell", output);
        Assert.Contains("spellId=" + CombatGearAttackSpellId.ToString(CultureInfo.InvariantCulture), output);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic monster proc", output);
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive monster proc", output);
    }

    [Fact]
    public async Task HandlerMonsterArcherAttackBeyondTwoTilesSkipsArcherPreProcAndStillAllowsKineticSpark()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var monsterFamily = CreateTrainingMonsterFamily();
        var spawns = new[]
        {
            CreateMonsterSpawn(8, PlayerX + 3, PlayerY),
            CreateMonsterSpawn(9, PlayerX + 5, PlayerY),
        };
        var worldData = CreateWorldData(
            [CreateKineticSparkMagicRow(), CreateVisibleMonsterMagicRow(ArcherPreProcSpellId)],
            [monsterFamily],
            spawns);
        var primary = CreateMonsterInstance(worldData, spawns[0], monsterFamily);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            [
                CreatePlayerSpell(KineticSparkSpellId),
                CreatePlayerSpell(ArcherPreProcSpellId),
            ],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyArcherPreProcRateRollAttribute + "." + ArcherPreProcSpellId] = "0",
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(CreateMonsterAttack(primary, attackType)).ToFrameBytes()));

        var animations = ParseSpellAnimations(sent);
        Assert.DoesNotContain(animations, animation => animation.SpellId == ArcherPreProcSpellId);
        var spark = Assert.Single(animations, animation => animation.SpellId == KineticSparkSpellId);
        Assert.Contains(spark.Targets, target => target.Uid == primary.Uid);
        Assert.DoesNotContain("WorldServer handled legacy Archer pre-Kinetic monster proc", log.ToString());
    }

    [Fact]
    public async Task HandlerMonsterArcherAttackBeyondDirectRangeSuppressesKineticSparkAndPhysicalFallback()
    {
        const MsgAttackType attackType = MsgAttackType.Archer;
        var monsterFamily = CreateTrainingMonsterFamily();
        var spawn = CreateMonsterSpawn(10, checked((ushort)(PlayerX + 19)), PlayerY);
        var worldData = CreateWorldData([CreateKineticSparkMagicRow()], [monsterFamily], [spawn]);
        var primary = CreateMonsterInstance(worldData, spawn, monsterFamily);
        var sessions = new WorldSessionStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            [CreatePlayerSpell(KineticSparkSpellId)],
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
            });
        sessions.Set(connection, session);

        await PacketHandlers.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                MonsterStates: monsterStates),
            ReadFrame(new MsgAttackPacket(CreateMonsterAttack(primary, attackType)).ToFrameBytes()));

        var output = log.ToString();
        Assert.Contains("WorldServer rejected legacy Archer", output);
        Assert.Contains("monster", output);
        Assert.Contains("distance=19", output);
        Assert.Contains("AttackID.Archer monster branch Base.GetDistance <= 18", output);
        Assert.DoesNotContain("WorldServer handled legacy KineticSpark passive monster proc", output);
        Assert.DoesNotContain("WorldServer handled monster combat", output);
        Assert.DoesNotContain(ParseSpellAnimations(sent), animation => animation.SpellId == KineticSparkSpellId);
        Assert.DoesNotContain(
            ParseAttacks(sent),
            attack => attack.Query.AttackType == attackType &&
                attack.Query.OpponentUid == primary.Uid);
        Assert.True(monsterStates.TryGetVisibleInstance(primary, DateTimeOffset.UtcNow, out var unchangedMonster));
        Assert.Equal(primary.MaxLife, unchangedMonster.Hitpoints);
    }

    private static MsgAttackQuery CreateMonsterAttack(LegacyMonsterInstance monster, MsgAttackType attackType)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: attackType,
            Damage: 500,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0);
    }

    private static WorldSession CreateSession(
        IReadOnlyList<PlayerSpellSnapshotEntry>? playerSpells = null,
        IReadOnlyDictionary<string, string>? attributes = null,
        int classCode = 142,
        CombatGearSnapshot? combatGear = null)
    {
        return new WorldSession(
            AccountId: 1,
            Username: "caster",
            CreateCharacter(attributes, classCode),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(playerSpells ?? [CreatePlayerSpell(KineticSparkSpellId)]),
            PlayerProficiencySnapshot.Empty,
            CombatGear: combatGear);
    }

    private static CharacterSnapshot CreateCharacter(
        IReadOnlyDictionary<string, string>? attributes = null,
        int classCode = 142)
    {
        var characterAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["StatusFlags"] = BuildStatusFlags(KineticSparkStatusFlag),
            [RuntimeLegacyKineticSparkPassiveRateRollAttribute] = "0",
        };
        if (attributes is not null)
        {
            foreach (var (key, value) in attributes)
            {
                characterAttributes[key] = value;
            }
        }

        return new CharacterSnapshot(
            Uid: PlayerUid,
            Name: "Caster",
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: classCode,
            FirstClass: classCode,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId,
            X: PlayerX,
            Y: PlayerY,
            PreviousMapId: MapId,
            PreviousMapX: PlayerX,
            PreviousMapY: PlayerY,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 300,
            AttributePoints: 0,
            MinHitPoints: 5_000,
            MinMana: 500,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://kinetic-spark-passive-monster-runtime",
            Attributes: characterAttributes,
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static CombatGearSnapshot CreateEquippedCombatGear()
    {
        return new CombatGearSnapshot(
            MasteryPoints: 0,
            TaoistReiki: 0,
            PirateExp: 0,
            Lottery: 0,
            Count: 1,
            CombatGearAirPowersSnapshot.Empty,
            [
                new CombatGearSnapshotItem(
                    CombatGearStoneCrackerItemId,
                    Level: 1,
                    Exp: 0,
                    Equip: 1,
                    Transform: 0,
                    MasteryScore: 0,
                    Animas: [[], []],
                    BlackJades: []),
            ],
            CombatGearBlackJadeBagSnapshot.Empty);
    }

    private static PlayerSpellSnapshotEntry CreatePlayerSpell(ushort spellId)
    {
        return new PlayerSpellSnapshotEntry(
            spellId,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static Monster CreateTrainingMonsterFamily()
    {
        return new Monster(
            901,
            "TrainingMonster",
            new Dictionary<string, string>
            {
                ["life"] = "70000",
                ["lookface"] = "901",
                ["level"] = "80",
                ["defense"] = "0",
                ["defense2"] = "10000",
                ["attack_range"] = "3",
            },
            []);
    }

    private static MonsterSpawn CreateMonsterSpawn(int generatorId, ushort x, ushort y)
    {
        return new MonsterSpawn(
            GeneratorId: generatorId,
            MapId,
            MonsterTypeId: 901,
            BoundX: x,
            BoundY: y,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 10,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: MapId,
            SourceMonsterName: "TrainingMonster",
            SourceRelativePath: Path.Combine(MapId.ToString(CultureInfo.InvariantCulture), "TrainingMonster", generatorId + ".ini"),
            Attributes: new Dictionary<string, string>
            {
                ["id"] = generatorId.ToString(CultureInfo.InvariantCulture),
                ["mapid"] = MapId.ToString(CultureInfo.InvariantCulture),
                ["npctype"] = "901",
            });
    }

    private static LegacyMonsterInstance CreateMonsterInstance(
        WorldDataCatalog worldData,
        MonsterSpawn spawn,
        Monster monsterFamily)
    {
        return Assert.Single(LegacyMonsterInstance.FromGenerator(
            spawn,
            monsterFamily,
            worldData.GetLegacyMonsterRuntimeUidBase(spawn)));
    }

    private static WorldDataCatalog CreateWorldData(
        IReadOnlyList<MagicType> magicRows,
        IReadOnlyList<Monster> monsters,
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        IReadOnlyList<GouyuImmortal>? gouyuImmortals = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-23T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://kinetic-spark-passive-monster-runtime",
                new Dictionary<string, int>()),
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
            gouyuImmortals: gouyuImmortals ?? [],
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
            activityTasks: new ActivityTaskCatalog(new ActivityTaskMetadata(0, "", 0, []), [], "fixture://activity"),
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
            guildWarInfo: EmptyGuildWarData("guildwar"),
            superGuildWarInfo: EmptyGuildWarData("superguildwar"),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: magicRows,
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: [],
            portals: [],
            maps: [],
            mapNames: [],
            monsters: monsters,
            monsterSpawns: monsterSpawns,
            lotteryDictionary: [],
            collectionRewardTiers: [],
            taskRewardTypes: [],
            taskRewardTypeIniEntries: [],
            neiGongHeader: new NeiGongHeader(0, [], "fixture://neigong", 0, "", new Dictionary<string, string>()),
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
    }

    private static GouyuImmortal CreateGouyuImmortal(int level, int chance, int rate, int seconds)
    {
        var values = Enumerable.Repeat(0, 15).ToArray();
        values[0] = level;
        values[5] = chance;
        values[6] = seconds;
        values[7] = rate;
        return new GouyuImmortal(
            Id: level,
            Values: values,
            SourceFile: "fixture://gouyu_immortal",
            LineNumber: 1,
            RawLine: "",
            Fields: []);
    }

    private static MagicType CreateKineticSparkMagicRow()
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = KineticSparkSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = KineticSparkSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = KineticSparkSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "KineticSpark";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "100";
        fields[13] = "0";
        fields[14] = "5";
        fields[15] = "8";
        fields[18] = "0";
        fields[20] = "0";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "0";
        fields[36] = "0";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(KineticSparkSpellId, KineticSparkSpellId, "KineticSpark", fields);
    }

    private static MagicType CreateCombatGearMagicRow()
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = CombatGearAttackSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = CombatGearAttackSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = BombSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "StoneCracker";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "120";
        fields[13] = "0";
        fields[14] = "1";
        fields[15] = "18";
        fields[18] = "0";
        fields[20] = "0";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "0";
        fields[36] = "0";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(CombatGearAttackSpellId, CombatGearAttackSpellId, "StoneCracker", fields);
    }

    private static MagicType CreateVisibleMonsterMagicRow(ushort spellId)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = BombSort.ToString(CultureInfo.InvariantCulture);
        fields[3] = "CrescentShadow";
        fields[8] = "0";
        fields[9] = "0";
        fields[10] = "120";
        fields[13] = "0";
        fields[14] = "1";
        fields[15] = "8";
        fields[18] = "0";
        fields[20] = "0";
        fields[22] = "0";
        fields[29] = "0";
        fields[33] = "0";
        fields[35] = "0";
        fields[36] = "0";
        fields[37] = "0";
        fields[44] = "0";
        fields[47] = "0";
        return new MagicType(spellId, spellId, "CrescentShadow", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData($"fixture://{name}", [], [], [], info: null, pole: null, conductors: []);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(
            localEndPoint: null,
            remoteEndPoint: new IPEndPoint(IPAddress.Loopback, 9958),
            sendAsync: (frame, _) =>
            {
                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }

    private static IReadOnlyList<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySpellAnimationPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<MsgAttackPacket> ParseAttacks(IEnumerable<byte[]> frames)
    {
        var packets = new List<MsgAttackPacket>();
        foreach (var bytes in frames)
        {
            if (MsgAttackPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseMsgUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertLegacyStatusFlagUpdate(IEnumerable<byte[]> frames, uint uid, int flag)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, flag)));
    }

    private static void AssertLegacyArchiveSkillUpdate(
        IEnumerable<byte[]> frames,
        uint uid,
        int flag,
        uint duration)
    {
        Assert.Contains(
            ParseMsgUpdates(frames),
            packet => packet.Uid == uid &&
                packet.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                    entry.Flag == checked((uint)flag) &&
                    entry.Time == duration));
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            IsLegacyStatusFlagSet(
                raw
                    .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(part => ulong.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                        ? value
                        : 0)
                    .ToArray(),
                flag);
    }

    private static bool IsLegacyStatusFlagSet(IReadOnlyList<ulong> values, int flag)
    {
        var wordIndex = flag / 32;
        return wordIndex < values.Count &&
            (values[wordIndex] & (1UL << (flag % 32))) != 0;
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var words = new ulong[18];
        foreach (var flag in flags)
        {
            words[flag / 32] |= 1UL << (flag % 32);
        }

        return string.Join('/', words);
    }
}
