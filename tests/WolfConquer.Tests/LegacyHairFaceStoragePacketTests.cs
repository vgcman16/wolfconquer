using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyHairFaceStoragePacketTests : IDisposable
{
    private const uint PlayerUid = 1_293_600;
    private const uint BuyableHairstyleId = 27;
    private const int BuyableHairstyleCost = 500;
    private const uint BuyableColorIndex = 5;
    private const int BuyableColorCost = 150;
    private const long InitialColorMask = 1;
    private const long BoughtColorMask = InitialColorMask | (1L << (int)BuyableColorIndex);
    private readonly string _dbRoot = Path.Combine(Path.GetTempPath(), "wolf-legacy-hairface-" + Guid.NewGuid());

    [Fact]
    public void LegacyHairFaceStorageOpcodeMatchesLegacyFinalize()
    {
        Assert.Equal((ushort)2282, WorldPacketOpcodes.LegacyHairFaceStorage);
        Assert.Equal(WorldPacketOpcodes.LegacyHairFaceStorage, LegacyHairFaceStoragePacket.Opcode);
    }

    [Fact]
    public void LegacyHairFaceStorageActionsMatchLegacyValues()
    {
        Assert.Equal(0, (int)LegacyHairFaceStorageAction.Login);
        Assert.Equal(1, (int)LegacyHairFaceStorageAction.Add);
        Assert.Equal(3, (int)LegacyHairFaceStorageAction.Equip);
        Assert.Equal(4, (int)LegacyHairFaceStorageAction.Buy);
        Assert.Equal(5, (int)LegacyHairFaceStorageAction.AddColor);
        Assert.Equal(6, (int)LegacyHairFaceStorageAction.EquipColor);
        Assert.Equal(7, (int)LegacyHairFaceStorageAction.BuyColor);
        Assert.Equal(8, (int)LegacyHairFaceStorageAction.Avatars);
        Assert.Equal(9, (int)LegacyHairFaceStorageAction.HairStyles);
    }

    [Fact]
    public void LegacyHairFaceStorageTypesMatchLegacyValues()
    {
        Assert.Equal(0, (int)LegacyHairFaceStorageType.Hairstyle);
        Assert.Equal(1, (int)LegacyHairFaceStorageType.Avatar);
    }

    [Fact]
    public void LegacyHairFaceStorageLoginRoundTripsWithEmptyItems()
    {
        var packet = LegacyHairFaceStoragePacket.EmptyLoginResponse();

        var bytes = packet.ToFrameBytes();

        Assert.Equal("0600ea080800", Hex(bytes));
        Assert.True(LegacyHairFaceStoragePacket.TryParse(ReadFrame(bytes), out var parsed));
        Assert.Equal(LegacyHairFaceStorageAction.Login, parsed.Action);
        Assert.Empty(parsed.Items);
        Assert.Empty(parsed.AvatarsOrHairTap);
    }

    [Theory]
    [InlineData(LegacyHairFaceStorageAction.Avatars)]
    [InlineData(LegacyHairFaceStorageAction.HairStyles)]
    public void LegacyHairFaceStorageTabResponseWritesOneDefaultItemWithZeroColor(
        LegacyHairFaceStorageAction action)
    {
        var packet = LegacyHairFaceStoragePacket.TabResponse(action);

        var bytes = packet.ToFrameBytes();
        var frame = ReadFrame(bytes);

        Assert.Equal(WorldPacketOpcodes.LegacyHairFaceStorage, frame.Header.Opcode);
        Assert.True(LegacyHairFaceStoragePacket.TryParse(frame, out var parsed));
        Assert.Equal(action, parsed.Action);
        Assert.Empty(parsed.Items);

        var item = Assert.Single(parsed.AvatarsOrHairTap);
        Assert.Equal(LegacyHairFaceStorageType.Hairstyle, item.Type);
        Assert.Equal(0U, item.Id);
        Assert.False(item.Equipped);
        Assert.Equal(0U, item.Unknown4);
        Assert.Equal([0L], item.Colors);
    }

    [Fact]
    public void LegacyHairFaceStorageParsesInboundLoginRequest()
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyHairFaceStorage, payloadLength: 2);
        writer.Payload[0] = 0x08;
        writer.Payload[1] = 0x00;

        Assert.True(LegacyHairFaceStoragePacket.TryParse(ReadFrame(writer.ToArray()), out var parsed));
        Assert.Equal(LegacyHairFaceStorageAction.Login, parsed.Action);
        Assert.Empty(parsed.Items);
        Assert.Empty(parsed.AvatarsOrHairTap);
    }

    [Fact]
    public async Task HandlerLegacyHairFaceStorageLoginSendsLoginResponse()
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var request = LegacyHairFaceStoragePacket.EmptyLoginResponse();

        await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

        var responseFrame = ReadFrame(Assert.Single(sent));
        Assert.Equal(WorldPacketOpcodes.LegacyHairFaceStorage, responseFrame.Header.Opcode);
        Assert.True(LegacyHairFaceStoragePacket.TryParse(responseFrame, out var response));
        Assert.Equal(LegacyHairFaceStorageAction.Login, response.Action);
        Assert.Empty(response.Items);
        Assert.Empty(response.AvatarsOrHairTap);
        Assert.DoesNotContain("WorldServer received opcode 2282", log.ToString());
    }

    [Theory]
    [InlineData(LegacyHairFaceStorageAction.Avatars)]
    [InlineData(LegacyHairFaceStorageAction.HairStyles)]
    public async Task HandlerLegacyHairFaceStorageTabActionsSendDefaultTabResponse(
        LegacyHairFaceStorageAction action)
    {
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(snapshotLoader: null, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var request = LegacyHairFaceStoragePacket.TabResponse(action) with { AvatarsOrHairTap = [] };

        await handler.HandleFrameAsync(connection, ReadFrame(request.ToFrameBytes()));

        var responseFrame = ReadFrame(Assert.Single(sent));
        Assert.Equal(WorldPacketOpcodes.LegacyHairFaceStorage, responseFrame.Header.Opcode);
        Assert.True(LegacyHairFaceStoragePacket.TryParse(responseFrame, out var response));
        Assert.Equal(action, response.Action);
        Assert.Empty(response.Items);

        var item = Assert.Single(response.AvatarsOrHairTap);
        Assert.Equal([0L], item.Colors);
        Assert.Contains("WorldServer sent LegacyHairFaceStorage", log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyHairFaceStorageBuyWithEnoughMoneyAddsOwnedHairstyleForNextLogin()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(money: 1_000, hair: 12, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LogInAsync(handler, connection, sent);

        await handler.HandleFrameAsync(connection, ReadFrame(BuyHairstyleRequest(BuyableHairstyleId).ToFrameBytes()));
        var countBeforeLogin = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LoginRequest().ToFrameBytes()));

        Assert.Equal(countBeforeLogin + 1, sent.Count);
        var response = AssertHairFaceResponse(sent[countBeforeLogin], LegacyHairFaceStorageAction.Login);
        var item = Assert.Single(response.Items);
        Assert.Equal(LegacyHairFaceStorageType.Hairstyle, item.Type);
        Assert.Equal(BuyableHairstyleId, item.Id);
        Assert.False(item.Equipped);
    }

    [Fact]
    public async Task HandlerLegacyHairFaceStorageBuyWithInsufficientMoneyKeepsNextLoginEmptyAndLogsSkip()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(money: BuyableHairstyleCost - 1, hair: 12, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LogInAsync(handler, connection, sent);
        var countBeforeBuy = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(BuyHairstyleRequest(BuyableHairstyleId).ToFrameBytes()));

        Assert.Equal(countBeforeBuy, sent.Count);

        await handler.HandleFrameAsync(connection, ReadFrame(LoginRequest().ToFrameBytes()));

        var response = AssertHairFaceResponse(sent[countBeforeBuy], LegacyHairFaceStorageAction.Login);
        Assert.Empty(response.Items);
        Assert.Contains("skipped LegacyHairFaceStorage buy", log.ToString());
        Assert.Contains("cost=500 money=499", log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyHairFaceStorageEquipOwnedHairstyleMarksItEquippedForNextLogin()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(money: 1_000, hair: 12, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LogInAsync(handler, connection, sent);

        await handler.HandleFrameAsync(connection, ReadFrame(BuyHairstyleRequest(BuyableHairstyleId).ToFrameBytes()));
        await handler.HandleFrameAsync(connection, ReadFrame(EquipHairstyleRequest(BuyableHairstyleId).ToFrameBytes()));
        var countBeforeLogin = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LoginRequest().ToFrameBytes()));

        Assert.Equal(countBeforeLogin + 1, sent.Count);
        var response = AssertHairFaceResponse(sent[countBeforeLogin], LegacyHairFaceStorageAction.Login);
        var item = Assert.Single(response.Items);
        Assert.Equal(LegacyHairFaceStorageType.Hairstyle, item.Type);
        Assert.Equal(BuyableHairstyleId, item.Id);
        Assert.True(item.Equipped);
    }

    [Fact]
    public async Task HandlerLegacyHairFaceStorageBuyColorWithEnoughMoneyAddsColorForNextLogin()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(money: 1_000, hair: 12, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LogInAsync(handler, connection, sent);

        await handler.HandleFrameAsync(connection, ReadFrame(BuyHairstyleRequest(BuyableHairstyleId).ToFrameBytes()));
        await handler.HandleFrameAsync(connection, ReadFrame(BuyColorRequest(BuyableHairstyleId, BuyableColorIndex).ToFrameBytes()));
        var countBeforeLogin = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LoginRequest().ToFrameBytes()));

        Assert.Equal(countBeforeLogin + 1, sent.Count);
        var response = AssertHairFaceResponse(sent[countBeforeLogin], LegacyHairFaceStorageAction.Login);
        var item = Assert.Single(response.Items);
        Assert.Equal(BuyableHairstyleId, item.Id);
        Assert.Equal([0L, BoughtColorMask], item.Colors);
    }

    [Fact]
    public async Task HandlerLegacyHairFaceStorageBuyColorWithInsufficientMoneyKeepsColorsUnchangedAndLogsSkip()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(money: BuyableHairstyleCost + BuyableColorCost - 1, hair: 12, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LogInAsync(handler, connection, sent);

        await handler.HandleFrameAsync(connection, ReadFrame(BuyHairstyleRequest(BuyableHairstyleId).ToFrameBytes()));
        await handler.HandleFrameAsync(connection, ReadFrame(BuyColorRequest(BuyableHairstyleId, BuyableColorIndex).ToFrameBytes()));
        var countBeforeLogin = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LoginRequest().ToFrameBytes()));

        Assert.Equal(countBeforeLogin + 1, sent.Count);
        var response = AssertHairFaceResponse(sent[countBeforeLogin], LegacyHairFaceStorageAction.Login);
        var item = Assert.Single(response.Items);
        Assert.Equal(BuyableHairstyleId, item.Id);
        Assert.Equal([0L, InitialColorMask], item.Colors);
        Assert.Contains("skipped LegacyHairFaceStorage buy color", log.ToString());
        Assert.Contains("cost=150 money=149", log.ToString());
    }

    [Fact]
    public async Task HandlerLegacyHairFaceStorageEquipBoughtColorMarksColorEquippedForNextLogin()
    {
        using var log = new StringWriter();
        var handler = CreateHandler(money: 1_000, hair: 12, log);
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);

        await LogInAsync(handler, connection, sent);

        await handler.HandleFrameAsync(connection, ReadFrame(BuyHairstyleRequest(BuyableHairstyleId).ToFrameBytes()));
        await handler.HandleFrameAsync(connection, ReadFrame(EquipHairstyleRequest(BuyableHairstyleId).ToFrameBytes()));
        await handler.HandleFrameAsync(connection, ReadFrame(BuyColorRequest(BuyableHairstyleId, BuyableColorIndex).ToFrameBytes()));
        await handler.HandleFrameAsync(connection, ReadFrame(EquipColorRequest(BuyableHairstyleId, BuyableColorIndex).ToFrameBytes()));
        var countBeforeLogin = sent.Count;

        await handler.HandleFrameAsync(connection, ReadFrame(LoginRequest().ToFrameBytes()));

        Assert.Equal(countBeforeLogin + 1, sent.Count);
        var response = AssertHairFaceResponse(sent[countBeforeLogin], LegacyHairFaceStorageAction.Login);
        var item = Assert.Single(response.Items);
        Assert.Equal(BuyableHairstyleId, item.Id);
        Assert.True(item.Equipped);
        Assert.Equal([(long)BuyableColorIndex, BoughtColorMask], item.Colors);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dbRoot))
        {
            Directory.Delete(_dbRoot, recursive: true);
        }
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private WorldServerFrameHandler CreateHandler(long money, long hair, TextWriter log)
    {
        var usersRoot = Path.Combine(_dbRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(usersRoot, accountId: PlayerUid, uid: PlayerUid, money, hair);

        return new WorldServerFrameHandler(
            new CharacterSnapshotLoader(usersRoot),
            handoffService: null,
            CreateHairFaceCatalog(BuyableHairstyle()),
            log);
    }

    private static async Task LogInAsync(
        WorldServerFrameHandler handler,
        ConquerTcpConnection connection,
        List<byte[]> sent)
    {
        await handler.HandleFrameAsync(
            connection,
            ReadFrame(new LegacyMsgLoginGamePacket(PlayerUid).ToFrameBytes()));

        Assert.NotEmpty(sent);
    }

    private static LegacyHairFaceStoragePacket LoginRequest()
    {
        return LegacyHairFaceStoragePacket.EmptyLoginResponse();
    }

    private static LegacyHairFaceStoragePacket BuyHairstyleRequest(uint id)
    {
        return HairOperationRequest(LegacyHairFaceStorageAction.Buy, LegacyHairFaceStorageType.Hairstyle, id);
    }

    private static LegacyHairFaceStoragePacket EquipHairstyleRequest(uint id)
    {
        return HairOperationRequest(LegacyHairFaceStorageAction.Equip, LegacyHairFaceStorageType.Hairstyle, id);
    }

    private static LegacyHairFaceStoragePacket BuyColorRequest(uint hairstyleId, uint colorIndex)
    {
        return HairColorOperationRequest(LegacyHairFaceStorageAction.BuyColor, hairstyleId, colorIndex);
    }

    private static LegacyHairFaceStoragePacket EquipColorRequest(uint hairstyleId, uint colorIndex)
    {
        return HairColorOperationRequest(LegacyHairFaceStorageAction.EquipColor, hairstyleId, colorIndex);
    }

    private static LegacyHairFaceStoragePacket HairOperationRequest(
        LegacyHairFaceStorageAction action,
        LegacyHairFaceStorageType type,
        uint id)
    {
        return new LegacyHairFaceStoragePacket(
            action,
            new LegacyHairFaceStorageOperation((uint)type, id),
            HairColor: null,
            AvatarsOrHairTap: [],
            Items: []);
    }

    private static LegacyHairFaceStoragePacket HairColorOperationRequest(
        LegacyHairFaceStorageAction action,
        uint hairstyleId,
        uint colorIndex)
    {
        return new LegacyHairFaceStoragePacket(
            action,
            Hair: null,
            new LegacyHairFaceStorageOperation(hairstyleId, colorIndex),
            AvatarsOrHairTap: [],
            Items: []);
    }

    private static LegacyHairFaceStoragePacket AssertHairFaceResponse(
        byte[] bytes,
        LegacyHairFaceStorageAction action)
    {
        var frame = ReadFrame(bytes);
        Assert.Equal(WorldPacketOpcodes.LegacyHairFaceStorage, frame.Header.Opcode);
        Assert.True(LegacyHairFaceStoragePacket.TryParse(frame, out var response));
        Assert.Equal(action, response.Action);
        Assert.Empty(response.AvatarsOrHairTap);
        return response;
    }

    private static HairFaceStorageType BuyableHairstyle()
    {
        return new HairFaceStorageType(
            CategoryType: (int)LegacyHairFaceStorageType.Hairstyle,
            Id: (int)BuyableHairstyleId,
            Unknown0: 0,
            Name: "SourceBackedTestStyle",
            UnlockType: 0,
            RequiredValue: 0,
            Unknown1: 0,
            Cost: BuyableHairstyleCost,
            Unknown2: 0,
            LinkedColorIdsRaw: "",
            Sort: 0,
            SourceFile: "hairface_storage_type.txt",
            LineNumber: 1,
            RawLine: "0@@27@@0@@SourceBackedTestStyle@@0@@0@@0@@500@@0@@@@0@@",
            Fields: ["0", "27", "0", "SourceBackedTestStyle", "0", "0", "0", "500", "0", "", "0", ""]);
    }

    private static HairColorType BuyableHairColor()
    {
        return new HairColorType(
            HairColorId: (int)BuyableHairstyleId,
            ColorIndex: (int)BuyableColorIndex,
            UnlockType: 0,
            ItemTypeId: 3_326_778,
            Cost: BuyableColorCost,
            SourceFile: "hair_color_type.txt",
            LineNumber: 1,
            RawLine: "27@@5@@0@@3326778@@150",
            Fields: ["27", "5", "0", "3326778", "150"]);
    }

    private static WorldDataCatalog CreateHairFaceCatalog(HairFaceStorageType hairstyle)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z"),
                "fixture://hairface",
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
            hairColorTypes: [BuyableHairColor()],
            hairFaceStorageTypes: [hairstyle],
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

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private static void WriteSnapshot(string usersRoot, uint accountId, uint uid, long money, long hair)
    {
        File.WriteAllText(
            Path.Combine(usersRoot, accountId + ".ini"),
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
            Reborn=2
            Level=140
            Haire={{hair}}
            Experience=0
            PMap=1000
            PMapX=500
            PMapY=650
            Agility=36
            Strength=0
            Vitaliti=447
            Spirit=0
            Atributes=12
            MinHitPoints=61258
            MinMana=5000
            ConquerPoints=0
            BankCps=0
            BoundConquerPoints=0
            Money={{money}}
            PkPoints=0
            Online=7
            Activeness=0/80/0/

            [PaidNobility]
            Rank=King
            Identifier={{uid}}
            """);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static string Hex(byte[] bytes)
    {
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
