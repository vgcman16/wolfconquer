using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyGoldPrizeTokenExchangeTests : IDisposable
{
    private const uint GoldPrizeTokenNpcId = 4_294_967_269;
    private const uint GoldPrizeTokenItemId = 723_467;
    private const uint GoldPrizeRewardItemId = 2_100_075;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyGoldPrizeTokenExchangeTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task OptionOneBroadcastsThenConsumesThreeUnboundUnitsAuditsAndAddsRewardWithoutDialog()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, GoldPrizeTokenItemId, stackSize: 5, bound: 1),
            CreateInventoryItem(20, GoldPrizeTokenItemId, stackSize: 2),
            CreateInventoryItem(30, GoldPrizeTokenItemId),
            CreateInventoryItem(40, 720_001));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GoldPrizeRewardItemId, "GoldPrize", durability: 321)),
            playerItemsRoot,
            includeSpectator: true);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);

        var boundTokens = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 10);
        Assert.Equal(GoldPrizeTokenItemId, boundTokens.ItemId);
        Assert.Equal(5, boundTokens.StackSize);
        Assert.Equal(1, boundTokens.Bound);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid is 20 or 30);

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == GoldPrizeRewardItemId);
        Assert.Equal(41U, reward.Uid);
        Assert.Equal(321, reward.Durability);
        Assert.Equal(321, reward.MaximumDurability);
        Assert.Equal(0, reward.Position);
        Assert.Equal(0, reward.Bound);
        Assert.Equal(1, reward.StackSize);

        const string expectedGlobal =
            "Congratulation to GoldPrizeTester ! he/she managed to open the legendary LordToken and received 1 GoldPrize.";
        Assert.Equal(5, result.ActorSent.Count);
        AssertGlobalTalk(result.ActorSent[0], expectedGlobal);
        AssertTokenUpdate(result.ActorSent[1], uid: 20, stackSize: 1);
        AssertTokenRemoval(result.ActorSent[2], uid: 20);
        AssertTokenRemoval(result.ActorSent[3], uid: 30);

        var rewardPacket = AssertGameItem(result.ActorSent[4]);
        Assert.Equal(41U, rewardPacket.Uid);
        Assert.Equal(GoldPrizeRewardItemId, rewardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        Assert.Equal(321, rewardPacket.Durability);
        Assert.Equal(321, rewardPacket.MaximumDurability);
        Assert.Equal(0, rewardPacket.Bound);
        Assert.Equal(1, rewardPacket.StackSize);
        Assert.DoesNotContain(result.ActorSent, IsNpcReply);

        Assert.Single(result.SpectatorSent);
        AssertGlobalTalk(result.SpectatorSent[0], expectedGlobal);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains("TokenGoldPrize audit", result.Log);
        Assert.Contains("playerName=GoldPrizeTester", result.Log);
        Assert.Contains("characterUid=1293600", result.Log);
        Assert.Contains("source=NpcHandler.GoldPrizeToken", result.Log);
        Assert.Contains("sourceLines=24153-24181", result.Log);
        Assert.Contains("sourceOrder=global-talk+remove-three+audit+add-item", result.Log);
        Assert.Contains("no-success-dialog=true", result.Log);
    }

    [Fact]
    public async Task BoundTokensNeverSatisfyOuterUnboundGateOrGetConsumed()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, GoldPrizeTokenItemId, stackSize: 10, bound: 1),
            CreateInventoryItem(20, GoldPrizeTokenItemId, stackSize: 2));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GoldPrizeRewardItemId, "GoldPrize")),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        AssertExactDialog(
            result.ActorSent,
            "Sorry, you do not have 3 Lord Tokens.",
            "I~see.");
        AssertNoExchangeFrames(result.ActorSent);
        AssertPersistedEquals(playerItemsRoot, originalItems);
        Assert.DoesNotContain("TokenGoldPrize audit playerName=", result.Log);
    }

    [Fact]
    public async Task FullInventoryGatePrecedesItemTypeLookupAndAnyMutation()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(10, GoldPrizeTokenItemId, stackSize: 3),
        };
        inventory.AddRange(
            Enumerable.Range(0, 39)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        AssertExactDialog(
            result.ActorSent,
            "Please make one more space in your inventory.",
            "I~see.");
        AssertNoExchangeFrames(result.ActorSent);
        Assert.DoesNotContain("reason=itemtype-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingRewardItemTypeIsMutationFreeAndEmitsNoBroadcastAuditOrDialog()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, GoldPrizeTokenItemId, stackSize: 3));
        var result = await InvokeAsync(originalItems, CreateWorldDataCatalog(), playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.ActorSent);
        Assert.Contains("rewardItemId=2100075", result.Log);
        Assert.Contains("reason=itemtype-missing", result.Log);
        Assert.Contains("no tokens were consumed", result.Log);
        Assert.DoesNotContain("TokenGoldPrize audit playerName=", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingPlayerItemsRootKeepsCompatibilityGapMutationFree()
    {
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, GoldPrizeTokenItemId, stackSize: 3));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GoldPrizeRewardItemId, "GoldPrize")),
            playerItemsRoot: null,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        AssertExactDialog(
            result.ActorSent,
            "Gold Prize Token exchange is unavailable here until token removal, reward grants, broadcast, and audit logging are modeled safely.",
            "I~see.");
        AssertNoExchangeFrames(result.ActorSent);
        Assert.Contains("PARITY-GAP LegacyNpc RealmTransferServices", result.Log);
        Assert.Contains("sourceLines=24120-24189", result.Log);
        Assert.DoesNotContain("TokenGoldPrize audit playerName=", result.Log);
    }

    [Fact]
    public async Task MissingSessionStoreDoesNotPersistMutateBroadcastAuditOrReply()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, GoldPrizeTokenItemId, stackSize: 3));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GoldPrizeRewardItemId, "GoldPrize")),
            playerItemsRoot,
            includeSessionStore: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.ActorSent);
        Assert.Contains("reason=session-store-missing", result.Log);
        Assert.Contains("no item persistence, broadcast, audit, or session mutation occurred", result.Log);
        Assert.DoesNotContain("TokenGoldPrize audit playerName=", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task PersistenceWriteFailureDoesNotCommitMutateBroadcastAuditOrReply()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, GoldPrizeTokenItemId, stackSize: 3));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GoldPrizeRewardItemId, "GoldPrize")),
            blockedPlayerItemsRoot,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.ActorSent);
        Assert.Contains("Gold Prize Token exchange persistence", result.Log);
        Assert.Contains("no session mutation, broadcast, or audit was committed", result.Log);
        Assert.DoesNotContain("TokenGoldPrize audit playerName=", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
    }

    [Fact]
    public async Task ExhaustedItemUidDoesNotConsumeTokensBroadcastAuditOrReply()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(uint.MaxValue, GoldPrizeTokenItemId, stackSize: 3));
        var result = await InvokeAsync(
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GoldPrizeRewardItemId, "GoldPrize")),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.ActorSent);
        Assert.Contains("reason=item-uid-exhausted", result.Log);
        Assert.Contains("no tokens were consumed", result.Log);
        Assert.DoesNotContain("TokenGoldPrize audit playerName=", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        string? playerItemsRoot,
        bool seedPersistence = true,
        bool includeSessionStore = true,
        bool includeSpectator = false)
    {
        if (seedPersistence)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot!).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var actorSent = new List<byte[]>();
        var spectatorSent = new List<byte[]>();
        var actorConnection = CreateSendingConnection(actorSent);
        var session = CreateSession(originalItems);
        var sessions = new WorldSessionStore();
        sessions.Set(actorConnection, session);
        if (includeSpectator)
        {
            var spectatorConnection = CreateSendingConnection(spectatorSent);
            var spectator = session with
            {
                Character = session.Character with
                {
                    Uid = CharacterUid + 1,
                    Name = "GoldPrizeObserver",
                },
            };
            sessions.Set(spectatorConnection, spectator);
        }

        var handled = await WorldPacketHandlers.TryHandleLegacyRealmTransferServicesNpcAsync(
            new WorldPacketHandlerContext(
                actorConnection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: playerItemsRoot),
            session,
            GoldPrizeTokenNpcId,
            optionId: 1,
            CancellationToken.None);

        Assert.True(sessions.TryGet(actorConnection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(
            handled,
            actorSent,
            spectatorSent,
            log.ToString(),
            session,
            updatedSession);
    }

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private static WorldSession CreateSession(PlayerItemSnapshot playerItems)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "gold-prize-token-exchange-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "GoldPrizeTester",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7_051,
                FirstClass: 7_051,
                SecondClass: 0,
                Reborn: 0,
                Level: 140,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
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
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: playerItems,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshot CreatePlayerItems(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem CreateInventoryItem(
        uint uid,
        uint itemId,
        ushort stackSize = 1,
        byte bound = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 1,
            MaximumDurability = 1,
            Position = 0,
            StackSize = stackSize,
            Bound = bound,
        };
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static void AssertMutationFree(InvokeResult result, PlayerItemSnapshot originalItems)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
    }

    private static void AssertPersistedEquals(string playerItemsRoot, PlayerItemSnapshot expected)
    {
        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(expected, persisted);
    }

    private static void AssertGlobalTalk(byte[] bytes, string expectedMessage)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType);
        Assert.Equal("SYSTEM", packet.Content[0]);
        Assert.Equal("ALL", packet.Content[1]);
        Assert.Equal(expectedMessage, packet.Content[3]);
    }

    private static void AssertTokenUpdate(byte[] bytes, uint uid, ushort stackSize)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(GoldPrizeTokenItemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        Assert.Equal(stackSize, packet.StackSize);
    }

    private static void AssertTokenRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(0UL, packet.DwParam);
        Assert.Equal(0U, packet.DwParam2);
        Assert.Equal(0U, packet.DwParam3);
        Assert.Equal(0U, packet.DwParam4);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string expectedText,
        string expectedOption)
    {
        Assert.Equal(3, sent.Count);

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[0]), out var dialog));
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, dialog.InteractType);
        Assert.Equal(expectedText, dialog.Text);

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[1]), out var option));
        Assert.Equal(LegacyNpcReplyInteractType.Option, option.InteractType);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(expectedOption, option.Text);

        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(sent[2]), out var finish));
        Assert.Equal(LegacyNpcReplyInteractType.Finish, finish.InteractType);
    }

    private static void AssertNoExchangeFrames(IReadOnlyList<byte[]> sent)
    {
        Assert.DoesNotContain(sent, IsGlobalMessage);
        Assert.DoesNotContain(sent, IsGameItem);
        Assert.DoesNotContain(sent, IsItemUsage);
    }

    private static bool IsGlobalMessage(byte[] bytes)
    {
        return LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsGameItem(byte[] bytes)
    {
        return LegacyGameItemPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsItemUsage(byte[] bytes)
    {
        return MsgItemUsagePacket.TryParse(ReadFrame(bytes), out _);
    }

    private static bool IsNpcReply(byte[] bytes)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _);
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-10T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://gold-prize-token-exchange",
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
            activityTasks: new ActivityTaskCatalog(
                new ActivityTaskMetadata(0, string.Empty, 0, []),
                [],
                "fixture://activity"),
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
            neiGongHeader: new NeiGongHeader(
                0,
                [],
                "fixture://neigong",
                0,
                string.Empty,
                new Dictionary<string, string>()),
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

    private static ItemType CreateItemType(
        uint itemTypeId,
        string name,
        uint durability = 1)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = name;
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), name, fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData("fixture://" + name, [], [], [], info: null, pole: null, conductors: []);
    }

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> ActorSent,
        IReadOnlyList<byte[]> SpectatorSent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession);
}
