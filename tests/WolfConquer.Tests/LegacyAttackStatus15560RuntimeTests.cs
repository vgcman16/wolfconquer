using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyAttackStatus15560RuntimeTests
{
    private const uint CasterUid = 1_553_600;
    private const uint TargetPlayerUid = 1_553_601;
    private const ushort DuelAttackStatusSpellId = 15_560;
    private const int LegacyDuelFlag = 0xE1;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Fact]
    public async Task HandlerAttackStatus15560AppliesDuelPercentAndFlagToVisiblePlayerWithZeroDamage()
    {
        var worldData = CreateWorldData(
            [],
            [],
            CreateMagicRow(
                DuelAttackStatusSpellId,
                sort: 16,
                useMana: 7,
                damage: 42,
                range: 8,
                duration: 11));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            uid: CasterUid,
            name: "Caster",
            x: 100,
            y: 100,
            minMana: 500,
            spellIds: [DuelAttackStatusSpellId]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 103,
            y: 100,
            minMana: 0,
            spellIds: []);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(TargetPlayerUid, 103, 100)).ToFrameBytes()));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTarget));
        Assert.NotNull(updatedTarget);
        Assert.Equal("42", updatedTarget.Character.Attributes["DuelPercent"]);
        AssertHasStatusFlag(updatedTarget.Character.Attributes, LegacyDuelFlag);

        Assert.True(sessions.TryGet(casterConnection, out var updatedCaster));
        Assert.NotNull(updatedCaster);
        Assert.Equal(493, updatedCaster.Character.MinMana);
        var learnedSpell = Assert.Single(updatedCaster.PlayerSpells.Spells);
        Assert.Equal(0, learnedSpell.Experience);

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(CasterUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal(103, animation.X);
        Assert.Equal(100, animation.Y);
        Assert.Equal(DuelAttackStatusSpellId, animation.SpellId);
        var animationTarget = Assert.Single(animation.Targets);
        Assert.Equal(TargetPlayerUid, animationTarget.Uid);
        Assert.Equal(0U, animationTarget.Damage);
        Assert.True(animationTarget.Hit);

        var targetStatusUpdate = Assert.Single(ParseStatusFlagUpdates(targetSent));
        Assert.Equal(TargetPlayerUid, targetStatusUpdate.Uid);
        var statusEntry = Assert.Single(targetStatusUpdate.Updates);
        Assert.NotNull(statusEntry.Values);
        Assert.True((statusEntry.Values[LegacyDuelFlag / 32] & (1UL << (LegacyDuelFlag % 32))) != 0);

        var output = log.ToString();
        Assert.Contains("spellId=15560", output);
        Assert.Contains("targets=1", output);
        Assert.Contains("totalDamage=0", output);
    }

    [Fact]
    public async Task HandlerAttackStatus15560SkipsMonsterAndSobTargetsButStillSendsZeroTargetAnimation()
    {
        var monsterFamily = CreateMonsterFamily();
        var monsterSpawn = CreateMonsterSpawn();
        var monster = Assert.Single(LegacyMonsterInstance.FromGenerator(
            monsterSpawn,
            monsterFamily,
            CreateWorldData(
                    [monsterFamily],
                    [monsterSpawn],
                    CreateMagicRow(DuelAttackStatusSpellId, sort: 16, range: 8))
                .GetLegacyMonsterRuntimeUidBase(monsterSpawn)));
        var sobNpc = CreateSobNpc();
        var worldData = CreateWorldData(
            [monsterFamily],
            [monsterSpawn],
            CreateMagicRow(DuelAttackStatusSpellId, sort: 16, damage: 42, range: 8),
            sobNpcs: [sobNpc]);
        var monsterStates = new MonsterRuntimeStateStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            uid: CasterUid,
            name: "Caster",
            x: 100,
            y: 100,
            spellIds: [DuelAttackStatusSpellId]);
        sessions.Set(connection, session);

        var monsterContext = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            SobNpcStates: sobNpcStates);
        await PacketHandlers.HandleAsync(
            monsterContext,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(monster.Uid, monster.X, monster.Y)).ToFrameBytes()));
        await PacketHandlers.HandleAsync(
            monsterContext,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(CheckedUInt32(sobNpc.Id), CheckedUInt16(sobNpc.X), CheckedUInt16(sobNpc.Y))).ToFrameBytes()));

        var animations = ParseSpellAnimations(sent);
        Assert.Equal(2, animations.Count);
        Assert.All(animations, animation =>
        {
            Assert.Equal(DuelAttackStatusSpellId, animation.SpellId);
            Assert.Empty(animation.Targets);
        });

        Assert.True(monsterStates.TryGetVisibleInstance(monster, DateTimeOffset.UtcNow, out var visibleMonster));
        Assert.Equal(monster.MaxLife, visibleMonster.Hitpoints);
        var visibleSobNpc = sobNpcStates.GetVisible(sobNpc);
        Assert.Equal(sobNpc.Life, visibleSobNpc.Life);

        var output = log.ToString();
        Assert.Contains("spellId=15560", output);
        Assert.Contains("targets=0", output);
        Assert.Contains("totalDamage=0", output);
    }

    private static MsgAttackQuery CreateMagicQuery(uint targetUid, ushort x, ushort y)
    {
        return new MsgAttackQuery(
            Uid: CasterUid,
            OpponentUid: targetUid,
            X: x,
            Y: y,
            AttackType: MsgAttackType.Magic,
            Damage: DuelAttackStatusSpellId,
            KilledMonster: 0,
            ResponseDamage: 0,
            Effect: MsgAttackEffect.None,
            Unknown: 0,
            SpellId: DuelAttackStatusSpellId);
    }

    private static WorldSession CreateSession(
        uint uid,
        string name,
        long x,
        long y,
        long minMana = 500,
        IReadOnlyList<ushort>? spellIds = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        var spells = spellIds is null
            ? []
            : spellIds
                .Select(spellId => new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0))
                .ToArray();
        return new WorldSession(
            AccountId: uid,
            Username: name.ToLowerInvariant(),
            CreateCharacter(uid, name, x, y, minMana, attributes),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(spells),
            PlayerProficiencySnapshot.Empty,
            CombatGear: CombatGearSnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(
        uint uid,
        string name,
        long x,
        long y,
        long minMana,
        IReadOnlyDictionary<string, string>? runtimeAttributes)
    {
        var attributes = runtimeAttributes is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(runtimeAttributes, StringComparer.OrdinalIgnoreCase);
        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
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
            X: x,
            Y: y,
            PreviousMapId: 1_015,
            PreviousMapX: x,
            PreviousMapY: y,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 300,
            AttributePoints: 0,
            MinHitPoints: 5_000,
            MinMana: minMana,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 1,
            SourceFile: "fixture://attack-status-15560",
            Attributes: attributes,
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static MagicType CreateMagicRow(
        ushort spellId,
        byte sort,
        ushort useMana = 0,
        uint damage = 42,
        byte range = 8,
        uint duration = 11)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = spellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = sort.ToString(CultureInfo.InvariantCulture);
        fields[3] = $"Spell{spellId}";
        fields[8] = "0";
        fields[9] = useMana.ToString(CultureInfo.InvariantCulture);
        fields[10] = damage.ToString(CultureInfo.InvariantCulture);
        fields[13] = duration.ToString(CultureInfo.InvariantCulture);
        fields[14] = "0";
        fields[15] = range.ToString(CultureInfo.InvariantCulture);
        fields[20] = "0";
        fields[33] = "0";
        fields[47] = "0";
        return new MagicType(spellId, spellId, $"Spell{spellId}", fields);
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

    private static MonsterSpawn CreateMonsterSpawn()
    {
        return new MonsterSpawn(
            GeneratorId: 1,
            MapId: 1_015,
            MonsterTypeId: 9_001,
            BoundX: 103,
            BoundY: 100,
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
            Attributes: new Dictionary<string, string>());
    }

    private static SobNpc CreateSobNpc()
    {
        return new SobNpc(
            Id: 65_005,
            Name: "GuildPole",
            Kind: 10,
            Lookface: 1_137,
            MapId: 1_015,
            X: 103,
            Y: 100,
            Life: 5_000,
            MaxLife: 5_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: "65005,GuildPole,10,1137,1015,103,100,5000,5000,17,0",
            Fields: ["65005", "GuildPole", "10", "1137", "1015", "103", "100", "5000", "5000", "17", "0"]);
    }

    private static WorldDataCatalog CreateWorldData(
        IReadOnlyList<Monster> monsters,
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        MagicType magic,
        IReadOnlyList<SobNpc>? sobNpcs = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-22T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://attack-status-15560",
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
            guildWarInfo: new GuildWarData("fixture://guildwar", [], [], [], info: null, pole: null, conductors: []),
            superGuildWarInfo: new GuildWarData("fixture://superguildwar", [], [], [], info: null, pole: null, conductors: []),
            classLevelStats: [],
            professionLevelUps: [],
            professionLevelBenefits: [],
            professionTitleBenefits: [],
            questInfo: new QuestInfoMetadata(ActionBase: null, RecommendInterval: null, TotalMission: null),
            questTaskTypes: [],
            questMissions: [],
            quizQuestions: [],
            magic: [magic],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: sobNpcs ?? [],
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

    private static IReadOnlyList<LegacyMsgUpdatePacket> ParseStatusFlagUpdates(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacyMsgUpdatePacket>();
        foreach (var bytes in frames)
        {
            if (LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Updates.Any(update => update.Type == LegacyMsgUpdateDataType.StatusFlag))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static void AssertHasStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        Assert.True(attributes.TryGetValue("StatusFlags", out var raw));
        var parts = raw.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.True(parts.Length > flag / 32);
        Assert.True(ulong.TryParse(parts[flag / 32], NumberStyles.Integer, CultureInfo.InvariantCulture, out var word));
        Assert.True((word & (1UL << (flag % 32))) != 0);
    }

    private static ushort CheckedUInt16(long value)
    {
        Assert.InRange(value, ushort.MinValue, ushort.MaxValue);
        return (ushort)value;
    }

    private static uint CheckedUInt32(long value)
    {
        Assert.InRange(value, uint.MinValue, uint.MaxValue);
        return (uint)value;
    }
}
