using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyClearInventoryMutationTests : IDisposable
{
    private const long AccountId = 1_293_600;
    private const uint CharacterUid = 1_293_600;
    private const uint ClearInventoryNpcId = 1_245;

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyClearInventoryMutationTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task OptionFiveRemovesWholeStacksAfterPersistenceAndSendsExactFramesInUidOrder()
    {
        var items = PlayerItemSnapshot.Empty with
        {
            InventoryItems =
            [
                CreateItem(uid: 30, itemId: 7_100_030, stackSize: 99),
                CreateItem(uid: 10, itemId: 7_100_010, stackSize: 2),
                CreateItem(uid: 20, itemId: 7_100_020, stackSize: 1),
            ],
        };
        var result = await InvokeAsync(items, CreatePlayerItemsRoot());

        Assert.True(result.Handled);
        Assert.NotSame(result.OriginalSession, result.UpdatedSession);
        Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
        Assert.Equal(1, result.CommitCount);
        Assert.Empty(Assert.IsType<PlayerItemSnapshot>(result.PersistedAtCommit).InventoryItems);
        Assert.Empty(new PlayerItemSnapshotLoader(result.PlayerItemsRoot)
            .LoadByCharacterUid(CharacterUid)
            .InventoryItems);

        Assert.Collection(
            result.Sent,
            frame => AssertRemoveInventoryFrame(frame, 10),
            frame => AssertRemoveInventoryFrame(frame, 20),
            frame => AssertRemoveInventoryFrame(frame, 30));
        Assert.Equal([10U, 20U, 30U], result.Sent.Select(ParseRemoveInventoryUid).ToArray());
        Assert.Contains("removeFull=true", result.Log);
        Assert.Contains("persistenceBeforeSessionCommit=true", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Fact]
    public async Task OptionFivePreservesEveryNonInventoryCollectionAndEquipment()
    {
        var items = new PlayerItemSnapshot(
            InventoryItems: [CreateItem(uid: 10, itemId: 7_100_010, stackSize: 8)],
            RelicFuseItems: [CreateItem(uid: 101, itemId: 8_100_101, position: 213)],
            MythsoulBagItems: [CreateItem(uid: 102, itemId: 8_100_102, position: 217)],
            EquipmentItems: [CreateItem(uid: 103, itemId: 410_339, position: 4)],
            RuneItems: [CreateItem(uid: 104, itemId: 4_030_109, position: 101)],
            WardrobeItems: [CreateItem(uid: 105, itemId: 200_614, warehouseId: 100)])
        {
            WarehouseItems = [CreateItem(uid: 106, itemId: 1_088_001, warehouseId: 6)],
        };
        var expected = items with { InventoryItems = [] };
        var result = await InvokeAsync(items, CreatePlayerItemsRoot());

        AssertPlayerItemsEqual(expected, result.UpdatedSession.PlayerItems);
        var persisted = new PlayerItemSnapshotLoader(result.PlayerItemsRoot)
            .LoadByCharacterUid(CharacterUid);
        AssertPlayerItemsEqual(expected, persisted);
        Assert.Single(result.Sent);
        AssertRemoveInventoryFrame(result.Sent[0], 10);
    }

    [Fact]
    public async Task OptionFiveWithEmptyInventoryIsANoOp()
    {
        var items = PlayerItemSnapshot.Empty;
        var result = await InvokeAsync(items, CreatePlayerItemsRoot());

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(0, result.CommitCount);
        Assert.Null(result.PersistedAtCommit);
        Assert.Empty(result.Sent);
        AssertPlayerItemsEqual(
            items,
            new PlayerItemSnapshotLoader(result.PlayerItemsRoot).LoadByCharacterUid(CharacterUid));
        Assert.Contains("removedCount=0", result.Log);
    }

    [Theory]
    [InlineData(false, true, "sessionStoreMissing")]
    [InlineData(true, false, "playerItemsRootMissing")]
    public async Task MissingMutationWiringLeavesSessionAndPersistenceUnchanged(
        bool includeSessions,
        bool includePlayerItemsRoot,
        string expectedReason)
    {
        var items = PlayerItemSnapshot.Empty with
        {
            InventoryItems = [CreateItem(uid: 10, itemId: 7_100_010, stackSize: 4)],
        };
        var result = await InvokeAsync(
            items,
            CreatePlayerItemsRoot(),
            includeSessions,
            includePlayerItemsRoot);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(0, result.CommitCount);
        Assert.Null(result.PersistedAtCommit);
        AssertPlayerItemsEqual(items, result.UpdatedSession.PlayerItems);
        AssertPlayerItemsEqual(
            items,
            new PlayerItemSnapshotLoader(result.PlayerItemsRoot).LoadByCharacterUid(CharacterUid));
        Assert.DoesNotContain(result.Sent, IsRemoveInventoryFrame);
        Assert.Contains(expectedReason, result.Log);
    }

    [Fact]
    public async Task PersistenceFailureDoesNotCommitOrSendRemoveFrames()
    {
        Directory.CreateDirectory(_root);
        var blockedPlayerItemsRoot = Path.Combine(_root, "blocked-player-items-root");
        File.WriteAllText(blockedPlayerItemsRoot, "not a directory");
        var items = PlayerItemSnapshot.Empty with
        {
            InventoryItems = [CreateItem(uid: 10, itemId: 7_100_010, stackSize: 7)],
        };
        var result = await InvokeAsync(
            items,
            blockedPlayerItemsRoot,
            seedPersistence: false);

        Assert.True(result.Handled);
        Assert.Same(result.OriginalSession, result.UpdatedSession);
        Assert.Equal(0, result.CommitCount);
        Assert.Null(result.PersistedAtCommit);
        AssertPlayerItemsEqual(items, result.UpdatedSession.PlayerItems);
        Assert.Empty(result.Sent);
        Assert.Equal("not a directory", File.ReadAllText(blockedPlayerItemsRoot));
        Assert.Contains("clearinventory persistence", result.Log);
        Assert.Contains("no session mutation or remove-inventory frames were committed", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<InvokeResult> InvokeAsync(
        PlayerItemSnapshot items,
        string playerItemsRoot,
        bool includeSessions = true,
        bool includePlayerItemsRoot = true,
        bool seedPersistence = true)
    {
        var itemLoader = new PlayerItemSnapshotLoader(playerItemsRoot);
        if (seedPersistence)
        {
            itemLoader.SaveByCharacterUid(CharacterUid, items);
        }

        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(items);
        PlayerItemSnapshot? persistedAtCommit = null;
        var commitCount = 0;
        var sessions = new WorldSessionStore((_, previous, current) =>
        {
            if (previous is null)
            {
                return;
            }

            commitCount++;
            persistedAtCommit = itemLoader.LoadByCharacterUid(current.Character.Uid);
        });
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyUtilityNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: includeSessions ? sessions : null,
                PlayerItemsRoot: includePlayerItemsRoot ? playerItemsRoot : null),
            session,
            ClearInventoryNpcId,
            optionId: 5,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new InvokeResult(
            handled,
            session,
            updatedSession,
            persistedAtCommit,
            commitCount,
            sent,
            log.ToString(),
            playerItemsRoot);
    }

    private static WorldSession CreateSession(PlayerItemSnapshot items)
    {
        return new WorldSession(
            AccountId,
            "clear-inventory-test",
            new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "ClearInventoryHero",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 15,
                FirstClass: 15,
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
                Attributes: new Dictionary<string, string>(),
                PaidNobility: new Dictionary<string, string>()),
            items,
            PlayerSpellSnapshot.Empty,
            PlayerProficiencySnapshot.Empty);
    }

    private static PlayerItemSnapshotItem CreateItem(
        uint uid,
        uint itemId,
        ushort position = 0,
        ushort stackSize = 1,
        uint warehouseId = 0)
    {
        return new PlayerItemSnapshotItem
        {
            Uid = uid,
            ItemId = itemId,
            Durability = 100,
            MaximumDurability = 100,
            Position = position,
            StackSize = stackSize,
            WarehouseId = warehouseId,
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

    private string CreatePlayerItemsRoot()
    {
        return Path.Combine(_root, "PlayersItems-" + Guid.NewGuid().ToString("N"));
    }

    private static void AssertRemoveInventoryFrame(byte[] bytes, uint expectedUid)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(expectedUid, packet.Id);
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(0UL, packet.DwParam);
        Assert.NotEqual(0U, packet.Timestamp);
        Assert.Equal(0U, packet.DwParam2);
        Assert.Equal(0U, packet.DwParam3);
        Assert.Equal(0U, packet.DwParam4);
        Assert.Equal(
            new MsgItemUsagePacket(
                expectedUid,
                DwParam: 0,
                MsgItemUsageAction.RemoveInventory,
                packet.Timestamp,
                DwParam2: 0,
                DwParam3: 0,
                DwParam4: 0)
            .ToFrameBytes(),
            bytes);
    }

    private static uint ParseRemoveInventoryUid(byte[] bytes)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet));
        Assert.Equal(MsgItemUsageAction.RemoveInventory, packet.Action);
        Assert.Equal(0UL, packet.DwParam);
        Assert.NotEqual(0U, packet.Timestamp);
        Assert.Equal(0U, packet.DwParam2);
        Assert.Equal(0U, packet.DwParam3);
        Assert.Equal(0U, packet.DwParam4);
        return packet.Id;
    }

    private static bool IsRemoveInventoryFrame(byte[] bytes)
    {
        return MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Action == MsgItemUsageAction.RemoveInventory;
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

    private sealed record InvokeResult(
        bool Handled,
        WorldSession OriginalSession,
        WorldSession UpdatedSession,
        PlayerItemSnapshot? PersistedAtCommit,
        int CommitCount,
        IReadOnlyList<byte[]> Sent,
        string Log,
        string PlayerItemsRoot);
}
