using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMonsterDynamicVisibilityParityTests
{
    private const uint PlayerUid = 1_293_600;
    private const ushort ThunderSpellId = 1_000;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Theory]
    [InlineData("12345", true)]
    [InlineData("67890", false)]
    public async Task VisibleMonsterSpawnRequiresMatchingDynamicId(string playerDynamicId, bool shouldSpawn)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(dynamicId: "12345");
        var worldData = CreateWorldData(monsterFamily, [spawn]);
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(dynamicId: playerDynamicId);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            MonsterStates: new MonsterRuntimeStateStore());

        await InvokeSendVisibleMonstersAsync(context, session.Character);

        Assert.Equal(shouldSpawn, ContainsMonsterSpawn(sent, monster.Uid));
    }

    [Theory]
    [InlineData("12345", true)]
    [InlineData("67890", false)]
    public async Task MagicAttackAgainstMonsterRequiresMatchingDynamicId(string playerDynamicId, bool shouldAttack)
    {
        var monsterFamily = CreateMonsterFamily();
        var spawn = CreateMonsterSpawn(dynamicId: "12345");
        var worldData = CreateWorldData(monsterFamily, [spawn]);
        var monster = LegacyMonsterInstance.FromGenerator(
                spawn,
                monsterFamily,
                worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .Single();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(dynamicId: playerDynamicId);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster)).ToFrameBytes()));

        Assert.Equal(shouldAttack, TryGetSpellAnimationTargetDamage(sent, monster.Uid, out var damage));
        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visible));
        Assert.Equal(shouldAttack ? monster.MaxLife - damage : monster.MaxLife, visible.Hitpoints);
    }

    private static async Task InvokeSendVisibleMonstersAsync(
        WorldPacketHandlerContext context,
        CharacterSnapshot character)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            "SendVisibleMonstersAsync",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var result = method.Invoke(null, [context, character, CancellationToken.None, null]);
        var task = Assert.IsType<ValueTask>(result);
        await task;
    }

    private static MsgAttackQuery CreateMagicQuery(LegacyMonsterInstance monster)
    {
        return new MsgAttackQuery(
            Uid: PlayerUid,
            OpponentUid: monster.Uid,
            X: monster.X,
            Y: monster.Y,
            AttackType: MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: ThunderSpellId);
    }

    private static WorldSession CreateSession(string dynamicId)
    {
        return new WorldSession(
            AccountId: 1,
            Username: "caster",
            CreateCharacter(dynamicId),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(
            [
                new PlayerSpellSnapshotEntry(
                    ThunderSpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0),
            ]),
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(string dynamicId)
    {
        return new CharacterSnapshot(
            Uid: PlayerUid,
            Name: "Caster",
            Body: 1_003,
            Face: 0,
            Hair: 0,
            Class: 142,
            FirstClass: 142,
            SecondClass: 0,
            Reborn: 0,
            Level: 120,
            Experience: 0,
            MapId: 1_015,
            X: 830,
            Y: 584,
            PreviousMapId: 1_015,
            PreviousMapX: 830,
            PreviousMapY: 584,
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
            SourceFile: "fixture://dynamic-monster-character",
            Attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = dynamicId,
            },
            PaidNobility: new Dictionary<string, string>());
    }

    private static Monster CreateMonsterFamily()
    {
        return new Monster(
            9_001,
            "TrainingMonster",
            new Dictionary<string, string>
            {
                ["life"] = "5000",
                ["lookface"] = "100",
                ["level"] = "100",
                ["defense"] = "25",
                ["defense2"] = "10000",
                ["attack_range"] = "0",
            },
            []);
    }

    private static MonsterSpawn CreateMonsterSpawn(string dynamicId)
    {
        return new MonsterSpawn(
            GeneratorId: 1,
            MapId: 1_015,
            MonsterTypeId: 9_001,
            BoundX: 832,
            BoundY: 584,
            BoundWidth: 1,
            BoundHeight: 1,
            MaxNpc: 1,
            RestSeconds: 5,
            MaxPerGenerator: 1,
            TimerBegin: 0,
            TimerEnd: 0,
            BornX: 0,
            BornY: 0,
            SourceMapFolderId: null,
            SourceMonsterName: "TrainingMonster",
            SourceRelativePath: "1015/1.txt",
            Attributes: new Dictionary<string, string>
            {
                ["DynamicID"] = dynamicId,
                ["legacy_source"] = "LoadMapMonsters",
            });
    }

    private static WorldDataCatalog CreateWorldData(Monster monster, IReadOnlyList<MonsterSpawn> spawns)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-monster-dynamic-visibility",
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
            magic: [CreateMagicRow()],
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
            monsters: [monster],
            monsterSpawns: spawns,
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

    private static MagicType CreateMagicRow()
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = ThunderSpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = ThunderSpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = "1";
        fields[3] = "Thunder";
        fields[8] = "0";
        fields[9] = "7";
        fields[10] = "120";
        fields[15] = "9";
        fields[33] = "1000";
        fields[47] = "0";
        return new MagicType(ThunderSpellId, ThunderSpellId, "Thunder", fields);
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

    private static bool ContainsMonsterSpawn(IEnumerable<byte[]> frames, uint monsterUid)
    {
        return frames.Any(frame =>
            LegacyMonsterSpawnPacket.TryParse(ReadFrame(frame), out var spawn) &&
            spawn.Uid == monsterUid);
    }

    private static bool TryGetSpellAnimationTargetDamage(
        IEnumerable<byte[]> frames,
        uint monsterUid,
        out uint damage)
    {
        foreach (var frame in frames)
        {
            if (!LegacySpellAnimationPacket.TryParse(ReadFrame(frame), out var spell))
            {
                continue;
            }

            foreach (var target in spell.Targets)
            {
                if (target.Uid == monsterUid)
                {
                    damage = target.Damage;
                    return true;
                }
            }
        }

        damage = 0;
        return false;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        var sequence = new ReadOnlySequence<byte>(bytes);
        Assert.True(PacketFrameDecoder.TryRead(sequence, out var frame, out _, out _));
        return frame;
    }
}
