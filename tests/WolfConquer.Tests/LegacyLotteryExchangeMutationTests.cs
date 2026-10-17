using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyLotteryExchangeMutationTests : IDisposable
{
    private const uint LotteryCollectorWongNpcId = 3_952;
    private const uint MarketLadyLuckNpcId = 923;
    private const uint DisguiseAmuletItemId = 723_724;
    private const uint LifeFruitBasketItemId = 723_725;
    private const uint PenitenceAmuletItemId = 723_727;
    private const uint LotteryTicketItemId = 710_212;
    private const uint SmallLotteryTicketItemId = 711_504;
    private const long AccountId = 301;
    private const long CharacterUid = 1_294_700;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyLotteryExchangeMutationTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(1, DisguiseAmuletItemId)]
    [InlineData(2, LifeFruitBasketItemId)]
    [InlineData(3, PenitenceAmuletItemId)]
    public async Task CollectorSuccessfulOptionsPersistThenRemoveUpdateWalletAndSendExactDialog(
        byte optionId,
        uint sourceItemId)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var usersRoot = CreateUsersRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, sourceItemId),
            CreateInventoryItem(20, 720_001));
        WriteUserSnapshot(usersRoot, CharacterUid, conquerPoints: 100);
        var persistenceObservedBeforePacket = false;

        var result = await InvokeAsync(
            LotteryCollectorWongNpcId,
            optionId,
            originalItems,
            worldData: null,
            playerItemsRoot,
            usersRoot,
            conquerPoints: 100,
            onFirstSend: committedSession =>
            {
                persistenceObservedBeforePacket = true;
                Assert.Equal(115, committedSession.Character.ConquerPoints);
                Assert.DoesNotContain(
                    new PlayerItemSnapshotLoader(playerItemsRoot)
                        .LoadByCharacterUid(CharacterUid)
                        .InventoryItems,
                    item => item.Uid == 10);
                Assert.Contains("ConquerPoints=115", ReadUserSnapshot(usersRoot));
            });

        Assert.True(result.Handled);
        Assert.True(persistenceObservedBeforePacket);
        Assert.Equal(115, result.UpdatedSession.Character.ConquerPoints);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10);
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 20);

        Assert.Equal(6, result.Sent.Count);
        AssertRemoval(result.Sent[0], uid: 10);
        AssertConquerPoints(result.Sent[1], expected: 115);
        AssertExactDialog(
            result.Sent.Skip(2).ToArray(),
            "You receive 15 ConquerPoints for you item.",
            "Ok~thank~you.",
            avatarId: 10);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains("source=NpcHandler.LotteryCollectorWong", result.Log);
        Assert.Contains("sourceLines=40212-40269", result.Log);
        Assert.Contains($"itemId={sourceItemId}", result.Log);
        Assert.Contains("sourceOrder=remove-item+conquer-points+dialog", result.Log);
    }

    [Fact]
    public async Task CollectorUnboundGateCanRemoveEarlierBoundItemLikeSourceInventoryRemove()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var usersRoot = CreateUsersRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, DisguiseAmuletItemId, bound: 1),
            CreateInventoryItem(20, DisguiseAmuletItemId));
        WriteUserSnapshot(usersRoot, CharacterUid, conquerPoints: 25);

        var result = await InvokeAsync(
            LotteryCollectorWongNpcId,
            optionId: 1,
            originalItems,
            worldData: null,
            playerItemsRoot,
            usersRoot,
            conquerPoints: 25);

        Assert.Equal(40, result.UpdatedSession.Character.ConquerPoints);
        var remaining = Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(20U, remaining.Uid);
        Assert.Equal(0, remaining.Bound);
        AssertRemoval(result.Sent[0], uid: 10);
        Assert.Contains("removedBound=1", result.Log);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Fact]
    public async Task CollectorBoundOnlyMaterialFailsUnboundGateWithExactSourceDialog()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var usersRoot = CreateUsersRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, LifeFruitBasketItemId, stackSize: 5, bound: 1));
        WriteUserSnapshot(usersRoot, CharacterUid, conquerPoints: 80);

        var result = await InvokeAsync(
            LotteryCollectorWongNpcId,
            optionId: 2,
            originalItems,
            worldData: null,
            playerItemsRoot,
            usersRoot,
            conquerPoints: 80);

        AssertMutationFree(result, originalItems, conquerPoints: 80);
        AssertExactDialog(
            result.Sent,
            "You~do~not~have~what~I~want.~Whenever~you~have~one~of~DisguiseAmulets,~LifeFruitBaskets~or~PenitenceAmulets,~just~come~here.",
            "See~you~again.",
            avatarId: 10);
        AssertPersistedEquals(playerItemsRoot, originalItems);
        Assert.Contains("ConquerPoints=80", ReadUserSnapshot(usersRoot));
    }

    [Fact]
    public async Task CollectorMissingMaterialDialogPrecedesMissingPersistenceInfrastructure()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, 720_001));

        var result = await InvokeAsync(
            LotteryCollectorWongNpcId,
            optionId: 1,
            originalItems,
            worldData: null,
            playerItemsRoot: null,
            usersRoot: null,
            conquerPoints: 45,
            includeSessionStore: false);

        AssertMutationFree(result, originalItems, conquerPoints: 45);
        AssertExactDialog(
            result.Sent,
            "You~do~not~have~what~I~want.~Whenever~you~have~one~of~DisguiseAmulets,~LifeFruitBaskets~or~PenitenceAmulets,~just~come~here.",
            "See~you~again.",
            avatarId: 10);
        Assert.DoesNotContain("requires session, player-item, and character-wallet persistence", result.Log);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public async Task CollectorMissingPersistenceDependencyIsMutationFree(
        bool includeSessionStore,
        bool includePlayerItemsRoot,
        bool includeUsersRoot)
    {
        var playerItemsRoot = includePlayerItemsRoot ? CreatePlayerItemsRoot() : null;
        var usersRoot = includeUsersRoot ? CreateUsersRoot() : null;
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, PenitenceAmuletItemId));
        if (usersRoot is not null)
        {
            WriteUserSnapshot(usersRoot, CharacterUid, conquerPoints: 5);
        }

        var result = await InvokeAsync(
            LotteryCollectorWongNpcId,
            optionId: 3,
            originalItems,
            worldData: null,
            playerItemsRoot,
            usersRoot,
            conquerPoints: 5,
            includeSessionStore: includeSessionStore);

        AssertMutationFree(result, originalItems, conquerPoints: 5);
        AssertExactDialog(
            result.Sent,
            "That service is not available yet. No items, CPs, silver, or character state were changed.",
            "I see.",
            avatarId: 10);
        if (playerItemsRoot is not null)
        {
            AssertPersistedEquals(playerItemsRoot, originalItems);
        }

        Assert.Contains("requires session, player-item, and character-wallet persistence", result.Log);
    }

    [Fact]
    public async Task CollectorWalletFailureRollsBackFirstItemWriteAndSendsNothing()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var usersRoot = CreateUsersRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, DisguiseAmuletItemId));
        WriteUserSnapshot(usersRoot, CharacterUid + 1, conquerPoints: 200);
        var originalCharacterBytes = File.ReadAllBytes(GetUserSnapshotPath(usersRoot));

        var result = await InvokeAsync(
            LotteryCollectorWongNpcId,
            optionId: 1,
            originalItems,
            worldData: null,
            playerItemsRoot,
            usersRoot,
            conquerPoints: 200);

        AssertMutationFree(result, originalItems, conquerPoints: 200);
        Assert.Empty(result.Sent);
        AssertPersistedEquals(playerItemsRoot, originalItems);
        Assert.Equal(originalCharacterBytes, File.ReadAllBytes(GetUserSnapshotPath(usersRoot)));
        Assert.Contains("reason=uidMismatch", result.Log);
        Assert.Contains("itemRollback=complete", result.Log);
        Assert.Contains("characterRollback=complete", result.Log);
    }

    [Fact]
    public async Task CollectorMissingCharacterSnapshotAbortsBeforeItemWrite()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var usersRoot = CreateUsersRoot();
        Directory.CreateDirectory(usersRoot);
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, DisguiseAmuletItemId));

        var result = await InvokeAsync(
            LotteryCollectorWongNpcId,
            optionId: 1,
            originalItems,
            worldData: null,
            playerItemsRoot,
            usersRoot,
            conquerPoints: 210);

        AssertMutationFree(result, originalItems, conquerPoints: 210);
        Assert.Empty(result.Sent);
        AssertPersistedEquals(playerItemsRoot, originalItems);
        Assert.Contains("reason=character-preflight-FileNotFoundException", result.Log);
        Assert.DoesNotContain("itemRollback=", result.Log);
    }

    [Fact]
    public async Task CollectorItemWriteFailureDoesNotChangeWalletSessionOrPackets()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var usersRoot = CreateUsersRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, DisguiseAmuletItemId));
        WriteUserSnapshot(usersRoot, CharacterUid, conquerPoints: 300);

        var result = await InvokeAsync(
            LotteryCollectorWongNpcId,
            optionId: 1,
            originalItems,
            worldData: null,
            blockedPlayerItemsRoot,
            usersRoot,
            conquerPoints: 300,
            seedPlayerItems: false);

        AssertMutationFree(result, originalItems, conquerPoints: 300);
        Assert.Empty(result.Sent);
        Assert.Contains("ConquerPoints=300", ReadUserSnapshot(usersRoot));
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
        Assert.Contains("LotteryCollectorWong exchange persistence", result.Log);
    }

    [Fact]
    public async Task MarketExchangePersistsBeforePacketsRemovesThenAddsAuthoritativeUnboundRewardWithoutDialog()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, LotteryTicketItemId),
            CreateInventoryItem(20, 720_001));
        var persistenceObservedBeforePacket = false;

        var result = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket", durability: 77)),
            playerItemsRoot,
            usersRoot: null,
            onFirstSend: committedSession =>
            {
                persistenceObservedBeforePacket = true;
                var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
                Assert.DoesNotContain(persisted.InventoryItems, item => item.Uid == 10);
                Assert.Single(persisted.InventoryItems, item => item.ItemId == SmallLotteryTicketItemId);
                Assert.Single(committedSession.PlayerItems.InventoryItems, item => item.ItemId == SmallLotteryTicketItemId);
            });

        Assert.True(result.Handled);
        Assert.True(persistenceObservedBeforePacket);
        Assert.Equal(2, result.Sent.Count);
        AssertRemoval(result.Sent[0], uid: 10);
        var rewardPacket = AssertGameItem(result.Sent[1]);
        Assert.Equal(21U, rewardPacket.Uid);
        Assert.Equal(SmallLotteryTicketItemId, rewardPacket.ItemId);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, rewardPacket.Mode);
        Assert.Equal(77, rewardPacket.Durability);
        Assert.Equal(77, rewardPacket.MaximumDurability);
        Assert.Equal(0, rewardPacket.Bound);
        Assert.DoesNotContain(result.Sent, IsNpcReply);

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == SmallLotteryTicketItemId);
        Assert.Equal(0, reward.Bound);
        Assert.Equal(77, reward.Durability);
        Assert.InRange(reward.Color, 3U, 8U);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains("source=NpcHandler.MarketLadyLuck", result.Log);
        Assert.Contains("sourceLines=40295-40338", result.Log);
        Assert.Contains("sourceOrder=remove-item+add-item no-success-dialog=true", result.Log);
    }

    [Fact]
    public async Task MarketUnboundGateCanRemoveEarlierBoundTicketLikeSourceInventoryRemove()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, LotteryTicketItemId, bound: 1),
            CreateInventoryItem(20, LotteryTicketItemId));

        var result = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket")),
            playerItemsRoot,
            usersRoot: null);

        AssertRemoval(result.Sent[0], uid: 10);
        var remainingTicket = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == LotteryTicketItemId);
        Assert.Equal(20U, remainingTicket.Uid);
        Assert.Equal(0, remainingTicket.Bound);
        Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == SmallLotteryTicketItemId && item.Bound == 0);
        Assert.Contains("removedBound=1", result.Log);
    }

    [Fact]
    public async Task MarketBoundOnlyTicketFailsUnboundGateWithExactSourceDialog()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, LotteryTicketItemId, stackSize: 2, bound: 1));

        var result = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket")),
            playerItemsRoot,
            usersRoot: null);

        AssertMutationFree(result, originalItems, conquerPoints: 0);
        AssertExactDialog(
            result.Sent,
            "You don`t have any Lottery Tickets to exchange for the Small Lottery Tickets.",
            "Oops.",
            avatarId: 123);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MarketFullInventoryWithSingletonTicketUsesFreedSlotAndStillGrantsReward()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(1, LotteryTicketItemId),
        };
        inventory.AddRange(Enumerable.Range(2, 39).Select(index =>
            CreateInventoryItem((uint)index, checked(800_000U + (uint)index))));
        var originalItems = CreatePlayerItems(inventory.ToArray());

        var result = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket")),
            playerItemsRoot,
            usersRoot: null);

        Assert.Equal(40, originalItems.InventoryItems.Count);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 1);
        Assert.Single(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == SmallLotteryTicketItemId);
        Assert.Equal(2, result.Sent.Count);
    }

    [Fact]
    public async Task MarketFullInventoryWithStackedTicketFencesWholeExchangeWithoutLoss()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(1, LotteryTicketItemId, stackSize: 2),
        };
        inventory.AddRange(Enumerable.Range(2, 39).Select(index =>
            CreateInventoryItem((uint)index, checked(810_000U + (uint)index))));
        var originalItems = CreatePlayerItems(inventory.ToArray());

        var result = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket")),
            playerItemsRoot,
            usersRoot: null);

        AssertMutationFree(result, originalItems, conquerPoints: 0);
        Assert.Empty(result.Sent);
        AssertPersistedEquals(playerItemsRoot, originalItems);
        Assert.Contains("reason=inventory-full-after-planned-ticket-removal", result.Log);
        Assert.Contains("no ticket was consumed and no reward was granted", result.Log);
    }

    [Fact]
    public async Task MarketMissingRewardDataDoesNotConsumeTicketOrSendMutation()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, LotteryTicketItemId));

        var result = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(),
            playerItemsRoot,
            usersRoot: null);

        AssertMutationFree(result, originalItems, conquerPoints: 0);
        Assert.Empty(result.Sent);
        AssertPersistedEquals(playerItemsRoot, originalItems);
        Assert.Contains("reason=itemtype-missing-or-invalid", result.Log);
    }

    [Fact]
    public async Task MarketExhaustedUidDoesNotConsumeTicketOrSendMutation()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, LotteryTicketItemId),
            CreateInventoryItem(uint.MaxValue, 720_001));

        var result = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket")),
            playerItemsRoot,
            usersRoot: null);

        AssertMutationFree(result, originalItems, conquerPoints: 0);
        Assert.Empty(result.Sent);
        AssertPersistedEquals(playerItemsRoot, originalItems);
        Assert.Contains("reason=item-uid-exhausted", result.Log);
    }

    [Fact]
    public async Task MarketMissingSessionStoreOrWriteAuthorityDoesNotConsumeOrGrant()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, LotteryTicketItemId));
        var noSessions = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket")),
            playerItemsRoot,
            usersRoot: null,
            includeSessionStore: false);

        AssertMutationFree(noSessions, originalItems, conquerPoints: 0);
        AssertExactDialog(
            noSessions.Sent,
            "That service is not available yet. No items, CPs, silver, or character state were changed.",
            "I see.",
            avatarId: 123);
        AssertPersistedEquals(playerItemsRoot, originalItems);

        Directory.CreateDirectory(_root);
        var blockedRoot = Path.Combine(_root, "blocked-market-player-items-root");
        File.WriteAllText(blockedRoot, "not a directory");
        var blockedWrite = await InvokeAsync(
            MarketLadyLuckNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(SmallLotteryTicketItemId, "SmallLotteryTicket")),
            blockedRoot,
            usersRoot: null,
            seedPlayerItems: false);

        AssertMutationFree(blockedWrite, originalItems, conquerPoints: 0);
        Assert.Empty(blockedWrite.Sent);
        Assert.Equal("not a directory", File.ReadAllText(blockedRoot));
        Assert.Contains("MarketLadyLuck exchange persistence", blockedWrite.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        string? playerItemsRoot,
        string? usersRoot,
        long conquerPoints = 0,
        bool includeSessionStore = true,
        bool seedPlayerItems = true,
        Action<WorldSession>? onFirstSend = null)
    {
        if (seedPlayerItems && !string.IsNullOrWhiteSpace(playerItemsRoot))
        {
            new PlayerItemSnapshotLoader(playerItemsRoot).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var sessions = new WorldSessionStore();
        ConquerTcpConnection? connection = null;
        var firstSend = true;
        connection = new ConquerTcpConnection(
            null,
            new IPEndPoint(IPAddress.Loopback, 9958),
            (frame, _) =>
            {
                if (firstSend)
                {
                    firstSend = false;
                    Assert.True(sessions.TryGet(connection!, out var committedSession));
                    onFirstSend?.Invoke(committedSession!);
                }

                sent.Add(frame.ToArray());
                return ValueTask.CompletedTask;
            });
        var session = CreateSession(originalItems, usersRoot, conquerPoints);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyJailGuildServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: includeSessionStore ? sessions : null,
                PlayerItemsRoot: playerItemsRoot),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        return new InvokeResult(
            handled,
            sent,
            log.ToString(),
            session,
            updatedSession!);
    }

    private static WorldSession CreateSession(
        PlayerItemSnapshot playerItems,
        string? usersRoot,
        long conquerPoints)
    {
        return new WorldSession(
            AccountId,
            "lottery-exchange-test",
            new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "LotteryTester",
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
                ConquerPoints: conquerPoints,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["ConquerPoints"] = conquerPoints.ToString(CultureInfo.InvariantCulture),
                },
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            playerItems,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty,
            UsersRoot: usersRoot);
    }

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private string CreateUsersRoot()
    {
        return Path.Combine(_root, "Users-" + Guid.NewGuid().ToString("N"));
    }

    private static void WriteUserSnapshot(string usersRoot, long uid, long conquerPoints)
    {
        Directory.CreateDirectory(usersRoot);
        File.WriteAllLines(
            Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini"),
            [
                "[Character]",
                "UID=" + uid.ToString(CultureInfo.InvariantCulture),
                "ConquerPoints=" + conquerPoints.ToString(CultureInfo.InvariantCulture),
                "Note=keep-me",
            ]);
    }

    private static string ReadUserSnapshot(string usersRoot)
    {
        return File.ReadAllText(GetUserSnapshotPath(usersRoot));
    }

    private static string GetUserSnapshotPath(string usersRoot)
    {
        return Path.Combine(usersRoot, AccountId.ToString(CultureInfo.InvariantCulture) + ".ini");
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

    private static void AssertMutationFree(
        InvokeResult result,
        PlayerItemSnapshot originalItems,
        long conquerPoints)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(conquerPoints, result.UpdatedSession.Character.ConquerPoints);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
    }

    private static void AssertPersistedEquals(string playerItemsRoot, PlayerItemSnapshot expected)
    {
        var persisted = new PlayerItemSnapshotLoader(playerItemsRoot).LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(expected, persisted);
    }

    private static void AssertPlayerItemsEqual(PlayerItemSnapshot expected, PlayerItemSnapshot actual)
    {
        Assert.Equal(
            PlayerItemSnapshotLoader.ToLegacyBytes(expected),
            PlayerItemSnapshotLoader.ToLegacyBytes(actual));
    }

    private static void AssertRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(0UL, packet.DwParam);
        Assert.Equal(0U, packet.DwParam2);
        Assert.Equal(0U, packet.DwParam3);
        Assert.Equal(0U, packet.DwParam4);
    }

    private static void AssertConquerPoints(byte[] bytes, ulong expected)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal((uint)CharacterUid, packet.Uid);
        var update = Assert.Single(packet.Updates);
        Assert.Equal(LegacyMsgUpdateDataType.ConquerPoints, update.Type);
        Assert.Equal(expected, update.Value);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static void AssertExactDialog(
        IReadOnlyList<byte[]> sent,
        string text,
        string optionText,
        ushort avatarId)
    {
        Assert.Equal(4, sent.Count);
        var dialog = AssertNpcReply(sent[0], LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(text, dialog.Text);
        var option = AssertNpcReply(sent[1], LegacyNpcReplyInteractType.Option);
        Assert.Equal(LegacyNpcReplyPacket.ClosedOptionId, option.OptionId);
        Assert.Equal(optionText, option.Text);
        var avatar = AssertNpcReply(sent[2], LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(avatarId, avatar.InputMaxLength);
        AssertNpcReply(sent[3], LegacyNpcReplyInteractType.Finish);
    }

    private static LegacyNpcReplyPacket AssertNpcReply(
        byte[] bytes,
        LegacyNpcReplyInteractType expectedType)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedType, packet.InteractType);
        return packet;
    }

    private static bool IsNpcReply(byte[] bytes)
    {
        return LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out _);
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
                DateTimeOffset.Parse("2026-07-11T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://lottery-exchange",
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

    private static ItemType CreateItemType(uint itemTypeId, string name, uint durability = 1)
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
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession);
}
