using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyItemPackNpcTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyItemPackNpcTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(3_306_562U, 3, 4_010_201U, 1)]
    [InlineData(3_306_363U, 54, 4_034_001U, 1)]
    [InlineData(3_306_563U, 25, 4_028_001U, 1)]
    public async Task RuneSelectionPackItemUseGrantsSourceMappedReward(
        uint packItemId,
        byte optionId,
        uint expectedRewardItemId,
        byte expectedBound)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var pack = new PlayerItemSnapshotItem
        {
            Uid = 501,
            ItemId = packItemId,
            StackSize = 1,
        };
        var session = CreateSession() with
        {
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [pack] },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(CreateItemUse(pack.Uid).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.Equal(packItemId, activeSession?.ActiveNpcId);
        Assert.Contains("RuneSelectionPack", log.ToString());

        var countBeforeReward = sent.Count;
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId, interactType: 1, npcId: 0)));

        Assert.Equal(2, sent.Count - countBeforeReward);
        AssertRemoveInventory(sent[countBeforeReward], pack.Uid);
        var reward = AssertGameItem(sent[countBeforeReward + 1], expectedRewardItemId);
        Assert.Equal(expectedBound, reward.Bound);
        Assert.True(sessions.TryGet(connection, out var rewardedSession));
        Assert.DoesNotContain(rewardedSession!.PlayerItems.InventoryItems, item => item.ItemId == packItemId);
        Assert.Contains(rewardedSession.PlayerItems.InventoryItems, item => item.ItemId == expectedRewardItemId);
    }

    [Fact]
    public async Task AnimaItemUseOpensSourceDialogAndEmbedWindowReply()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var anima = new PlayerItemSnapshotItem
        {
            Uid = 601,
            ItemId = 4_200_003,
            StackSize = 1,
        };
        var session = CreateSession() with
        {
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [anima] },
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            ReadFrame(CreateItemUse(anima.Uid).ToFrameBytes()));

        Assert.Equal(9, sent.Count);
        var first = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Contains("Anima contains everlasting vital power", first.Text);
        var yes = AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Option);
        Assert.Equal(1, yes.OptionId);
        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.Equal(anima.ItemId, activeSession?.ActiveNpcId);

        var countBeforeUsePage = sent.Count;
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: activeSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 1, interactType: 1, npcId: 0)));

        Assert.Equal(7, sent.Count - countBeforeUsePage);
        var embed = AssertNpcReply(sent[countBeforeUsePage + 3], LegacyNpcReplyInteractType.Option);
        Assert.Equal(5, embed.OptionId);
        Assert.Equal("Embed~Anima.", embed.Text);

        Assert.True(sessions.TryGet(connection, out var usePageSession));
        var countBeforeEmbed = sent.Count;
        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: usePageSession, Sessions: sessions),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 5, interactType: 1, npcId: 0)));

        Assert.Single(sent.Skip(countBeforeEmbed));
        var action = AssertAction(sent[countBeforeEmbed]);
        Assert.Equal(MsgActionType.OpenClientWindow, action.Type);
        Assert.Equal(930U, action.DwParam);
        Assert.Contains("source=NpcHandler.Animas", log.ToString());
    }

    [Fact]
    public async Task AnimaConquerPointExchangePersistsWalletUpdateAndLogsBalance()
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var usersRoot = Path.Combine(_root, "Users");
        var playerItemsRoot = Path.Combine(_root, "PlayerItems");
        Directory.CreateDirectory(usersRoot);
        Directory.CreateDirectory(playerItemsRoot);
        File.WriteAllText(
            Path.Combine(usersRoot, "100.ini"),
            """
            [Character]
            UID=1293600
            Name=HeroOne
            ConquerPoints=10
            """);
        var anima = new PlayerItemSnapshotItem
        {
            Uid = 701,
            ItemId = 4_200_003,
            StackSize = 1,
        };
        var baseSession = CreateSession();
        var session = baseSession with
        {
            AccountId = 100,
            Character = baseSession.Character with { ConquerPoints = 10 },
            PlayerItems = PlayerItemSnapshot.Empty with { InventoryItems = [anima] },
            UsersRoot = usersRoot,
        };
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);
        var registry = PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);
        var worldData = CreateWorldDataCatalog(CreateItemType(anima.ItemId, "P3Anima", conquerPointsWorth: 101));

        await registry.HandleAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, WorldData: worldData, Sessions: sessions),
            ReadFrame(CreateItemUse(anima.Uid).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var activeSession));
        Assert.NotNull(activeSession);
        var countBeforeExchange = sent.Count;

        await registry.HandleAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: activeSession,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            ReadFrame(BuildLegacyNpcDialogReply(optionId: 7, interactType: 1, npcId: 0)));

        Assert.Equal(3, sent.Count - countBeforeExchange);
        AssertRemoveInventory(sent[countBeforeExchange], anima.Uid);
        AssertScalarUpdate(sent[countBeforeExchange + 1], LegacyMsgUpdateDataType.ConquerPoints, 110);
        var message = AssertNpcReply(sent[countBeforeExchange + 2], LegacyNpcReplyInteractType.MessageBox);
        Assert.Contains("100 CPs", message.Text);
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(110, updatedSession.Character.ConquerPoints);
        Assert.DoesNotContain(updatedSession.PlayerItems.InventoryItems, item => item.ItemId == anima.ItemId);

        Assert.Contains("ConquerPoints=110", File.ReadAllText(Path.Combine(usersRoot, "100.ini")));
        var persistedItems = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(updatedSession.Character.Uid);
        Assert.DoesNotContain(persistedItems.InventoryItems, item => item.ItemId == anima.ItemId);
        var output = log.ToString();
        Assert.Contains("WorldServer persisted LegacyNpc item-pack character", output);
        Assert.Contains("sourceConquerPointsWorth=101", output);
        Assert.Contains("tunedConquerPoints=100", output);
        Assert.Contains("updatedConquerPoints=110", output);
        Assert.Contains("walletUpdate=LegacyMsgUpdate.ConquerPoints", output);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private static MsgItemUsagePacket CreateItemUse(uint itemUid)
    {
        return new MsgItemUsagePacket(
            itemUid,
            DwParam: 0,
            MsgItemUsageAction.Equip,
            Timestamp: 0,
            DwParam2: 0,
            DwParam3: 0,
            DwParam4: 0);
    }

    private static WorldSession CreateSession()
    {
        return new WorldSession(
            AccountId: 1_293_600,
            Username: "user02",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: "HeroOne",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 380,
                PreviousMapId: 1002,
                PreviousMapX: 577,
                PreviousMapY: 586,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static byte[] BuildLegacyNpcDialogReply(byte optionId, byte interactType, uint npcId)
    {
        var writer = new PacketWriter(WorldPacketOpcodes.LegacyNpcReply, payloadLength: 11);
        writer.WriteUInt32(0, npcId);
        writer.WriteUInt16(4, 0);
        writer.Payload[6] = optionId;
        writer.Payload[7] = interactType;
        writer.Payload[8] = 0;
        writer.WriteUInt16(9, 0);
        return writer.ToArray();
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes, LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes, uint expectedItemId)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedItemId, packet.ItemId);
        return packet;
    }

    private static MsgActionPacket AssertAction(byte[] bytes)
    {
        Assert.True(MsgActionPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertScalarUpdate(byte[] bytes, LegacyMsgUpdateDataType dataType, ulong value)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Single(packet.Updates);
        Assert.Equal(new LegacyMsgUpdateEntry(dataType, value), packet.Updates[0]);
    }

    private static void AssertRemoveInventory(byte[] bytes, uint itemUid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(itemUid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-06-05T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://item-pack",
                new Dictionary<string, int>()),
            clientConfigs: [],
            items: items,
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

    private static ItemType CreateItemType(uint itemTypeId, string name, uint conquerPointsWorth)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.ConquerPointsWorthFieldIndex] = conquerPointsWorth.ToString(CultureInfo.InvariantCulture);
        fields[ItemType.DurabilityFieldIndex] = "1";
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
