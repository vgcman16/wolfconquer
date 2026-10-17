using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacySacredRefineryPackMutationTests : IDisposable
{
    private const uint NpcId = 4_294_967_292;
    private const uint PackItemId = 3_004_249;
    private const long CharacterUid = 1_293_600;
    private const string SuccessText = "You received 1 Sacred Refinery Material.";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacySacredRefineryPackMutationTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<byte, uint> RewardCases => new()
    {
        { 15, 3_004_160 },
        { 16, 3_004_159 },
        { 17, 3_004_158 },
        { 18, 3_004_157 },
        { 19, 3_004_156 },
        { 20, 3_004_155 },
        { 22, 3_004_154 },
        { 23, 3_004_153 },
        { 24, 3_004_152 },
        { 25, 3_004_151 },
        { 26, 3_004_150 },
        { 27, 3_004_149 },
        { 29, 3_004_148 },
        { 30, 3_004_147 },
        { 32, 3_004_166 },
        { 33, 3_004_165 },
        { 34, 3_004_164 },
        { 36, 3_004_163 },
        { 37, 3_004_162 },
        { 41, 3_004_146 },
        { 42, 3_004_145 },
        { 43, 3_004_144 },
        { 44, 3_004_143 },
        { 47, 3_004_142 },
        { 48, 3_004_141 },
        { 49, 3_004_140 },
        { 83, 3_004_139 },
        { 84, 3_004_138 },
        { 85, 3_004_137 },
        { 87, 3_004_136 },
        { 88, 3_004_138 },
        { 89, 3_004_162 },
        { 90, 3_004_154 },
        { 91, 3_004_145 },
        { 92, 3_004_165 },
        { 94, 3_004_152 },
        { 95, 3_004_161 },
        { 96, 3_004_143 },
        { 97, 3_004_153 },
        { 101, 3_004_150 },
        { 102, 3_004_159 },
        { 104, 3_004_141 },
        { 105, 3_004_156 },
        { 106, 3_004_137 },
        { 107, 3_004_147 },
    };

    public static TheoryData<byte, byte[]> PageCases => new()
    {
        { 0, [32, 33, 34, 36, 37, 38, 39] },
        { 31, [32, 33, 34, 36, 37, 38, 39] },
        { 39, [15, 16, 17, 18, 19, 20, 21] },
        { 21, [22, 23, 24, 25, 26, 27, 28] },
        { 28, [29, 30, 41, 42, 43, 44, 45] },
        { 45, [29, 30, 41, 42, 43, 44, 46] },
        { 46, [47, 48, 49, 83, 84, 85, 86] },
        { 86, [87, 88, 89, 90, 91, 92, 93] },
        { 93, [94, 95, 96, 97, 101, 102, 103] },
        { 103, [104, 105, 106, 107, 31] },
    };

    [Theory]
    [MemberData(nameof(RewardCases))]
    public async Task EveryDecompiledRewardSelectionPersistsExactItemAndPacketOrder(
        byte optionId,
        uint rewardItemId)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, 720_001),
            CreateInventoryItem(50, PackItemId));
        var result = await InvokeAsync(
            optionId,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(rewardItemId, durability: 321)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == PackItemId);
        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(51U, reward.Uid);
        Assert.Equal(321, reward.Durability);
        Assert.Equal(321, reward.MaximumDurability);
        Assert.Equal(0, reward.Position);
        Assert.Equal(0, reward.Plus);
        Assert.Equal(0, reward.Bless);
        Assert.Equal(0, reward.Bound);
        Assert.Equal(0, reward.Enchant);
        Assert.Equal(1, reward.StackSize);
        Assert.InRange(reward.Color, 3U, 8U);

        Assert.Equal(3, result.Sent.Count);
        var addIndex = optionId == 19 ? 0 : 2;
        var removeIndex = optionId == 19 ? 1 : 0;
        var messageIndex = optionId == 19 ? 2 : 1;
        AssertPackRemoval(result.Sent[removeIndex], 50);
        Assert.Equal(SuccessText, AssertNpcReply(result.Sent[messageIndex]).Text);
        var packet = AssertGameItem(result.Sent[addIndex]);
        Assert.Equal(51U, packet.Uid);
        Assert.Equal(rewardItemId, packet.ItemId);
        Assert.Equal(321, packet.Durability);
        Assert.Equal(321, packet.MaximumDurability);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
        Assert.Equal(0, packet.Bound);
        Assert.Equal(1, packet.StackSize);

        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains($"optionId={optionId}", result.Log);
        Assert.Contains($"rewardItemId={rewardItemId}", result.Log);
        Assert.Contains(
            optionId == 19
                ? "sourceOrder=Add,Remove,MessageBox"
                : "sourceOrder=Remove,MessageBox,Add",
            result.Log);
        Assert.DoesNotContain("not available yet", result.Log, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(PageCases))]
    public async Task EveryDecompiledPagePreservesExactTextOptionIdsAvatarAndFinish(
        byte optionId,
        byte[] expectedOptionIds)
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, PackItemId));
        var result = await InvokeAsync(
            optionId,
            originalItems,
            worldData: null,
            playerItemsRoot: null,
            seedPersistence: false,
            includeSessionStore: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        var replies = result.Sent.Select(AssertNpcReply).ToArray();
        Assert.Equal("Choose what Refineries with level 6 you need .", replies[0].Text);
        Assert.Equal(LegacyNpcReplyInteractType.Dialog, replies[0].InteractType);
        Assert.Equal(
            expectedOptionIds,
            replies
                .Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option)
                .Select(reply => reply.OptionId)
                .ToArray());
        var avatar = Assert.Single(replies, reply => reply.InteractType == LegacyNpcReplyInteractType.Avatar);
        Assert.Equal(200, avatar.InputMaxLength);
        Assert.Equal(LegacyNpcReplyInteractType.Finish, replies[^1].InteractType);
        Assert.Contains("source=NpcHandler.SelectSacredMaterial", result.Log);
    }

    [Fact]
    public async Task RootPageUsesExactDecompilerOptionLabels()
    {
        var result = await InvokeAsync(
            optionId: 0,
            CreatePlayerItems(CreateInventoryItem(50, PackItemId)),
            worldData: null,
            playerItemsRoot: null,
            seedPersistence: false,
            includeSessionStore: false);

        var optionTexts = result.Sent
            .Select(AssertNpcReply)
            .Where(reply => reply.InteractType == LegacyNpcReplyInteractType.Option)
            .Select(reply => reply.Text)
            .ToArray();
        Assert.Equal(
            [
                "Detoxication[Bag]. ",
                "Detoxication[Necklace]. ",
                "Breakthrough[Bracelet]. ",
                "Breakthrough[Ring]. ",
                "Penetration[Bracelet]. ",
                "Detoxication[Armor]. ",
                "Next Page.",
            ],
            optionTexts);
    }

    [Fact]
    public async Task BoundOnlyPackFailsTheUnboundGateWithExactSourceMessage()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, PackItemId, bound: 1));
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            worldData: null,
            playerItemsRoot: null,
            seedPersistence: false,
            includeSessionStore: false);

        AssertMutationFree(result, originalItems);
        Assert.Equal("Sorry, you don`t have a SacredRefineryPack.", Assert.Single(
            result.Sent.Select(AssertNpcReply)).Text);
    }

    [Fact]
    public async Task UnboundGateThenBoundAgnosticRemovalConsumesFirstBoundPack()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, PackItemId, bound: 1),
            CreateInventoryItem(20, PackItemId));
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10);
        var unboundGateItem = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 20);
        Assert.Equal(0, unboundGateItem.Bound);
        AssertPackRemoval(result.Sent[0], 10);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Fact]
    public async Task UnboundGateThenRemovalConsumesFirstZeroStackMatchingRecord()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, PackItemId, stackSize: 0, bound: 1),
            CreateInventoryItem(20, PackItemId));
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160, durability: 123)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 10);
        var unboundGateItem = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 20);
        Assert.Equal(1, unboundGateItem.StackSize);
        Assert.Equal(0, unboundGateItem.Bound);

        var reward = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == 3_004_160);
        Assert.Equal(21U, reward.Uid);
        Assert.Equal(123, reward.Durability);
        Assert.Equal(0, reward.Bound);
        Assert.InRange(reward.Color, 3U, 8U);

        Assert.Equal(3, result.Sent.Count);
        AssertPackRemoval(result.Sent[0], 10);
        Assert.Equal(SuccessText, AssertNpcReply(result.Sent[1]).Text);
        Assert.Equal(3_004_160U, AssertGameItem(result.Sent[2]).ItemId);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Fact]
    public async Task StackedBoundFirstPackEmitsUpdateAfterUnboundEligibilityGate()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(
            CreateInventoryItem(10, PackItemId, stackSize: 2, bound: 1),
            CreateInventoryItem(20, PackItemId));
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160)),
            playerItemsRoot);

        var updatedBoundPack = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 10);
        Assert.Equal(1, updatedBoundPack.StackSize);
        Assert.Equal(1, updatedBoundPack.Bound);
        var updatePacket = AssertGameItem(result.Sent[0]);
        Assert.Equal(10U, updatePacket.Uid);
        Assert.Equal(PackItemId, updatePacket.ItemId);
        Assert.Equal(1, updatePacket.StackSize);
        Assert.Equal(1, updatePacket.Bound);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, updatePacket.Mode);
    }

    [Fact]
    public async Task RemoveFirstBranchCanUseSlotFreedBySinglePack()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreateFullInventory(packStackSize: 1);
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == PackItemId);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 3_004_160);
        Assert.Equal(3, result.Sent.Count);
        AssertPackRemoval(result.Sent[0], 50);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Fact]
    public async Task RemoveFirstBranchWithStackedPackAndFullInventoryIsMutationFree()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreateFullInventory(packStackSize: 2);
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("inventory-full-after-planned-pack-removal", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task AddFirstOption19RequiresCapacityBeforePackRemoval()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreateFullInventory(packStackSize: 1);
        var result = await InvokeAsync(
            optionId: 19,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_156)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("inventory-full-before-source-add", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task PersistencePrecedesSessionCommitAndFirstMutationPacket()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, PackItemId));
        var commitObserved = false;
        var firstPacketObserved = false;

        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160)),
            playerItemsRoot,
            sessionChanged: (_, previous, current) =>
            {
                if (previous is null)
                {
                    return;
                }

                AssertPersistedEquals(playerItemsRoot, current.PlayerItems);
                Assert.DoesNotContain(current.PlayerItems.InventoryItems, item => item.ItemId == PackItemId);
                Assert.Contains(current.PlayerItems.InventoryItems, item => item.ItemId == 3_004_160);
                commitObserved = true;
            },
            frameSent: (sessions, connection, _) =>
            {
                if (firstPacketObserved)
                {
                    return;
                }

                Assert.True(commitObserved);
                Assert.True(sessions.TryGet(connection, out var current));
                Assert.NotNull(current);
                AssertPersistedEquals(playerItemsRoot, current.PlayerItems);
                firstPacketObserved = true;
            });

        Assert.True(result.Handled);
        Assert.True(commitObserved);
        Assert.True(firstPacketObserved);
    }

    [Theory]
    [InlineData(false, true, true, "session-store-missing")]
    [InlineData(true, false, true, "session-registration-missing")]
    [InlineData(true, true, false, "player-items-root-missing")]
    public async Task MissingPersistenceInfrastructureIsMutationAndPacketFree(
        bool includeSessionStore,
        bool registerSession,
        bool includePlayerItemsRoot,
        string expectedReason)
    {
        var playerItemsRoot = includePlayerItemsRoot ? CreatePlayerItemsRoot() : null;
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, PackItemId));
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160)),
            playerItemsRoot,
            seedPersistence: includePlayerItemsRoot,
            includeSessionStore: includeSessionStore,
            registerSession: registerSession);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains(expectedReason, result.Log);
        if (playerItemsRoot is not null)
        {
            AssertPersistedEquals(playerItemsRoot, originalItems);
        }
    }

    [Fact]
    public async Task MissingCharacterUidIsMutationAndPacketFree()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, PackItemId));
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160)),
            CreatePlayerItemsRoot(),
            seedPersistence: false,
            characterUid: 0);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("character-uid-unavailable", result.Log);
    }

    [Theory]
    [InlineData(0, "reward-itemtype-missing")]
    [InlineData(1, "reward-itemtype-missing")]
    [InlineData(2, "reward-itemtype-invalid")]
    public async Task MissingOrInvalidAuthoritativeRewardDataDoesNotConsumePack(
        int dataMode,
        string expectedReason)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, PackItemId));
        var worldData = dataMode switch
        {
            0 => null,
            1 => CreateWorldDataCatalog(),
            _ => CreateWorldDataCatalog(CreateItemType(3_004_160, durability: 65_536)),
        };
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            worldData,
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains(expectedReason, result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task ExhaustedItemUidDoesNotConsumePackOrSendPackets()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = CreatePlayerItems(CreateInventoryItem(uint.MaxValue, PackItemId));
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("item-uid-exhausted", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task PersistenceFailureDoesNotCommitOrSendPackets()
    {
        Directory.CreateDirectory(_root);
        var blockedRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedRoot, "not a directory");
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, PackItemId));
        var result = await InvokeAsync(
            optionId: 15,
            originalItems,
            CreateWorldDataCatalog(CreateItemType(3_004_160)),
            blockedRoot,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("persistence-", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedRoot));
    }

    [Fact]
    public async Task SourceEmptyOption38RemainsARealNoOp()
    {
        var originalItems = CreatePlayerItems(CreateInventoryItem(50, PackItemId));
        var result = await InvokeAsync(
            optionId: 38,
            originalItems,
            worldData: null,
            playerItemsRoot: null,
            seedPersistence: false,
            includeSessionStore: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("ItemPackServices no-op", result.Log);
        Assert.DoesNotContain("not available yet", result.Log, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        byte optionId,
        PlayerItemSnapshot originalItems,
        WorldDataCatalog? worldData,
        string? playerItemsRoot,
        bool seedPersistence = true,
        bool includeSessionStore = true,
        bool registerSession = true,
        long characterUid = CharacterUid,
        Action<ConquerTcpConnection, WorldSession?, WorldSession>? sessionChanged = null,
        Action<WorldSessionStore, ConquerTcpConnection, byte[]>? frameSent = null)
    {
        if (seedPersistence)
        {
            Assert.False(string.IsNullOrWhiteSpace(playerItemsRoot));
            new PlayerItemSnapshotLoader(playerItemsRoot!).SaveByCharacterUid(characterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        WorldSessionStore? sessions = includeSessionStore ? new WorldSessionStore(sessionChanged) : null;
        ConquerTcpConnection? connection = null;
        connection = CreateSendingConnection(
            sent,
            bytes =>
            {
                if (sessions is not null)
                {
                    frameSent?.Invoke(sessions, connection!, bytes);
                }
            });
        var session = CreateSession(originalItems, characterUid);
        if (registerSession)
        {
            sessions?.Set(connection, session);
        }

        var handled = await WorldPacketHandlers.TryHandleLegacyItemPackServicesNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerItemsRoot: playerItemsRoot),
            session,
            NpcId,
            optionId,
            CancellationToken.None);

        var updatedSession = session;
        if (sessions?.TryGet(connection, out var stored) == true)
        {
            Assert.NotNull(stored);
            updatedSession = stored;
        }

        return new InvokeResult(handled, sent, log.ToString(), session, updatedSession);
    }

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private static PlayerItemSnapshot CreateFullInventory(ushort packStackSize)
    {
        var items = new List<PlayerItemSnapshotItem>
        {
            CreateInventoryItem(50, PackItemId, packStackSize),
        };
        items.AddRange(
            Enumerable.Range(0, 39).Select(index => CreateInventoryItem(
                checked((uint)(100 + index)),
                checked((uint)(410_000 + index)))));
        return CreatePlayerItems(items.ToArray());
    }

    private static WorldSession CreateSession(PlayerItemSnapshot playerItems, long characterUid)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "legacy-sacred-refinery-pack-test",
            Character: new CharacterSnapshot(
                Uid: characterUid,
                Name: "SacredTester",
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

    private static void AssertPackRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
    }

    private static LegacyGameItemPacket AssertGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }

    private static LegacyNpcReplyPacket AssertNpcReply(byte[] bytes)
    {
        Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
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
                "fixture://legacy-sacred-refinery-pack-mutation",
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
        fields[1] = "SacredRefineryMaterial";
        fields[ItemType.DurabilityFieldIndex] = durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), "SacredRefineryMaterial", fields);
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
