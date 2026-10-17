using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySteedPackMutationTests : IDisposable
{
    private const uint Steed3PackNpcId = 4_294_967_264;
    private const uint Steed1PackNpcId = 4_294_967_265;
    private const uint RareSteedPack6NpcId = 4_294_967_280;
    private const uint NobleSteedPackNpcId = 4_294_967_281;
    private const uint Steed6NpcId = 4_294_967_289;
    private const uint Steed3NpcId = 4_294_967_290;
    private const uint Steed1NpcId = 4_294_967_291;
    private const uint Steed3PackItemId = 3_007_283;
    private const uint Steed1PackItemId = 3_007_287;
    private const uint Steed1ItemId = 728_525;
    private const uint Steed3ItemId = 728_526;
    private const uint Steed6ItemId = 728_527;
    private const uint RareSteedPack6ItemId = 3_001_406;
    private const uint NobleSteedPackItemId = 729_990;
    private const uint GrantedSteedItemId = 300_000;
    private const long CharacterUid = 1_293_600;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacySteedPackMutationTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<uint, uint, byte, byte, bool, byte, byte, byte> SelectionCases => new()
    {
        { Steed3PackNpcId, Steed3PackItemId, 2, 3, true, 150, byte.MaxValue, 0 },
        { Steed3PackNpcId, Steed3PackItemId, 6, 3, true, 0, 150, byte.MaxValue },
        { Steed3PackNpcId, Steed3PackItemId, 11, 3, true, byte.MaxValue, 0, 150 },
        { Steed1PackNpcId, Steed1PackItemId, 2, 1, true, 150, byte.MaxValue, 0 },
        { Steed1PackNpcId, Steed1PackItemId, 6, 1, true, 0, 150, byte.MaxValue },
        { Steed1PackNpcId, Steed1PackItemId, 11, 1, true, byte.MaxValue, 0, 150 },

        { Steed1NpcId, Steed1ItemId, 1, 1, false, byte.MaxValue, 0, 150 },
        { Steed1NpcId, Steed1ItemId, 2, 1, false, 0, 150, byte.MaxValue },
        { Steed1NpcId, Steed1ItemId, 3, 1, false, 150, byte.MaxValue, 0 },
        { Steed3NpcId, Steed3ItemId, 1, 3, false, byte.MaxValue, 0, 150 },
        { Steed3NpcId, Steed3ItemId, 2, 3, false, 0, 150, byte.MaxValue },
        { Steed3NpcId, Steed3ItemId, 3, 3, false, 150, byte.MaxValue, 0 },
        { Steed6NpcId, Steed6ItemId, 1, 6, false, byte.MaxValue, 0, 150 },
        { Steed6NpcId, Steed6ItemId, 2, 6, false, 0, 150, byte.MaxValue },
        { Steed6NpcId, Steed6ItemId, 3, 6, false, 150, byte.MaxValue, 0 },

        { RareSteedPack6NpcId, RareSteedPack6ItemId, 1, 6, false, 201, 135, 187 },
        { RareSteedPack6NpcId, RareSteedPack6ItemId, 2, 6, false, 184, 63, 62 },
        { RareSteedPack6NpcId, RareSteedPack6ItemId, 3, 6, false, 137, 156, 148 },
        { RareSteedPack6NpcId, RareSteedPack6ItemId, 4, 6, false, 122, 134, 147 },
        { RareSteedPack6NpcId, RareSteedPack6ItemId, 5, 6, false, 46, 39, 142 },
        { NobleSteedPackNpcId, NobleSteedPackItemId, 1, 6, false, 201, 135, 187 },
        { NobleSteedPackNpcId, NobleSteedPackItemId, 2, 6, false, 184, 63, 62 },
        { NobleSteedPackNpcId, NobleSteedPackItemId, 3, 6, false, 137, 156, 148 },
        { NobleSteedPackNpcId, NobleSteedPackItemId, 4, 6, false, 122, 134, 147 },
        { NobleSteedPackNpcId, NobleSteedPackItemId, 5, 6, false, 46, 39, 142 },
    };

    [Theory]
    [MemberData(nameof(SelectionCases))]
    public async Task EverySourceSelectionPersistsAndEmitsExactSteedMutationPackets(
        uint npcId,
        uint packItemId,
        byte optionId,
        byte plus,
        bool bound,
        byte progressGreen,
        byte progressBlue,
        byte progressRed)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, 720_001),
            CreateInventoryItem(50, packItemId, bound: bound ? (byte)1 : (byte)0));
        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId, durability: 321)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 50);

        var socketProgress = ComposeSocketProgress(progressGreen, progressBlue, progressRed);
        var steed = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == GrantedSteedItemId);
        Assert.Equal(51U, steed.Uid);
        Assert.Equal(321, steed.Durability);
        Assert.Equal(321, steed.MaximumDurability);
        Assert.Equal(0, steed.Position);
        Assert.Equal(socketProgress, steed.SocketProgress);
        Assert.Equal(0, steed.SocketOne);
        Assert.Equal(0, steed.SocketTwo);
        Assert.Equal(plus, steed.Plus);
        Assert.Equal(progressRed, steed.Bless);
        Assert.Equal(bound ? 1 : 0, steed.Bound);
        Assert.Equal(progressBlue, steed.Enchant);
        Assert.Equal(1, steed.StackSize);
        Assert.InRange(steed.Color, 3U, 8U);

        var removeIndex = bound ? 1 : 0;
        var addIndex = bound ? 2 : 1;
        Assert.Equal(bound ? 3 : 2, result.Sent.Count);
        if (bound)
        {
            AssertBoundPackTalk(result.Sent[0], plus, optionId);
        }

        AssertPackRemoval(result.Sent[removeIndex], uid: 50);
        var packet = AssertGameItem(result.Sent[addIndex]);
        Assert.Equal(51U, packet.Uid);
        Assert.Equal(GrantedSteedItemId, packet.ItemId);
        Assert.Equal(321, packet.Durability);
        Assert.Equal(321, packet.MaximumDurability);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
        Assert.Equal(0, packet.Position);
        Assert.Equal(socketProgress, packet.SocketProgress);
        Assert.Equal(0, packet.SocketOne);
        Assert.Equal(0, packet.SocketTwo);
        Assert.Equal(0U, packet.SpellId);
        Assert.Equal(0, packet.Effect);
        Assert.Equal(plus, packet.Plus);
        Assert.Equal(progressRed, packet.Bless);
        Assert.Equal(bound ? 1 : 0, packet.Bound);
        Assert.Equal(progressBlue, packet.Enchant);
        Assert.Equal(progressGreen, packet.ProgressGreen);
        Assert.Equal(0U, packet.PlusProgress);
        Assert.Equal(0U, packet.RemainingTime);
        Assert.Equal(0U, packet.TimeLeftInMinutes);
        Assert.Equal(1, packet.StackSize);
        Assert.DoesNotContain(result.Sent, IsNpcReply);

        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains("source=" + SourceForNpc(npcId), result.Log);
        Assert.Contains($"sourceOrder={(bound ? "talk+remove+add-steed" : "remove+add-steed")}", result.Log);
    }

    public static TheoryData<uint, uint, byte, byte, byte> BindingRemovalQuirkCases => new()
    {
        { Steed1PackNpcId, Steed1PackItemId, 2, 0, 2 },
        { Steed1NpcId, Steed1ItemId, 1, 1, 0 },
        { RareSteedPack6NpcId, RareSteedPack6ItemId, 1, 1, 0 },
    };

    [Theory]
    [MemberData(nameof(BindingRemovalQuirkCases))]
    public async Task BindingGateThenBoundAgnosticRemoveConsumesFirstMatchingPack(
        uint npcId,
        uint packItemId,
        byte optionId,
        byte firstItemBound,
        byte gateItemBound)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, packItemId, bound: firstItemBound),
            CreateInventoryItem(20, packItemId, bound: gateItemBound));
        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10);
        var preservedGateItem = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 20);
        Assert.Equal(gateItemBound, preservedGateItem.Bound);
        AssertPackRemoval(result.Sent[firstItemBound == 0 && gateItemBound > 0 ? 1 : 0], uid: 10);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
    }

    public static TheoryData<uint, uint, byte, byte, string> MissingPackCases => new()
    {
        { Steed3PackNpcId, Steed3PackItemId, 2, 0, "Sorry, you don`t have a Steed+1Pack." },
        { Steed6NpcId, Steed6ItemId, 1, 1, "Sorry, you don`t have a Steed+1." },
        { NobleSteedPackNpcId, NobleSteedPackItemId, 1, 1, "Sorry, you don`t have a NobleSteedPack" },
    };

    [Theory]
    [MemberData(nameof(MissingPackCases))]
    public async Task MissingRequiredBindingClassUsesExactSourceMessageBoxWithoutMutation(
        uint npcId,
        uint packItemId,
        byte optionId,
        byte heldBound,
        string expectedText)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(10, packItemId, bound: heldBound));
        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        AssertMessageBox(result.Sent, expectedText);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task PersistencePrecedesLiveCommitAndMutationPackets()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, Steed3PackItemId, bound: 1));
        var sessionCommitObserved = false;
        var firstPacketObserved = false;

        var result = await InvokeAsync(
            Steed3PackNpcId,
            optionId: 2,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId, durability: 123)),
            playerItemsRoot,
            sessionChanged: (_, previous, current) =>
            {
                if (previous is null)
                {
                    return;
                }

                var persisted = new PlayerItemSnapshotLoader(playerItemsRoot)
                    .LoadByCharacterUid(CharacterUid);
                AssertPlayerItemsEqual(current.PlayerItems, persisted);
                Assert.DoesNotContain(current.PlayerItems.InventoryItems, item => item.ItemId == Steed3PackItemId);
                Assert.Contains(current.PlayerItems.InventoryItems, item => item.ItemId == GrantedSteedItemId);
                sessionCommitObserved = true;
            },
            frameSent: (sessions, connection, _) =>
            {
                if (firstPacketObserved)
                {
                    return;
                }

                Assert.True(sessionCommitObserved);
                Assert.True(sessions.TryGet(connection, out var current));
                Assert.NotNull(current);
                AssertPersistedEquals(playerItemsRoot, current.PlayerItems);
                firstPacketObserved = true;
            });

        Assert.True(result.Handled);
        Assert.True(sessionCommitObserved);
        Assert.True(firstPacketObserved);
        Assert.Equal(3, result.Sent.Count);
    }

    [Fact]
    public async Task MissingSessionStoreDoesNotPersistCommitOrSendMutationPackets()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, Steed1ItemId));
        var result = await InvokeAsync(
            Steed1NpcId,
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)),
            playerItemsRoot,
            includeSessionStore: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("session-store-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingPlayerItemsRootDoesNotCommitOrSendMutationPackets()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, Steed1ItemId));
        var result = await InvokeAsync(
            Steed1NpcId,
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)),
            playerItemsRoot: null,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("player-items-root-missing", result.Log);
    }

    [Fact]
    public async Task MissingAuthoritativeSteedItemDataDoesNotConsumePack()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, Steed1ItemId));
        var result = await InvokeAsync(
            Steed1NpcId,
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-missing:itemId=300000", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingWorldDataDoesNotConsumePack()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, Steed1ItemId));
        var result = await InvokeAsync(
            Steed1NpcId,
            optionId: 1,
            originalItems,
            worldData: null,
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-missing:itemId=300000", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task FullInventoryWithSinglePackSucceedsBecauseRemovalFreesGrantSlot()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(50, Steed1ItemId),
        };
        inventory.AddRange(
            Enumerable.Range(0, 39)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var result = await InvokeAsync(
            Steed1NpcId,
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.False(ReferenceEquals(result.OriginalSession, result.UpdatedSession), result.Log);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 50);
        var steed = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == GrantedSteedItemId);
        Assert.Equal(139U, steed.Uid);
        Assert.Equal(1, steed.Plus);
        Assert.Equal(0, steed.Bound);
        Assert.Equal(ComposeSocketProgress(byte.MaxValue, 0, 150), steed.SocketProgress);
        Assert.Equal(2, result.Sent.Count);
        AssertPackRemoval(result.Sent[0], uid: 50);
        Assert.Equal(GrantedSteedItemId, AssertGameItem(result.Sent[1]).ItemId);
        Assert.DoesNotContain("inventory-full", result.Log);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Fact]
    public async Task FullInventoryWithStackedPackStaysMutationFreeWhenRemovalDoesNotFreeSlot()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(50, Steed1ItemId, stackSize: 2),
        };
        inventory.AddRange(
            Enumerable.Range(0, 39)
                .Select(index => CreateInventoryItem(
                    checked((uint)(100 + index)),
                    checked((uint)(410_000 + index)))));
        var originalItems = CreatePlayerItems(inventory.ToArray());
        var result = await InvokeAsync(
            Steed1NpcId,
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Equal(2, Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 50).StackSize);
        AssertMessageBox(result.Sent, "Please make one more space in your inventory.");
        Assert.True(
            result.Log.Contains("inventory-full-after-planned-pack-removal", StringComparison.Ordinal),
            result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task PersistenceWriteFailureDoesNotCommitOrSendMutationPackets()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, Steed1ItemId));
        var result = await InvokeAsync(
            Steed1NpcId,
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)),
            blockedPlayerItemsRoot,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=persistence-", result.Log);
        Assert.Contains("no live session mutation or item packet was committed", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
    }

    [Fact]
    public async Task ExhaustedItemUidDoesNotConsumePackOrSendMutationPackets()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(uint.MaxValue, Steed1ItemId));
        var result = await InvokeAsync(
            Steed1NpcId,
            optionId: 1,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(GrantedSteedItemId)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("item-uid-exhausted", result.Log);
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
        uint npcId,
        byte optionId,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        string? playerItemsRoot,
        bool seedPersistence = true,
        bool includeSessionStore = true,
        Action<ConquerTcpConnection, WorldSession?, WorldSession>? sessionChanged = null,
        Action<WorldSessionStore, ConquerTcpConnection, byte[]>? frameSent = null)
    {
        if (seedPersistence)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot!).SaveByCharacterUid(CharacterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        WorldSessionStore? sessions = null;
        ConquerTcpConnection? connection = null;
        connection = CreateSendingConnection(
            sent,
            bytes => frameSent?.Invoke(sessions!, connection!, bytes));
        sessions = new WorldSessionStore(sessionChanged);
        var session = CreateSession(originalItems);
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
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
        Assert.NotNull(updatedSession);
        return new InvokeResult(
            handled,
            sent,
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
            Username: "legacy-steed-pack-mutation-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "SteedTester",
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

    private static ConquerTcpConnection CreateSendingConnection(
        List<byte[]> sent,
        Action<byte[]>? frameSent = null)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            var bytes = frame.ToArray();
            sent.Add(bytes);
            frameSent?.Invoke(bytes);
            return ValueTask.CompletedTask;
        });
    }

    private static void AssertMutationFree(InvokeResult result, PlayerItemSnapshot originalItems)
    {
        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        AssertPlayerItemsEqual(originalItems, result.UpdatedSession.PlayerItems);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == GrantedSteedItemId);
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

    private static void AssertBoundPackTalk(byte[] bytes, byte plus, byte optionId)
    {
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet));
        var colorName = optionId switch
        {
            2 => "White",
            6 => "Brown",
            11 => "Black",
            _ => throw new ArgumentOutOfRangeException(nameof(optionId)),
        };
        Assert.Equal(LegacyMsgMessagePacket.RedColor, packet.Color);
        Assert.Equal(LegacyMsgMessagePacket.TalkChatType, packet.ChatType);
        Assert.Equal("SYSTEM", packet.Content[0]);
        Assert.Equal("ALL", packet.Content[1]);
        Assert.Equal($"You received +{plus} {colorName} Steed (B)!", packet.Content[3]);
    }

    private static void AssertPackRemoval(byte[] bytes, uint uid)
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

    private static void AssertMessageBox(IReadOnlyList<byte[]> sent, string expectedText)
    {
        var bytes = Assert.Single(sent);
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(LegacyNpcReplyInteractType.MessageBox, packet.InteractType);
        Assert.Equal(expectedText, packet.Text);
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

    private static uint ComposeSocketProgress(byte green, byte blue, byte red)
    {
        return checked((uint)(green | (blue << 8) | (red << 16)));
    }

    private static string SourceForNpc(uint npcId)
    {
        return npcId switch
        {
            Steed3PackNpcId => "NpcHandler.Steed3Pack",
            Steed1PackNpcId => "NpcHandler.Steed1Pack",
            Steed1NpcId => "NpcHandler.Steed1",
            Steed3NpcId => "NpcHandler.Steed3",
            Steed6NpcId => "NpcHandler.Steed6",
            RareSteedPack6NpcId => "NpcHandler.RareSteedPack6",
            NobleSteedPackNpcId => "NpcHandler.NobleSteedPack",
            _ => throw new ArgumentOutOfRangeException(nameof(npcId)),
        };
    }

    private static WorldDataCatalog CreateWorldDataCatalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-11T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-steed-pack-mutation",
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

    private static ItemType CreateItemType(uint itemTypeId, uint durability = 1)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "Steed";
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), "Steed", fields);
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
