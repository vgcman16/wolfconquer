using System.Buffers;
using System.Buffers.Binary;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyUserAbilityScorePacketTests : IDisposable
{
    private const int LegacyItemPointsOffset = 68;

    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-user-ability-score-" + Guid.NewGuid());
    private readonly string _usersRoot;

    public LegacyUserAbilityScorePacketTests()
    {
        _usersRoot = Path.Combine(_dbRoot, "Users");
    }

    [Fact]
    public void LegacyUserAbilityScoreOpcodeMatchesLegacyWorldOpcode()
    {
        Assert.Equal((ushort)2154, WorldPacketOpcodes.LegacyUserAbilityScore);
        Assert.Equal(WorldPacketOpcodes.LegacyUserAbilityScore, LegacyUserAbilityScorePacket.Opcode);
    }

    [Fact]
    public void LegacyUserAbilityScoreQueryRoundTripsWithoutItems()
    {
        var packet = new LegacyUserAbilityScorePacket(Type: 0, Uid: 1_293_600, Level: 0, Items: []);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyUserAbilityScore, frame.Header.Opcode);
        Assert.True(LegacyUserAbilityScorePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.Uid, parsed.Uid);
        Assert.Equal(packet.Level, parsed.Level);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyUserAbilityScoreParsesFixedUidOnlyLiveQuery()
    {
        var writer = new PacketWriter(LegacyUserAbilityScorePacket.Opcode, payloadLength: 4);
        writer.WriteUInt32(0, 1_293_600);

        Assert.True(LegacyUserAbilityScorePacket.TryParse(ReadFrame(writer.ToArray()), out var parsed));
        Assert.Equal(0U, parsed.Type);
        Assert.Equal(1_293_600U, parsed.Uid);
        Assert.Equal(0U, parsed.Level);
        Assert.Empty(parsed.Items);
    }

    [Fact]
    public void LegacyUserAbilityScoreResponseRoundTripsNestedScores()
    {
        var packet = new LegacyUserAbilityScorePacket(
            Type: 1,
            Uid: 1_293_600,
            Level: 140,
            Items:
            [
                new LegacyUserAbilityScoreEntry(Position: 1, Points: 111),
                new LegacyUserAbilityScoreEntry(Position: 2, Points: 222),
            ]);
        var frame = ReadFrame(packet.ToFrameBytes());

        Assert.Equal(WorldPacketOpcodes.LegacyUserAbilityScore, frame.Header.Opcode);
        Assert.True(LegacyUserAbilityScorePacket.TryParse(frame, out var parsed));
        Assert.Equal(packet.Type, parsed.Type);
        Assert.Equal(packet.Uid, parsed.Uid);
        Assert.Equal(packet.Level, parsed.Level);
        Assert.Equal(
            [
                new LegacyUserAbilityScoreEntry(Position: 1, Points: 111),
                new LegacyUserAbilityScoreEntry(Position: 2, Points: 222),
            ],
            parsed.Items);
    }

    [Fact]
    public async Task HandlerRepliesToSelfQueryFromLegacyWorldSessionPrestigePoints()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync();
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyUserAbilityScorePacket(Type: 0, Uid: 1_293_600, Level: 0, Items: []).ToFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyUserAbilityScorePacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(1U, response.Type);
        Assert.Equal(1_293_600U, response.Uid);
        Assert.Equal(140U, response.Level);
        Assert.Equal(
            [
                new LegacyUserAbilityScoreEntry(Position: 1, Points: 111),
                new LegacyUserAbilityScoreEntry(Position: 2, Points: 222),
            ],
            response.Items.Take(2).ToArray());
    }

    [Theory]
    [InlineData(119, 1_190)]
    [InlineData(120, 1_800)]
    [InlineData(129, 1_935)]
    [InlineData(130, 2_600)]
    [InlineData(139, 2_780)]
    [InlineData(140, 3_500)]
    public async Task HandlerProjectsLegacyLevelPrestigeTierScoreWhenSnapshotHasNoOverrides(
        long level,
        uint expectedPoints)
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            level: level,
            includePrestigeOverrides: false);

        var response = await QuerySelfAbilityScoreAsync(handler, connection, sent, uid: 1_293_600);

        Assert.Equal((uint)level, response.Level);
        AssertPrestigePoint(response, position: 5, expectedPoints);
    }

    [Fact]
    public async Task HandlerProjectsSourceBackedPrestigeSlotsWhenSnapshotHasNoOverrides()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            reborn: 3,
            strength: 11,
            agility: 19,
            vitality: 17,
            spirit: 13,
            includePrestigeOverrides: false,
            extraCharacterAttributes:
                """
                NobilityRank=9
                PerfectionRedRune=101
                PerfectionBlueRune=202
                PerfectionYellowRune=303
                TailedBeastActivated=True
                TailedBeastLevel=52
                HundredWeaponsPerfectionScore=404
                """);

        var response = await QuerySelfAbilityScoreAsync(handler, connection, sent, uid: 1_293_600);

        Assert.Equal(43, response.Items.Count);
        AssertPrestigePoint(response, position: 5, points: 3_500);
        AssertPrestigePoint(response, position: 7, points: 300);
        AssertPrestigePoint(response, position: 8, points: 3_000);
        AssertPrestigePoint(response, position: 19, points: 9_000);
        AssertPrestigePoint(response, position: 21, points: 101);
        AssertPrestigePoint(response, position: 22, points: 202);
        AssertPrestigePoint(response, position: 23, points: 303);
        AssertPrestigePoint(response, position: 38, points: 10_600);
        AssertPrestigePoint(response, position: 39, points: 404);
    }

    [Fact]
    public async Task HandlerProjectsExpandedSourceBackedPrestigeSlotsWhenSnapshotHasNoOverrides()
    {
        var playerItems = BuildPlayerItemsWithItemPoints(
            (PlayerItemSnapshotLoaderTests.DbItem(uid: 1001, itemId: 500109, position: 4), 1_500U),
            (PlayerItemSnapshotLoaderTests.DbItem(uid: 1002, itemId: 410003, position: 5), 2_250U),
            (PlayerItemSnapshotLoaderTests.DbItem(uid: 1003, itemId: 3324226, position: 0), 9_999U));
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            includePrestigeOverrides: false,
            playerItems: playerItems,
            extraCharacterAttributes:
                """
                Inner_Strength=40500
                DragonChiRank=49
                PhoenixChiRank=50
                TigerRank=51
                TurtleChiRank=7
                SubProfInfo=6700#0#2#1#2#3#9#4#5#
                """);

        var response = await QuerySelfAbilityScoreAsync(handler, connection, sent, uid: 1_293_600);

        Assert.Equal(43, response.Items.Count);
        AssertPrestigePoint(response, position: 1, points: 13_365);
        AssertPrestigePoint(response, position: 2, points: 56_000);
        AssertPrestigePoint(response, position: 4, points: 7_500);
        AssertPrestigePoint(response, position: 16, points: 640);
    }

    [Fact]
    public async Task HandlerProjectsInnerPowerAndWardrobePrestigeSlotsFromWorldData()
    {
        var wardrobeItems = Enumerable.Range(0, 16)
            .Select(index => PlayerItemSnapshotLoaderTests.DbItem(
                uid: (uint)(7_000 + index),
                itemId: 181_305,
                position: 0,
                warehouseId: 100))
            .Concat(
            [
                PlayerItemSnapshotLoaderTests.DbItem(
                    uid: 8_000,
                    itemId: 181_315,
                    position: 0,
                    warehouseId: 100),
            ])
            .ToArray();
        var playerItems = PlayerItemSnapshotLoaderTests.BuildPlayerItems(wardrobeItems, []);
        var worldData = CreatePrestigeCatalog(
            innerPowerRanks: [CreateInnerPowerRankEntry()],
            coatStorageTypes:
            [
                CreateCoatStorageType(itemId: 181_305, garment: true),
                CreateCoatStorageType(itemId: 181_315, garment: false),
            ]);
        var (handler, connection, sent) = await LoginLegacyWorldAsync(
            includePrestigeOverrides: false,
            playerItems: playerItems,
            worldData: worldData);

        var response = await QuerySelfAbilityScoreAsync(handler, connection, sent, uid: 1_293_600);

        AssertPrestigePoint(response, position: 3, points: 200);
        AssertPrestigePoint(response, position: 17, points: 100);
        AssertPrestigePoint(response, position: 18, points: 100);
    }

    [Fact]
    public async Task HandlerSkipsMissingNonLiveUserAbilityScoreQuery()
    {
        var (handler, connection, sent) = await LoginLegacyWorldAsync();
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyUserAbilityScorePacket(Type: 0, Uid: 4_000_999, Level: 0, Items: []).ToFrameBytes()));

        Assert.Equal(countBeforeQuery, sent.Count);
    }

    [Fact]
    public async Task HandlerRepliesToOfflinePrestigeRankingQueryWhenRankSnapshotExists()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(accountId: 1_293_600, uid: 1_293_600);
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoffService: null,
            worldData: CreatePrestigeCatalog());
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyUserAbilityScorePacket(Type: 0, Uid: 4_000_222, Level: 0, Items: []).ToFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyUserAbilityScorePacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(1U, response.Type);
        Assert.Equal(4_000_222U, response.Uid);
        Assert.Equal(88U, response.Level);
        Assert.Equal(43, response.Items.Count);
        Assert.Equal(new LegacyUserAbilityScoreEntry(Position: 1, Points: 1_234), response.Items[0]);
        Assert.Equal(new LegacyUserAbilityScoreEntry(Position: 43, Points: 4_321), response.Items[42]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private async Task<(WorldServerFrameHandler Handler, ConquerTcpConnection Connection, List<byte[]> Sent)>
        LoginLegacyWorldAsync(
            long level = 140,
            long reborn = 2,
            long strength = 0,
            long agility = 36,
            long vitality = 447,
            long spirit = 0,
            bool includePrestigeOverrides = true,
            string? extraCharacterAttributes = null,
            byte[]? playerItems = null,
            WorldDataCatalog? worldData = null)
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(
            accountId: 1_293_600,
            uid: 1_293_600,
            level: level,
            reborn: reborn,
            strength: strength,
            agility: agility,
            vitality: vitality,
            spirit: spirit,
            includePrestigeOverrides: includePrestigeOverrides,
            extraCharacterAttributes: extraCharacterAttributes);
        if (playerItems is not null)
        {
            WritePlayerItems(uid: 1_293_600, playerItems);
        }

        var handler = worldData is null
            ? new WorldServerFrameHandler(new CharacterSnapshotLoader(_usersRoot))
            : new WorldServerFrameHandler(
                new CharacterSnapshotLoader(_usersRoot),
                handoffService: null,
                worldData);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(1_293_600).ToFrameBytes()));

        return (handler, connection, sent);
    }

    private void WritePlayerItems(long uid, byte[] bytes)
    {
        var itemsRoot = Path.Combine(_dbRoot, "PlayersItems");
        Directory.CreateDirectory(itemsRoot);
        File.WriteAllBytes(Path.Combine(itemsRoot, uid + ".bin"), bytes);
    }

    private static byte[] BuildPlayerItemsWithItemPoints(
        params (PlayerItemSnapshotLoaderTests.TestDbItem Item, uint Points)[] items)
    {
        var bytes = PlayerItemSnapshotLoaderTests.BuildPlayerItems(
            items.Select(entry => entry.Item).ToArray(),
            []);
        var offset = sizeof(int);
        foreach (var entry in items)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                bytes.AsSpan(offset + LegacyItemPointsOffset, sizeof(uint)),
                entry.Points);
            offset += PlayerItemSnapshotLoader.LegacyDbItemSize;
            offset += entry.Item.DepositedItems.Count * PlayerItemSnapshotLoader.LegacyDbItemSize;
        }

        return bytes;
    }

    private static async Task<LegacyUserAbilityScorePacket> QuerySelfAbilityScoreAsync(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        List<byte[]> sent,
        uint uid)
    {
        var countBeforeQuery = sent.Count;

        await handler.HandleFrameAsync(
            connection,
            ReadFrame(LegacyUserAbilityScorePacket.Query(uid).ToFrameBytes()));

        Assert.Equal(countBeforeQuery + 1, sent.Count);
        Assert.True(LegacyUserAbilityScorePacket.TryParse(ReadFrame(sent[countBeforeQuery]), out var response));
        Assert.Equal(1U, response.Type);
        Assert.Equal(uid, response.Uid);
        return response;
    }

    private static void AssertPrestigePoint(
        LegacyUserAbilityScorePacket response,
        int position,
        uint points)
    {
        Assert.InRange(position, 1, response.Items.Count);
        Assert.Equal(new LegacyUserAbilityScoreEntry((uint)position, points), response.Items[position - 1]);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static WorldDataCatalog CreatePrestigeCatalog(
        IReadOnlyList<InnerPowerRankEntry>? innerPowerRanks = null,
        IReadOnlyList<CoatStorageType>? coatStorageTypes = null)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "fixture://prestige",
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
            coatStorageTypes: coatStorageTypes ?? [],
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
            prestigeRanks: [CreatePrestigeRankEntry()],
            innerPowerRanks: innerPowerRanks ?? [],
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
            magic: [],
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
            monsters: [],
            monsterSpawns: [],
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

    private static PrestigeRankEntry CreatePrestigeRankEntry()
    {
        var fields = Enumerable.Repeat("0", 63).ToArray();
        fields[2] = "88";
        fields[19] = "43";
        fields[20] = "1234";
        fields[62] = "4321";

        return new PrestigeRankEntry(
            CharacterId: 4_000_222,
            CharacterName: "Ranked",
            RankingFields: fields,
            SourceFile: "PrestigeRanking.txt",
            LineNumber: 2,
            RawLine: "4000222/Ranked/fixture/",
            Fields: []);
    }

    private static InnerPowerRankEntry CreateInnerPowerRankEntry()
    {
        string[] fields =
        [
            "Inner",
            "1293600",
            "0",
            "3",
            "1",
            "1",
            "0",
            "2",
            "0",
            "0",
            "3",
            "1",
            "0",
            "",
        ];

        return new InnerPowerRankEntry(
            CharacterName: "Inner",
            CharacterId: 1_293_600,
            RankingFields: fields.Skip(2).Take(fields.Length - 3).ToArray(),
            SourceFile: "InnerPower.txt",
            LineNumber: 2,
            RawLine: "Inner/1293600/0/3/1/1/0/2/0/0/3/1/0/",
            Fields: fields);
    }

    private static CoatStorageType CreateCoatStorageType(int itemId, bool garment)
    {
        return new CoatStorageType(
            ItemTypeId: itemId,
            StorageType: itemId,
            Parameters: [garment ? 1 : 0, 0, 0, 0, 0, 1],
            SourceFile: "coat_storage_type.txt",
            LineNumber: 1,
            RawLine: itemId + "@@",
            Fields: []);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private void WriteSnapshot(
        long accountId,
        long uid,
        long level = 140,
        long reborn = 2,
        long strength = 0,
        long agility = 36,
        long vitality = 447,
        long spirit = 0,
        bool includePrestigeOverrides = true,
        string? extraCharacterAttributes = null)
    {
        var prestigeOverrideLines = includePrestigeOverrides
            ? """
            PrestigePoints=111/222/
            PrestigePoint1=111
            PrestigePoint2=222
            """
            : "";
        var extraAttributes = string.IsNullOrWhiteSpace(extraCharacterAttributes)
            ? ""
            : extraCharacterAttributes.Trim();

        File.WriteAllText(
            Path.Combine(_usersRoot, accountId + ".ini"),
            $$"""
            [Character]
            UID={{uid}}
            Body=1008
            Face=154
            Name=HeroOne
            Class=7051
            Map=1015
            X=830
            Y=584
            FirstClass=0
            SecoundeClass=7050
            Conquer=0
            Reborn={{reborn}}
            Level={{level}}
            Haire=27
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility={{agility}}
            Strength={{strength}}
            Vitaliti={{vitality}}
            Spirit={{spirit}}
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=639778904
            BankCps=123
            BoundConquerPoints=456
            Money=98602439956
            RacePoints=0
            ChampionPoints=0
            GuildDontion=900
            PkPoints=0
            Online=7
            Day={{DateTime.Now.DayOfYear}}
            {{extraAttributes}}
            {{prestigeOverrideLines}}
            Achivement=0/171966688/262144/523784/939949824/524288/1342381056/3/576/0/0/524288/25167856/6817280/0/
            Activeness=0/80/2/1/10/1/22/0/0/
            Dragon=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Pheonix=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Tiger=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            Turtle=1/5963000/8/2500/0/3/200/0/6/3500/0/2/200/0/
            ChiPoints=153850

            [PaidNobility]
            Rank=King
            Identifier=1293600
            """);
    }
}
