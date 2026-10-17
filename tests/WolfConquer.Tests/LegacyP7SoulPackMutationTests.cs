using System.Buffers;
using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyP7SoulPackMutationTests : IDisposable
{
    private const uint EquipmentNpcId = 4_294_967_293;
    private const uint WeaponNpcId = 4_294_967_294;
    private const uint EquipmentPackItemId = 3_004_248;
    private const uint WeaponPackItemId = 3_004_247;
    private const long CharacterUid = 1_293_600;
    private const string MissingPackText = "Sorry, you don`t have a P7WeaponSoulPack.";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyP7SoulPackMutationTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<uint, uint, byte, uint> SelectionCases => new()
    {
        { EquipmentNpcId, EquipmentPackItemId, 1, 820_073 },
        { EquipmentNpcId, EquipmentPackItemId, 2, 820_074 },
        { EquipmentNpcId, EquipmentPackItemId, 3, 820_075 },
        { EquipmentNpcId, EquipmentPackItemId, 4, 820_076 },
        { EquipmentNpcId, EquipmentPackItemId, 5, 821_033 },
        { EquipmentNpcId, EquipmentPackItemId, 6, 821_034 },
        { EquipmentNpcId, EquipmentPackItemId, 7, 822_071 },
        { EquipmentNpcId, EquipmentPackItemId, 9, 822_072 },
        { EquipmentNpcId, EquipmentPackItemId, 10, 823_058 },
        { EquipmentNpcId, EquipmentPackItemId, 11, 823_059 },
        { EquipmentNpcId, EquipmentPackItemId, 12, 823_060 },
        { EquipmentNpcId, EquipmentPackItemId, 13, 823_061 },
        { EquipmentNpcId, EquipmentPackItemId, 14, 823_062 },
        { EquipmentNpcId, EquipmentPackItemId, 15, 824_018 },
        { EquipmentNpcId, EquipmentPackItemId, 16, 824_019 },
        { EquipmentNpcId, EquipmentPackItemId, 17, 824_020 },

        { WeaponNpcId, WeaponPackItemId, 1, 800_020 },
        { WeaponNpcId, WeaponPackItemId, 2, 800_111 },
        { WeaponNpcId, WeaponPackItemId, 3, 800_142 },
        { WeaponNpcId, WeaponPackItemId, 4, 800_215 },
        { WeaponNpcId, WeaponPackItemId, 5, 800_255 },
        { WeaponNpcId, WeaponPackItemId, 6, 800_422 },
        { WeaponNpcId, WeaponPackItemId, 7, 800_522 },
        { WeaponNpcId, WeaponPackItemId, 9, 800_618 },
        { WeaponNpcId, WeaponPackItemId, 10, 800_725 },
        { WeaponNpcId, WeaponPackItemId, 11, 800_810 },
        { WeaponNpcId, WeaponPackItemId, 12, 800_811 },
        { WeaponNpcId, WeaponPackItemId, 13, 800_917 },
        { WeaponNpcId, WeaponPackItemId, 14, 801_004 },
        { WeaponNpcId, WeaponPackItemId, 15, 801_104 },
        { WeaponNpcId, WeaponPackItemId, 16, 801_216 },
        { WeaponNpcId, WeaponPackItemId, 17, 801_218 },
        { WeaponNpcId, WeaponPackItemId, 18, 801_308 },
        { WeaponNpcId, WeaponPackItemId, 25, 827_010 },
        { WeaponNpcId, WeaponPackItemId, 26, 827_011 },
        { WeaponNpcId, WeaponPackItemId, 27, 827_012 },
        { WeaponNpcId, WeaponPackItemId, 28, 827_013 },
    };

    public static TheoryData<uint, uint, byte, string> PageTextCases => new()
    {
        { EquipmentNpcId, EquipmentPackItemId, 0, "Select the p7 weapon you need." },
        { EquipmentNpcId, EquipmentPackItemId, 40, "Select the p7 weapon you need." },
        { EquipmentNpcId, EquipmentPackItemId, 8, "Select the p7 weapon you need." },
        { WeaponNpcId, WeaponPackItemId, 0, "Select the p7 weapon you need." },
        { WeaponNpcId, WeaponPackItemId, 20, "Select the p7 weapon you need." },
        { WeaponNpcId, WeaponPackItemId, 8, "Selecte the p7 weapon you need." },
        { WeaponNpcId, WeaponPackItemId, 21, "Selecte the p7 weapon you need." },
    };

    [Theory]
    [MemberData(nameof(PageTextCases))]
    public async Task EveryPageUsesExactDecompiledDialogText(
        uint npcId,
        uint packItemId,
        byte optionId,
        string expectedText)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(Item(50, packItemId));
        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            Catalog(),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        var replies = result.Sent.Select(frame =>
        {
            Assert.True(LegacyNpcReplyPacket.TryParse(ReadFrame(frame), out var packet));
            return packet;
        });
        var dialog = Assert.Single(
            replies,
            reply => reply.InteractType == LegacyNpcReplyInteractType.Dialog);
        Assert.Equal(expectedText, dialog.Text);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Theory]
    [MemberData(nameof(SelectionCases))]
    public async Task EverySourceSelectionPersistsExactOrdinarySoulAndRemoveAddOrder(
        uint npcId,
        uint packItemId,
        byte optionId,
        uint rewardItemId)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(
            Item(10, 710_001),
            Item(50, packItemId));
        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            Catalog(ItemType(rewardItemId, durability: 321)),
            playerItemsRoot);

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 50);

        var soul = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.ItemId == rewardItemId);
        Assert.Equal(51U, soul.Uid);
        Assert.Equal((ushort)321, soul.Durability);
        Assert.Equal((ushort)321, soul.MaximumDurability);
        Assert.Equal((ushort)0, soul.Position);
        Assert.Equal(0U, soul.SocketProgress);
        Assert.Equal((byte)0, soul.SocketOne);
        Assert.Equal((byte)0, soul.SocketTwo);
        Assert.Equal(0U, soul.Effect);
        Assert.Equal((byte)0, soul.Plus);
        Assert.Equal((byte)0, soul.Bless);
        Assert.Equal((byte)0, soul.Bound);
        Assert.Equal((byte)0, soul.Enchant);
        Assert.Equal((ushort)1, soul.StackSize);
        Assert.InRange(soul.Color, 3U, 8U);

        Assert.Collection(
            result.Sent,
            frame => AssertItemRemoval(frame, 50),
            frame => AssertSoulGrant(frame, 51, rewardItemId, durability: 321));
        Assert.DoesNotContain(result.Sent, IsNpcReply);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
        Assert.Contains("source=" + SourceForNpc(npcId), result.Log);
        Assert.Contains("sourceOrder=Remove,Add", result.Log);
        Assert.Contains("bound=false plus=0 socketOne=0 socketTwo=0", result.Log);
    }

    [Theory]
    [InlineData(EquipmentNpcId, EquipmentPackItemId, (byte)1, 820_073U)]
    [InlineData(WeaponNpcId, WeaponPackItemId, (byte)1, 800_020U)]
    public async Task UnboundGateThenBoundAgnosticRemoveConsumesFirstBoundStack(
        uint npcId,
        uint packItemId,
        byte optionId,
        uint rewardItemId)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(
            Item(10, packItemId, stackSize: 2, bound: 1),
            Item(20, packItemId));
        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            Catalog(ItemType(rewardItemId)),
            playerItemsRoot);

        var boundPack = Assert.Single(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 10);
        Assert.Equal((ushort)1, boundPack.StackSize);
        Assert.Equal((byte)1, boundPack.Bound);
        Assert.Contains(
            result.UpdatedSession.PlayerItems.InventoryItems,
            item => item.Uid == 20 && item.Bound == 0 && item.StackSize == 1);
        AssertItemUpdate(result.Sent[0], uid: 10, packItemId, stackSize: 1, bound: 1);
        AssertSoulGrant(result.Sent[1], uid: 21, rewardItemId, durability: 1);
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Theory]
    [InlineData(EquipmentNpcId, EquipmentPackItemId, (byte)1, 820_073U)]
    [InlineData(WeaponNpcId, WeaponPackItemId, (byte)1, 800_020U)]
    public async Task BoundOnlyPackFailsExactUnboundGateWithoutMutation(
        uint npcId,
        uint packItemId,
        byte optionId,
        uint rewardItemId)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(Item(10, packItemId, bound: 1));
        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            Catalog(ItemType(rewardItemId)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        AssertMessageBox(result.Sent, MissingPackText);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task FullInventoryWithSinglePackSucceedsBecauseRemovalFreesOneSlot()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem> { Item(50, EquipmentPackItemId) };
        inventory.AddRange(Enumerable.Range(0, 39)
            .Select(index => Item(
                checked((uint)(100 + index)),
                checked((uint)(410_000 + index)))));
        var originalItems = Items(inventory.ToArray());
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            Catalog(ItemType(820_073)),
            playerItemsRoot);

        Assert.Equal(40, result.UpdatedSession.PlayerItems.InventoryItems.Count);
        Assert.DoesNotContain(result.UpdatedSession.PlayerItems.InventoryItems, item => item.Uid == 50);
        Assert.Contains(result.UpdatedSession.PlayerItems.InventoryItems, item => item.ItemId == 820_073);
        Assert.Collection(
            result.Sent,
            frame => AssertItemRemoval(frame, 50),
            frame => AssertSoulGrant(frame, 139, 820_073, durability: 1));
        AssertPersistedEquals(playerItemsRoot, result.UpdatedSession.PlayerItems);
    }

    [Fact]
    public async Task FullInventoryWithStackedPackIsMutationFreeWhenRemovalWouldNotFreeSlot()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var inventory = new List<PlayerItemSnapshotItem>
        {
            Item(50, EquipmentPackItemId, stackSize: 2),
        };
        inventory.AddRange(Enumerable.Range(0, 39)
            .Select(index => Item(
                checked((uint)(100 + index)),
                checked((uint)(410_000 + index)))));
        var originalItems = Items(inventory.ToArray());
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            Catalog(ItemType(820_073)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("inventory-full-after-planned-pack-removal", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Theory]
    [InlineData(EquipmentNpcId, EquipmentPackItemId, (byte)1)]
    [InlineData(WeaponNpcId, WeaponPackItemId, (byte)1)]
    public async Task MissingAuthoritativeRewardDataDoesNotConsumePack(
        uint npcId,
        uint packItemId,
        byte optionId)
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(Item(50, packItemId));
        var result = await InvokeAsync(
            npcId,
            optionId,
            originalItems,
            Catalog(),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingWorldDataDoesNotConsumePack()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(Item(50, EquipmentPackItemId));
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            worldData: null,
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task InvalidRewardDurabilityDoesNotConsumePack()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(Item(50, EquipmentPackItemId));
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            Catalog(ItemType(820_073, durability: 70_000)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reward-itemtype-invalid", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingSessionStoreDoesNotPersistCommitOrSendMutationPackets()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(Item(50, EquipmentPackItemId));
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            Catalog(ItemType(820_073)),
            playerItemsRoot,
            includeSessionStore: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("session-store-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingSessionRegistrationDoesNotPersistCommitOrSendMutationPackets()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(Item(50, EquipmentPackItemId));
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            Catalog(ItemType(820_073)),
            playerItemsRoot,
            registerSession: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("session-registration-missing", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task MissingCharacterUidDoesNotPersistCommitOrSendMutationPackets()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(Item(50, EquipmentPackItemId));
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            Catalog(ItemType(820_073)),
            playerItemsRoot,
            seedPersistence: false,
            characterUid: 0);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("character-uid-unavailable", result.Log);
        Assert.False(Directory.Exists(playerItemsRoot));
    }

    [Fact]
    public async Task MissingPlayerItemsRootDoesNotCommitOrSendMutationPackets()
    {
        var originalItems = Items(Item(50, EquipmentPackItemId));
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            Catalog(ItemType(820_073)),
            playerItemsRoot: null,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("player-items-root-missing", result.Log);
    }

    [Fact]
    public async Task ExhaustedItemUidDoesNotConsumePackOrSendMutationPackets()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(
            Item(50, EquipmentPackItemId),
            Item(uint.MaxValue, 710_001));
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            Catalog(ItemType(820_073)),
            playerItemsRoot);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("item-uid-exhausted", result.Log);
        AssertPersistedEquals(playerItemsRoot, originalItems);
    }

    [Fact]
    public async Task PersistenceWriteFailureDoesNotCommitOrSendMutationPackets()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var originalItems = Items(Item(50, EquipmentPackItemId));
        var result = await InvokeAsync(
            EquipmentNpcId,
            optionId: 1,
            originalItems,
            Catalog(ItemType(820_073)),
            blockedPlayerItemsRoot,
            seedPersistence: false);

        AssertMutationFree(result, originalItems);
        Assert.Empty(result.Sent);
        Assert.Contains("reason=persistence-", result.Log);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
    }

    [Fact]
    public async Task PersistencePrecedesSessionCommitAndFirstMutationPacket()
    {
        var playerItemsRoot = CreatePlayerItemsRoot();
        var originalItems = Items(Item(50, WeaponPackItemId));
        var commitObserved = false;
        var firstPacketObserved = false;

        var result = await InvokeAsync(
            WeaponNpcId,
            optionId: 28,
            originalItems,
            Catalog(ItemType(827_013, durability: 444)),
            playerItemsRoot,
            sessionChanged: (_, previous, current) =>
            {
                if (previous is null)
                {
                    return;
                }

                AssertPersistedEquals(playerItemsRoot, current.PlayerItems);
                Assert.DoesNotContain(current.PlayerItems.InventoryItems, item => item.ItemId == WeaponPackItemId);
                Assert.Contains(current.PlayerItems.InventoryItems, item => item.ItemId == 827_013);
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
        Assert.Equal(2, result.Sent.Count);
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
        bool registerSession = true,
        long characterUid = CharacterUid,
        Action<ConquerTcpConnection, WorldSession?, WorldSession>? sessionChanged = null,
        Action<WorldSessionStore, ConquerTcpConnection, byte[]>? frameSent = null)
    {
        if (seedPersistence)
        {
            new PlayerItemSnapshotLoader(playerItemsRoot!).SaveByCharacterUid(characterUid, originalItems);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        WorldSessionStore? sessions = null;
        ConquerTcpConnection? connection = null;
        connection = CreateSendingConnection(
            sent,
            bytes => frameSent?.Invoke(sessions!, connection!, bytes));
        sessions = new WorldSessionStore(sessionChanged);
        var session = CreateSession(originalItems, characterUid);
        if (registerSession)
        {
            sessions.Set(connection, session);
        }

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

        var updatedSession = session;
        if (sessions.TryGet(connection, out var storedSession))
        {
            Assert.NotNull(storedSession);
            updatedSession = storedSession;
        }

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

    private static WorldSession CreateSession(
        PlayerItemSnapshot playerItems,
        long characterUid = CharacterUid)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "legacy-p7-soul-pack-mutation-test",
            Character: new CharacterSnapshot(
                Uid: characterUid,
                Name: "P7SoulTester",
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

    private static PlayerItemSnapshot Items(params PlayerItemSnapshotItem[] inventoryItems)
    {
        return new PlayerItemSnapshot(inventoryItems, [], [], [], []);
    }

    private static PlayerItemSnapshotItem Item(
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

    private static void AssertItemRemoval(byte[] bytes, uint uid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(uid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(0UL, packet.DwParam);
        Assert.Equal(0U, packet.DwParam2);
        Assert.Equal(0U, packet.DwParam3);
        Assert.Equal(0U, packet.DwParam4);
    }

    private static void AssertItemUpdate(
        byte[] bytes,
        uint uid,
        uint itemId,
        ushort stackSize,
        byte bound)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, packet.Mode);
        Assert.Equal(stackSize, packet.StackSize);
        Assert.Equal(bound, packet.Bound);
    }

    private static void AssertSoulGrant(
        byte[] bytes,
        uint uid,
        uint itemId,
        ushort durability)
    {
        var packet = AssertGameItem(bytes);
        Assert.Equal(uid, packet.Uid);
        Assert.Equal(itemId, packet.ItemId);
        Assert.Equal(durability, packet.Durability);
        Assert.Equal(durability, packet.MaximumDurability);
        Assert.Equal(LegacyGameItemPacket.AddItemMode, packet.Mode);
        Assert.Equal((byte)0, packet.Position);
        Assert.Equal(0U, packet.SocketProgress);
        Assert.Equal((byte)0, packet.SocketOne);
        Assert.Equal((byte)0, packet.SocketTwo);
        Assert.Equal(0U, packet.SpellId);
        Assert.Equal((byte)0, packet.Effect);
        Assert.Equal((byte)0, packet.Plus);
        Assert.Equal((byte)0, packet.Bless);
        Assert.Equal((byte)0, packet.Bound);
        Assert.Equal((byte)0, packet.Enchant);
        Assert.Equal(0U, packet.ProgressGreen);
        Assert.Equal(0U, packet.PlusProgress);
        Assert.Equal(0U, packet.RemainingTime);
        Assert.Equal(0U, packet.TimeLeftInMinutes);
        Assert.Equal((ushort)1, packet.StackSize);
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
        Assert.True(PacketFrameDecoder.TryRead(
            new ReadOnlySequence<byte>(bytes),
            out var frame,
            out _,
            out _));
        return frame;
    }

    private static string SourceForNpc(uint npcId)
    {
        return npcId switch
        {
            EquipmentNpcId => "NpcHandler.SelectP7EquipmentSoulPack",
            WeaponNpcId => "NpcHandler.SelectP7WeaponSoulPack",
            _ => throw new ArgumentOutOfRangeException(nameof(npcId)),
        };
    }

    private static WorldDataCatalog Catalog(params ItemType[] items)
    {
        return WorldDataCatalog.Create(
            new SeedManifest(
                DateTimeOffset.Parse("2026-07-11T00:00:00Z", CultureInfo.InvariantCulture),
                "fixture://legacy-p7-soul-pack-mutation",
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

    private static ItemType ItemType(uint itemTypeId, uint durability = 1)
    {
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = itemTypeId.ToString(CultureInfo.InvariantCulture);
        fields[1] = "P7Soul";
        fields[WolfConquer.Core.Data.ItemType.DurabilityFieldIndex] =
            durability.ToString(CultureInfo.InvariantCulture);
        return new ItemType(checked((int)itemTypeId), "P7Soul", fields);
    }

    private static GuildWarData EmptyGuildWarData(string name)
    {
        return new GuildWarData(
            "fixture://" + name,
            [],
            [],
            [],
            info: null,
            pole: null,
            conductors: []);
    }

    private sealed record InvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession OriginalSession,
        WorldSession UpdatedSession);
}
