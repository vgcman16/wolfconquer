using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using Xunit;

namespace WolfConquer.Tests;

public sealed class LegacyLayTrap12990RuntimeTests
{
    private const uint PlayerUid = 1_293_600;
    private const uint TargetPlayerUid = 1_293_601;
    private const ushort LayTrap12990SpellId = 12_990;

    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> PacketHandlers =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    [Theory]
    [InlineData("player", 836, 590)]
    [InlineData("monster", 837, 591)]
    [InlineData("sobNpc", 838, 592)]
    public async Task HandlerLayTrap12990CreatesSourceFloorEffectAtVisibleTargetCoordinates(
        string targetKind,
        ushort expectedX,
        ushort expectedY)
    {
        var monsterFamily = CreateMonsterFamily();
        var monsterSpawns = Array.Empty<MonsterSpawn>();
        var sobNpcs = Array.Empty<SobNpc>();
        MonsterSpawn? monsterSpawn = null;
        SobNpc? sobNpc = null;
        if (targetKind == "monster")
        {
            monsterSpawn = CreateSingleInstanceMonsterSpawn(boundX: expectedX, boundY: expectedY);
            monsterSpawns = [monsterSpawn];
        }
        else if (targetKind == "sobNpc")
        {
            sobNpc = CreateLayTrapSobNpc(id: 65_990, x: expectedX, y: expectedY);
            sobNpcs = [sobNpc];
        }

        var worldData = CreateLayTrap12990WorldData(monsterFamily, monsterSpawns, sobNpcs);
        uint opponentUid;
        if (targetKind == "monster")
        {
            Assert.NotNull(monsterSpawn);
            opponentUid = CreateRuntimeMonsterInstance(worldData, monsterSpawn, monsterFamily).Uid;
        }
        else if (targetKind == "sobNpc")
        {
            Assert.NotNull(sobNpc);
            opponentUid = checked((uint)sobNpc.Id);
        }
        else
        {
            opponentUid = TargetPlayerUid;
        }

        var floorItems = new FloorItemRuntimeStateStore();
        var monsterStates = new MonsterRuntimeStateStore();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    LayTrap12990SpellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 2),
            ],
            attributes: new Dictionary<string, string>
            {
                ["GuildID"] = "77",
            });
        sessions.Set(casterConnection, casterSession);
        if (targetKind == "player")
        {
            sessions.Set(
                targetConnection,
                CreateSession(uid: TargetPlayerUid, name: "Target", x: expectedX, y: expectedY));
        }

        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions,
            MonsterStates: monsterStates,
            FloorItems: floorItems);
        var before = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid,
                x: 812,
                y: 613,
                LayTrap12990SpellId)).ToFrameBytes()));

        var after = DateTimeOffset.UtcNow;
        var floorItem = Assert.Single(ParseFloorItems(casterSent));
        Assert.Equal((uint)1_530, floorItem.ItemId);
        Assert.Equal(expectedX, floorItem.X);
        Assert.Equal(expectedY, floorItem.Y);
        Assert.Equal((ushort)0, floorItem.MaxLife);
        Assert.Equal(LegacyFloorItemDropType.Effect, floorItem.DropType);
        Assert.Equal((uint)0, floorItem.Life);
        Assert.Equal((byte)14, floorItem.PlusOrColor);
        Assert.Equal(PlayerUid, floorItem.ItemOwnerUid);
        Assert.Equal((uint)77, floorItem.GuildId);
        Assert.Equal((ushort)830, floorItem.OwnerX);
        Assert.Equal((ushort)584, floorItem.OwnerY);
        Assert.Equal("", floorItem.Name);

        Assert.True(floorItems.TryGetVisibleEntry(floorItem.Uid, after, out var runtimeEntry));
        Assert.Equal(1_015, runtimeEntry.MapId);
        Assert.Equal(floorItem, runtimeEntry.Packet);
        Assert.NotNull(runtimeEntry.Effect);
        Assert.Equal(LayTrap12990SpellId, runtimeEntry.Effect!.SpellId);
        Assert.Equal((ushort)830, runtimeEntry.Effect.OriginX);
        Assert.Equal((ushort)584, runtimeEntry.Effect.OriginY);
        Assert.InRange(
            runtimeEntry.Effect.DueAt,
            before.AddMilliseconds(1_000),
            after.AddMilliseconds(1_000));

        var animation = Assert.Single(ParseSpellAnimations(casterSent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((ushort)830, animation.X);
        Assert.Equal((ushort)584, animation.Y);
        Assert.Equal(LayTrap12990SpellId, animation.SpellId);
        Assert.Equal((byte)0, animation.SpellLevel);
        Assert.Equal((byte)2, animation.LevelHu);
        Assert.Empty(animation.Targets);

        if (targetKind == "player")
        {
            Assert.Equal(floorItem, Assert.Single(ParseFloorItems(targetSent)));
            var targetAnimation = Assert.Single(ParseSpellAnimations(targetSent));
            Assert.Equal(animation.AttackerUid, targetAnimation.AttackerUid);
            Assert.Equal(animation.AttackedUid, targetAnimation.AttackedUid);
            Assert.Equal(animation.X, targetAnimation.X);
            Assert.Equal(animation.Y, targetAnimation.Y);
            Assert.Equal(animation.SpellId, targetAnimation.SpellId);
            Assert.Equal(animation.SpellLevel, targetAnimation.SpellLevel);
            Assert.Equal(animation.LevelHu, targetAnimation.LevelHu);
            Assert.Empty(targetAnimation.Targets);
        }

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(493, updatedCasterSession.Character.MinMana);
        var updatedSpell = Assert.Single(updatedCasterSession.PlayerSpells.Spells);
        Assert.Equal(LayTrap12990SpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy LayTrap magic", output);
        Assert.Contains("spellId=12990", output);
        Assert.Contains($"targetKind={targetKind}", output);
        Assert.Contains("floorEffectCount=1", output);
        Assert.Contains("source=LayTrap.cs case 12990", output);
    }

    [Fact]
    public async Task HandlerLayTrap12990MissingTargetStillSendsSourceAnimationAndSpellUpdatePath()
    {
        var worldData = CreateLayTrap12990WorldData(CreateMonsterFamily(), [], []);
        var sessions = new WorldSessionStore();
        var floorItems = new FloorItemRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [LayTrap12990SpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            FloorItems: floorItems);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(CreateMagicQuery(
                opponentUid: 999_999,
                x: 812,
                y: 613,
                LayTrap12990SpellId)).ToFrameBytes()));

        Assert.Empty(ParseFloorItems(sent));
        Assert.Empty(floorItems.SnapshotVisible(DateTimeOffset.UtcNow));

        var animation = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, animation.AttackerUid);
        Assert.Equal(0U, animation.AttackedUid);
        Assert.Equal((ushort)830, animation.X);
        Assert.Equal((ushort)584, animation.Y);
        Assert.Equal(LayTrap12990SpellId, animation.SpellId);
        Assert.Equal((byte)0, animation.SpellLevel);
        Assert.Empty(animation.Targets);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(493, updatedSession.Character.MinMana);
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(LayTrap12990SpellId, updatedSpell.Id);
        Assert.Equal(1, updatedSpell.Level);
        Assert.Equal(0, updatedSpell.Experience);

        var output = log.ToString();
        Assert.Contains("targetKind=none", output);
        Assert.Contains("floorEffectCount=0", output);
        Assert.Contains("source=LayTrap.cs case 12990", output);
    }

    private static MsgAttackQuery CreateMagicQuery(
        uint opponentUid,
        ushort x,
        ushort y,
        ushort spellId)
    {
        return new MsgAttackQuery(
            PlayerUid,
            opponentUid,
            x,
            y,
            MsgAttackType.Magic,
            Damage: 0,
            KilledMonster: 0,
            ResponseDamage: 0,
            MsgAttackEffect.None,
            Unknown: 0,
            SpellId: spellId);
    }

    private static WorldSession CreateSession(
        long uid = PlayerUid,
        string name = "Caster",
        long x = 830,
        long y = 584,
        long minMana = 500,
        IReadOnlyList<ushort>? spellIds = null,
        IReadOnlyList<PlayerSpellSnapshotEntry>? spellEntries = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        spellIds ??= [LayTrap12990SpellId];
        var spells = spellEntries ??
            spellIds
                .Select(spellId => new PlayerSpellSnapshotEntry(
                    spellId,
                    Level: 0,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0))
                .ToArray();

        return new WorldSession(
            AccountId: 1,
            Username: name.ToLowerInvariant(),
            CreateCharacter(uid, name, x, y, minMana, attributes),
            PlayerItemSnapshot.Empty,
            new PlayerSpellSnapshot(spells),
            PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateCharacter(
        long uid,
        string name,
        long x,
        long y,
        long minMana,
        IReadOnlyDictionary<string, string>? runtimeAttributes)
    {
        var attributes = runtimeAttributes is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(runtimeAttributes);

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
            SourceFile: "fixture://laytrap-12990-character",
            Attributes: attributes,
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

    private static MonsterSpawn CreateSingleInstanceMonsterSpawn(
        int generatorId = 1,
        int boundX = 836,
        int boundY = 584)
    {
        return new MonsterSpawn(
            GeneratorId: generatorId,
            MapId: 1_015,
            MonsterTypeId: 9_001,
            BoundX: boundX,
            BoundY: boundY,
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

    private static LegacyMonsterInstance CreateRuntimeMonsterInstance(
        WorldDataCatalog worldData,
        MonsterSpawn spawn,
        Monster monster)
    {
        return LegacyMonsterInstance
            .FromGenerator(spawn, monster, worldData.GetLegacyMonsterRuntimeUidBase(spawn))
            .First();
    }

    private static SobNpc CreateLayTrapSobNpc(int id, int x, int y)
    {
        return new SobNpc(
            Id: id,
            Name: "LayTrapPole",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 1_015,
            X: x,
            Y: y,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 1,
            RawLine: $"{id},LayTrapPole,10,1137,1015,{x},{y},50000,50000,17,0",
            Fields:
            [
                id.ToString(CultureInfo.InvariantCulture),
                "LayTrapPole",
                "10",
                "1137",
                "1015",
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture),
                "50000",
                "50000",
                "17",
                "0",
            ]);
    }

    private static WorldDataCatalog CreateLayTrap12990WorldData(
        Monster monster,
        IReadOnlyList<MonsterSpawn> monsterSpawns,
        IReadOnlyList<SobNpc> sobNpcs)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-22T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://laytrap-12990",
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
            magic:
            [
                CreateLayTrap12990MagicRow(level: 0, experience: 1),
                CreateLayTrap12990MagicRow(level: 1, experience: 100),
            ],
            magicEffects: [],
            magicTypeOperations: [],
            titleTypes: [],
            combatGear: [],
            traps: [],
            npcs: [],
            sobNpcs: sobNpcs,
            portals: [],
            maps: [],
            mapNames: [],
            monsters: [monster],
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

    private static MagicType CreateLayTrap12990MagicRow(byte level, uint experience)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        fields[0] = LayTrap12990SpellId.ToString(CultureInfo.InvariantCulture);
        fields[1] = LayTrap12990SpellId.ToString(CultureInfo.InvariantCulture);
        fields[2] = ((byte)LegacyMagicSort.LayTrap).ToString(CultureInfo.InvariantCulture);
        fields[3] = "Spell12990";
        fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[9] = "7";
        fields[10] = "120";
        fields[14] = "0";
        fields[15] = "9";
        fields[18] = experience.ToString(CultureInfo.InvariantCulture);
        fields[20] = "0";
        fields[33] = "1000";
        fields[47] = "0";
        return new MagicType(LayTrap12990SpellId, LayTrap12990SpellId, "Spell12990", fields);
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

    private static List<LegacyFloorItemPacket> ParseFloorItems(IEnumerable<byte[]> frames)
    {
        var floorItems = new List<LegacyFloorItemPacket>();
        foreach (var bytes in frames)
        {
            if (LegacyFloorItemPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                floorItems.Add(packet);
            }
        }

        return floorItems;
    }

    private static List<LegacySpellAnimationPacket> ParseSpellAnimations(IEnumerable<byte[]> frames)
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
}
