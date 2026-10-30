using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Actual managed frame-handler lifetime cancellation, not a live game-client test.
public sealed class LegacyTradeDisconnectLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-trade-disconnect-" + Guid.NewGuid().ToString("N"));
    private readonly List<Player> players = [];
    private readonly WorldServerFrameHandler handler;
    private readonly WorldSessionStore sessions;

    public LegacyTradeDisconnectLifecycleTests()
    {
        Directory.CreateDirectory(UsersRoot);
        Directory.CreateDirectory(ItemsRoot);
        handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(UsersRoot, ItemsRoot), TextWriter.Null);
        var field = typeof(WorldServerFrameHandler).GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        sessions = Assert.IsType<WorldSessionStore>(field.GetValue(handler));
    }

    [Fact]
    public async Task LoggedInTargetThatNeverSentTradeIsReleasedWhenItsConnectionLifetimeEnds()
    {
        var requester = await Login(1_294_001, 1_000, 50, 10);
        var target = await Login(1_294_002, 2_000, 60, 20);
        var nextTarget = await Login(1_294_003, 3_000, 70, 30);
        await Trade(requester, new(0) { UID = target.Uid });
        Assert.False(Current(requester).LegacyTradeActive);
        Assert.False(Current(target).LegacyTradeActive);

        // Target has only sent its login frame, never any Trade packet. Registering
        // cancellation only on Trade dispatch would leave this pending pair stuck.
        target.Lifetime.Cancel();
        await WaitUntil(() => !sessions.TryGet(target.Connection, out _));
        await Trade(requester, new(0) { UID = nextTarget.Uid });
        await Trade(nextTarget, new(1) { UID = requester.Uid });
        Assert.True(Current(requester).LegacyTradeActive);
        Assert.True(Current(nextTarget).LegacyTradeActive);
        Assert.Contains(TradePackets(nextTarget), packet => packet.Action == 3);
        await Trade(requester, new(2));
        Assert.False(Current(requester).LegacyTradeActive);
        Assert.False(Current(nextTarget).LegacyTradeActive);
    }

    [Fact]
    public async Task CancelledNonTradingScopeRetainsSessionButCannotBecomeANewTradeTarget()
    {
        var retired = await Login(1_294_061, 1_000, 50, 10);
        var requester = await Login(1_294_062, 2_000, 60, 20);
        var nextTarget = await Login(1_294_063, 3_000, 70, 30);
        var original = Current(retired);
        var field = typeof(WorldServerFrameHandler).GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var tradeStates = Assert.IsType<LegacyTradeStateStore>(field.GetValue(handler));
        retired.Lifetime.Cancel();
        await WaitUntil(() =>
        {
            lock (tradeStates.SyncRoot) return tradeStates.HasConnectionEnded(retired.Connection);
        });

        Assert.Same(original, Current(retired));
        await Trade(requester, new(0) { UID = retired.Uid });
        Assert.DoesNotContain(TradePackets(retired), packet => packet.Action == 0);
        await Trade(requester, new(0) { UID = nextTarget.Uid });
        await Trade(nextTarget, new(1) { UID = requester.Uid });
        Assert.True(Current(requester).LegacyTradeActive);
        Assert.True(Current(nextTarget).LegacyTradeActive);
        await Trade(requester, new(2));
        AssertPersistedWallet(retired, 1_000, 50);
    }

    [Fact]
    public async Task CancelledHandlerLifetimeRefundsEscrowRemovesDepartedSessionAndNotifiesPartnerOnce()
    {
        var left = await Login(1_294_011, 1_000, 50, 10);
        var right = await Login(1_294_012, 2_000, 60, 20);
        var originalLeftItems = File.ReadAllBytes(ItemPath(left));
        var originalRightItems = File.ReadAllBytes(ItemPath(right));
        await Trade(left, new(0) { UID = right.Uid });
        await Trade(right, new(1) { UID = left.Uid });
        await Trade(left, new(6) { ItemUID = 10 });
        await Trade(left, new(7) { Money = 300 });
        await Trade(right, new(12) { CPS = 10 });
        Assert.Equal(700, Current(left).Character.Money);
        Assert.Equal(50, Current(right).Character.ConquerPoints);
        var departingFrameCount = right.Sent.Count;

        right.Lifetime.Cancel();
        await WaitUntil(() => !sessions.TryGet(right.Connection, out _));
        Assert.Equal(departingFrameCount, right.Sent.Count);
        Assert.False(Current(left).LegacyTradeActive);
        Assert.Equal(1_000, Current(left).Character.Money);
        Assert.Equal(50, Current(left).Character.ConquerPoints);
        Assert.Single(TradePackets(left), packet => packet.Action == 5);
        AssertPersistedWallet(left, 1_000, 50);
        AssertPersistedWallet(right, 2_000, 60);
        Assert.Equal(originalLeftItems, File.ReadAllBytes(ItemPath(left)));
        Assert.Equal(originalRightItems, File.ReadAllBytes(ItemPath(right)));

        right.Lifetime.Cancel();
        await Trade(left, new(2));
        await Trade(left, new(9));
        Assert.False(sessions.TryGet(right.Connection, out _));
        Assert.Single(TradePackets(left), packet => packet.Action == 5);
        AssertPersistedWallet(left, 1_000, 50);
        AssertPersistedWallet(right, 2_000, 60);
        Assert.Equal(originalLeftItems, File.ReadAllBytes(ItemPath(left)));
        Assert.Equal(originalRightItems, File.ReadAllBytes(ItemPath(right)));
    }

    [Fact]
    public async Task DuplicateLoginRefundsTradeBeforeRemovingOldSessionRejectsSecondAndReloadsRefundOnThird()
    {
        var disconnects = 0;
        var left = await Login(1_294_021, 1_000, 50, 10,
            onDisconnect: () => Interlocked.Increment(ref disconnects));
        var refundedBeforeRemoval = false;
        var right = await Login(1_294_022, 2_000, 60, 20, beforeSend: bytes =>
        {
            if (!LegacyTradePacket.TryParse(ReadFrame(bytes), out var packet) || packet.Action != 5) return;
            refundedBeforeRemoval = sessions.TryGet(left.Connection, out var old) &&
                old is { LegacyTradeActive: false } && old.Character.Money == 1_000 &&
                old.Character.ConquerPoints == 50 && ReadPersistedWallet(left) == (1_000L, 50L);
        });
        var originalLeftItems = File.ReadAllBytes(ItemPath(left));
        var originalRightItems = File.ReadAllBytes(ItemPath(right));
        await Trade(left, new(0) { UID = right.Uid });
        await Trade(right, new(1) { UID = left.Uid });
        await Trade(left, new(6) { ItemUID = 10 });
        await Trade(left, new(7) { Money = 300 });
        await Trade(right, new(12) { CPS = 10 });

        var duplicate = CreateConnection(left.Uid);
        await SendLogin(duplicate);

        Assert.True(refundedBeforeRemoval, "The old session must still be reachable when its persisted refund is published.");
        Assert.Equal(1, Volatile.Read(ref disconnects));
        Assert.False(sessions.TryGet(left.Connection, out _));
        AssertDuplicateRejected(duplicate);
        Assert.False(Current(right).LegacyTradeActive);
        Assert.Equal(2_000, Current(right).Character.Money);
        Assert.Equal(60, Current(right).Character.ConquerPoints);
        AssertPersistedWallet(left, 1_000, 50);
        AssertPersistedWallet(right, 2_000, 60);
        Assert.Single(TradePackets(right), packet => packet.Action == 5);
        Assert.Equal(originalLeftItems, File.ReadAllBytes(ItemPath(left)));
        Assert.Equal(originalRightItems, File.ReadAllBytes(ItemPath(right)));

        var retry = CreateConnection(left.Uid);
        await SendLogin(retry);

        Assert.Equal(1_000, Current(retry).Character.Money);
        Assert.Equal(50, Current(retry).Character.ConquerPoints);
        Assert.False(Current(retry).LegacyTradeActive);
        Assert.Contains(retry.Sent, IsAnswerOk);
        Assert.Equal(originalLeftItems, PlayerItemSnapshotLoader.ToLegacyBytes(Current(retry).PlayerItems));
        Assert.Equal(1, Volatile.Read(ref disconnects));
    }

    [Fact]
    public async Task DuplicateLoginRetainsOldSessionAndEscrowWhenRefundCannotPersistThenCancellationRetriesOnce()
    {
        var disconnects = 0;
        var left = await Login(1_294_031, 1_000, 50, 10,
            onDisconnect: () => Interlocked.Increment(ref disconnects));
        var right = await Login(1_294_032, 2_000, 60, 20);
        var originalLeftItems = File.ReadAllBytes(ItemPath(left));
        var originalRightItems = File.ReadAllBytes(ItemPath(right));
        await Trade(left, new(0) { UID = right.Uid });
        await Trade(right, new(1) { UID = left.Uid });
        await Trade(left, new(6) { ItemUID = 10 });
        await Trade(left, new(7) { Money = 300 });
        await Trade(right, new(12) { CPS = 10 });
        var leftBeforeDuplicate = Current(left);
        var rightBeforeDuplicate = Current(right);
        var duplicate = CreateConnection(left.Uid);

        // Lock the other participant, not the duplicate UID's own item file: the
        // duplicate's initial snapshot load must reach the paired refund attempt.
        using (File.Open(ItemPath(right), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await SendLogin(duplicate);

            AssertDuplicateRejected(duplicate);
            Assert.Same(leftBeforeDuplicate, Current(left));
            Assert.Same(rightBeforeDuplicate, Current(right));
            Assert.True(Current(left).LegacyTradeActive);
            Assert.True(Current(right).LegacyTradeActive);
            Assert.Equal(700, Current(left).Character.Money);
            Assert.Equal(50, Current(right).Character.ConquerPoints);
            AssertPersistedWallet(left, 700, 50);
            AssertPersistedWallet(right, 2_000, 50);
            Assert.Equal(0, Volatile.Read(ref disconnects));
            Assert.DoesNotContain(TradePackets(left), packet => packet.Action == 5);
            Assert.DoesNotContain(TradePackets(right), packet => packet.Action == 5);
        }

        await Trade(left, new(2));
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        Assert.Equal(1_000, Current(left).Character.Money);
        Assert.Equal(60, Current(right).Character.ConquerPoints);
        AssertPersistedWallet(left, 1_000, 50);
        AssertPersistedWallet(right, 2_000, 60);
        Assert.Single(TradePackets(left), packet => packet.Action == 5);
        Assert.Single(TradePackets(right), packet => packet.Action == 5);
        Assert.Equal(originalLeftItems, File.ReadAllBytes(ItemPath(left)));
        Assert.Equal(originalRightItems, File.ReadAllBytes(ItemPath(right)));

        await Trade(left, new(2));
        await Trade(right, new(2));
        AssertPersistedWallet(left, 1_000, 50);
        AssertPersistedWallet(right, 2_000, 60);
        Assert.Single(TradePackets(left), packet => packet.Action == 5);
        Assert.Single(TradePackets(right), packet => packet.Action == 5);
        Assert.Equal(0, Volatile.Read(ref disconnects));
    }

    [Fact]
    public async Task ActiveTradeRepairRefusesAndQueuedItemMoveMakesConfirmationRefundWithoutDuplication()
    {
        const uint offeredUid = 20;
        const uint bank = 8;
        var left = await Login(1_294_051, 1_000, 50, 10);
        var offered = new PlayerItemSnapshotItem
        {
            Uid = offeredUid, ItemId = 101_000, Durability = 0, MaximumDurability = 10, StackSize = 1,
        };
        var meteors = Enumerable.Range(21, 5).Select(uid => new PlayerItemSnapshotItem
        {
            Uid = checked((uint)uid), ItemId = 1_088_001, StackSize = 1,
        }).ToArray();
        var right = await Login(1_294_052, 2_000, 60, offeredUid,
            inventoryItems: new[] { offered }.Concat(meteors).ToArray());
        await Trade(left, new(0) { UID = right.Uid });
        await Trade(right, new(1) { UID = left.Uid });
        await Trade(right, new(6) { ItemUID = offeredUid });
        await Trade(left, new(7) { Money = 300 });
        await Trade(right, new(12) { CPS = 10 });
        await Trade(right, new(9));
        var operations = ActualOwner<WorldOperationBarrier>("_worldOperations");
        var trades = ActualOwner<LegacyTradeStateStore>("_tradeStates");
        var recovery = ActualOwner<LegacyTradeRecoveryState>("_tradeRecovery");
        Assert.True(recovery.IsReadyFor(UsersRoot, ItemsRoot));
        Assert.True(recovery.TryRequireLiveJournalClear(out var recoveryReason), recoveryReason);
        Assert.Null(left.Connection.NativeGameClient);
        Assert.Null(right.Connection.NativeGameClient);
        var leftBefore = Current(left);
        var rightBefore = Current(right);
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(left.Connection, leftBefore));
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(right.Connection, rightBefore));
        Assert.True(leftBefore.LegacyTradeActive);
        Assert.True(rightBefore.LegacyTradeActive);
        Assert.Contains(TradePackets(left), packet => packet.Action == 9);
        var leftBeforeJson = System.Text.Json.JsonSerializer.Serialize(leftBefore);
        var rightBeforeJson = System.Text.Json.JsonSerializer.Serialize(rightBefore);
        var filesBefore = Files();
        int leftFramesBefore = left.Sent.Count;
        int rightFramesBefore = right.Sent.Count;
        LegacyTradeParty leftParty;
        LegacyTradeParty rightParty;
        LegacyTradeOffer originalOffer;
        lock (trades.SyncRoot)
        {
            Assert.True(trades.TryGet(left.Connection, out leftParty!));
            Assert.True(trades.TryGet(right.Connection, out rightParty!));
            originalOffer = Assert.Single(rightParty.Items).Value;
        }
        using var work = CancellationTokenSource.CreateLinkedTokenSource(left.Lifetime.Token, right.Lifetime.Token);
        work.CancelAfter(TimeSpan.FromSeconds(30));

        // An actual owned Shared operation blocks the repair's real Exclusive
        // preflight. No response callback is used as an operation-lifetime proxy.
        var repairPending = new List<Task>();
        var repairHold = await operations.EnterSharedAsync(work.Token);
        Exception? repairObservation = null;
        try
        {
            var repair = handler.HandleFrameAsync(right.Connection,
                ReadFrame(new MsgItemUsagePacket(offeredUid, 0, MsgItemUsageAction.RepairItem,
                    123, 0, 0, 0).ToFrameBytes()), work.Token).AsTask();
            repairPending.Add(repair);
            Assert.False(repair.IsCompleted);
            RequireUnchangedActiveTrade();
        }
        catch (Exception error) { repairObservation = error; }
        await FinishBlockedPhase(repairHold, repairPending, repairObservation);
        work.Token.ThrowIfCancellationRequested();

        // Required real owner/PIN/roots/materials are present. Active trade and
        // escrow refuse before repair costs, item changes, files or success output.
        RequireUnchangedActiveTrade();
        var moved = Assert.Single(rightBefore.PlayerItems.InventoryItems,
            item => item.Uid == offeredUid).WithWarehouseId(bank, 0);
        var expectedRightItems = rightBefore.PlayerItems with
        {
            InventoryItems = rightBefore.PlayerItems.InventoryItems.Where(item => item.Uid != offeredUid).ToArray(),
            WarehouseItems = rightBefore.PlayerItems.WarehouseItems.Append(moved)
                .OrderBy(item => item.WarehouseId).ThenBy(item => item.Uid).ToArray(),
        };

        // Warehouse's existing coherent-trade route may move an offered item.
        // Queue its real writer first, then confirmation: FIFO world admission
        // must let confirmation validate the durable changed offer and refund it.
        var movePending = new List<Task>();
        var moveHold = await operations.EnterSharedAsync(work.Token);
        Exception? moveObservation = null;
        try
        {
            var move = handler.HandleFrameAsync(right.Connection,
                ReadFrame(new LegacyWarehousePacket(bank, LegacyWarehouseAction.DepositItem,
                    offeredUid, 0, []).ToFrameBytes()), work.Token).AsTask();
            movePending.Add(move);
            var confirmation = handler.HandleFrameAsync(left.Connection,
                ReadFrame(new LegacyTradePacket(9).ToNativeFrameBytes()), work.Token).AsTask();
            movePending.Add(confirmation);
            Assert.False(move.IsCompleted);
            Assert.False(confirmation.IsCompleted);
            RequireUnchangedActiveTrade();
        }
        catch (Exception error) { moveObservation = error; }
        await FinishBlockedPhase(moveHold, movePending, moveObservation);
        work.Token.ThrowIfCancellationRequested();

        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        Assert.Equal(1_000, Current(left).Character.Money);
        Assert.Equal(50, Current(left).Character.ConquerPoints);
        Assert.Equal(2_000, Current(right).Character.Money);
        Assert.Equal(60, Current(right).Character.ConquerPoints);
        AssertPersistedWallet(left, 1_000, 50);
        AssertPersistedWallet(right, 2_000, 60);
        Assert.DoesNotContain(Current(left).PlayerItems.InventoryItems, item => item.Uid == offeredUid);
        Assert.DoesNotContain(Current(right).PlayerItems.InventoryItems, item => item.Uid == offeredUid);
        var retained = Assert.Single(Current(right).PlayerItems.WarehouseItems, item => item.Uid == offeredUid);
        Assert.Equal(bank, retained.WarehouseId);
        Assert.Equal((ushort)0, retained.Durability);
        Assert.Equal((ushort)10, retained.MaximumDurability);
        foreach (var meteor in meteors)
        {
            var actual = Assert.Single(Current(right).PlayerItems.InventoryItems, item => item.Uid == meteor.Uid);
            Assert.Equal(meteor.ItemId, actual.ItemId);
            Assert.Equal((ushort)1, actual.StackSize);
        }
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(leftBefore.PlayerItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(Current(left).PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(expectedRightItems),
            PlayerItemSnapshotLoader.ToLegacyBytes(Current(right).PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current(left).PlayerItems), File.ReadAllBytes(ItemPath(left)));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current(right).PlayerItems), File.ReadAllBytes(ItemPath(right)));
        var allItems = Current(left).PlayerItems.InventoryItems.Concat(Current(left).PlayerItems.WarehouseItems)
            .Concat(Current(right).PlayerItems.InventoryItems).Concat(Current(right).PlayerItems.WarehouseItems).ToArray();
        var originalItems = leftBefore.PlayerItems.InventoryItems.Concat(leftBefore.PlayerItems.WarehouseItems)
            .Concat(rightBefore.PlayerItems.InventoryItems).Concat(rightBefore.PlayerItems.WarehouseItems).ToArray();
        Assert.Equal(originalItems.Select(item => item.Uid).Order(), allItems.Select(item => item.Uid).Order());
        Assert.Single(allItems, item => item.Uid == offeredUid);
        Assert.Equal(allItems.Length, allItems.Select(item => item.Uid).Distinct().Count());
        Assert.Single(TradePackets(left), packet => packet.Action == 5);
        Assert.Single(TradePackets(right), packet => packet.Action == 5);
        Assert.DoesNotContain(TradePackets(left), packet => packet.Action == 4);
        Assert.DoesNotContain(TradePackets(right), packet => packet.Action == 4);
        Assert.True(LegacyTradeEscrowClaims.TryRead(Current(left).Character.Attributes, out var leftClaim, out _));
        Assert.True(LegacyTradeEscrowClaims.TryRead(Current(right).Character.Attributes, out var rightClaim, out _));
        Assert.Null(leftClaim);
        Assert.Null(rightClaim);
        lock (trades.SyncRoot)
        {
            Assert.False(trades.Contains(left.Connection));
            Assert.False(trades.Contains(right.Connection));
        }
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.False(recovery.IsBlocked);
        var leftFinal = Current(left);
        var rightFinal = Current(right);
        var finalFiles = Files();
        int leftFinalFrames = left.Sent.Count;
        int rightFinalFrames = right.Sent.Count;

        await Trade(left, new(2));
        await Trade(right, new(2));

        Assert.Same(leftFinal, Current(left));
        Assert.Same(rightFinal, Current(right));
        RequireFiles(finalFiles);
        Assert.Equal(leftFinalFrames, left.Sent.Count);
        Assert.Equal(rightFinalFrames, right.Sent.Count);
        Assert.Single(TradePackets(left), packet => packet.Action == 5);
        Assert.Single(TradePackets(right), packet => packet.Action == 5);

        T ActualOwner<T>(string name) where T : class
        {
            var field = typeof(WorldServerFrameHandler).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return Assert.IsType<T>(field.GetValue(handler)); // Observe the existing real owner, never install one.
        }

        Dictionary<string, byte[]> Files() => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

        void RequireFiles(Dictionary<string, byte[]> expected)
        {
            var actual = Files();
            Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
            foreach (var pair in expected)
                Assert.Equal(pair.Value, actual[pair.Key]);
        }

        void RequireUnchangedActiveTrade()
        {
            Assert.Same(leftBefore, Current(left));
            Assert.Same(rightBefore, Current(right));
            Assert.Equal(leftBeforeJson, System.Text.Json.JsonSerializer.Serialize(Current(left)));
            Assert.Equal(rightBeforeJson, System.Text.Json.JsonSerializer.Serialize(Current(right)));
            Assert.Equal(leftFramesBefore, left.Sent.Count);
            Assert.Equal(rightFramesBefore, right.Sent.Count);
            AssertPersistedWallet(left, 700, 50);
            AssertPersistedWallet(right, 2_000, 50);
            RequireFiles(filesBefore);
            lock (trades.SyncRoot)
            {
                Assert.True(trades.TryGet(left.Connection, out var actualLeft));
                Assert.True(trades.TryGet(right.Connection, out var actualRight));
                Assert.Same(leftParty, actualLeft);
                Assert.Same(rightParty, actualRight);
                Assert.Same(leftParty, rightParty.Other);
                Assert.Same(rightParty, leftParty.Other);
                Assert.True(leftParty.WindowOpen);
                Assert.True(rightParty.WindowOpen);
                Assert.False(leftParty.Confirmed);
                Assert.True(rightParty.Confirmed);
                Assert.Equal((300L, 0u), (leftParty.Money, leftParty.ConquerPoints));
                Assert.Equal((0L, 10u), (rightParty.Money, rightParty.ConquerPoints));
                Assert.Empty(leftParty.Items);
                Assert.Same(originalOffer, Assert.Single(rightParty.Items).Value);
            }
        }

        static async Task FinishBlockedPhase(IDisposable held, List<Task> producers, Exception? primary)
        {
            var errors = new List<Exception>();
            if (primary is not null)
                errors.Add(primary);
            try { held.Dispose(); }
            catch (Exception cleanup) { errors.Add(cleanup); }
            foreach (var producer in producers)
            {
                try { await producer; } // Actual bounded-token producers reach terminal before fixture deletion.
                catch (Exception cleanup) { errors.Add(cleanup); }
            }
            if (errors.Count == 1)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1)
                throw new AggregateException("Trade/repair observations and owned producer cleanup failed.", errors);
        }
    }

    public void Dispose()
    {
        foreach (var player in players) player.Lifetime.Dispose();
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string UsersRoot => Path.Combine(root, "Users");
    private string ItemsRoot => Path.Combine(root, "PlayersItems");
    private string UserPath(Player player) => Path.Combine(UsersRoot, player.Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
    private string ItemPath(Player player) => Path.Combine(ItemsRoot, player.Uid.ToString(CultureInfo.InvariantCulture) + ".bin");

    private Player CreateConnection(uint uid, Action? onDisconnect = null, Action<byte[]>? beforeSend = null,
        Func<byte[], CancellationToken, ValueTask>? beforeSendAsync = null)
    {
        var sent = new ConcurrentQueue<byte[]>();
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            async (bytes, cancellationToken) =>
            {
                var copy = bytes.ToArray();
                beforeSend?.Invoke(copy);
                if (beforeSendAsync is not null) await beforeSendAsync(copy, cancellationToken);
                sent.Enqueue(copy);
            },
            _ => { onDisconnect?.Invoke(); return ValueTask.CompletedTask; });
        var player = new Player(uid, connection, new CancellationTokenSource(), sent);
        players.Add(player);
        return player;
    }

    private async Task<Player> Login(uint uid, long money, long cps, uint itemUid,
        Action? onDisconnect = null, Action<byte[]>? beforeSend = null,
        Func<byte[], CancellationToken, ValueTask>? beforeSendAsync = null,
        PlayerItemSnapshotItem[]? inventoryItems = null)
    {
        var player = CreateConnection(uid, onDisconnect, beforeSend, beforeSendAsync);
        File.WriteAllText(UserPath(player), $$"""
            [Character]
            UID={{uid}}
            Name=Trader{{uid}}
            Body=1008
            Face=154
            Haire=27
            Class=10
            FirstClass=0
            SecoundeClass=0
            Reborn=0
            Level=100
            Experience=0
            Map=1002
            X=430
            Y=380
            PMap=1002
            PMapX=430
            PMapY=380
            Strength=10
            Agility=10
            Vitaliti=10
            Spirit=10
            Atributes=0
            MinHitPoints=1000
            MinMana=100
            Money={{money}}
            ConquerPoints={{cps}}
            BoundConquerPoints=333
            BankCps=444
            PkPoints=0
            Online=1
            """);
        var inventory = new PlayerItemSnapshotItem
        {
            Uid = itemUid, ItemId = 1_088_001, Durability = 5, MaximumDurability = 10, StackSize = 1,
        };
        var warehouse = new PlayerItemSnapshotItem
        {
            Uid = uid + 100, ItemId = 1_088_001, WarehouseId = 1000, StackSize = 1,
        };
        new PlayerItemSnapshotLoader(ItemsRoot).SaveByCharacterUid(uid,
            new PlayerItemSnapshot(inventoryItems ?? [inventory], [], [], [], []) { WarehouseItems = [warehouse] });
        await SendLogin(player);
        Assert.Equal(uid, Current(player).Character.Uid);
        return player;
    }

    private async Task SendLogin(Player player) => await handler.HandleFrameAsync(player.Connection,
        ReadFrame(new LegacyMsgLoginGamePacket(player.Uid).ToFrameBytes()), player.Lifetime.Token);

    private async Task Trade(Player player, LegacyTradePacket packet) => await handler.HandleFrameAsync(
        player.Connection, ReadFrame(packet.ToNativeFrameBytes()), player.Lifetime.Token);

    private WorldSession Current(Player player)
    {
        Assert.True(sessions.TryGet(player.Connection, out var session));
        return Assert.IsType<WorldSession>(session);
    }

    private void AssertPersistedWallet(Player player, long money, long cps)
    {
        Assert.Equal((money, cps), ReadPersistedWallet(player));
    }

    private (long Money, long ConquerPoints) ReadPersistedWallet(Player player)
    {
        var character = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(player)));
        return (character.Money, character.ConquerPoints);
    }

    private void AssertDuplicateRejected(Player player)
    {
        Assert.False(sessions.TryGet(player.Connection, out _));
        var bytes = Assert.Single(player.Sent);
        Assert.True(LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var dialog));
        Assert.Equal("Your account is already logged in on another computer. Please try again.", dialog.Content[3]);
        Assert.DoesNotContain(player.Sent, IsAnswerOk);
    }

    private static bool IsAnswerOk(byte[] bytes) =>
        LegacyMsgMessagePacket.TryParse(ReadFrame(bytes), out var packet) &&
        packet.Content.Count > 3 && packet.Content[3] == "ANSWER_OK";

    private static async Task WaitUntil(Func<bool> condition)
    {
        var elapsed = Stopwatch.StartNew();
        while (!condition() && elapsed.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(10);
        Assert.True(condition(), "Queued connection-lifetime cleanup did not finish within five seconds.");
    }

    private static LegacyTradePacket[] TradePackets(Player player) => player.Sent.ToArray()
        .Select(ReadFrame).Where(frame => frame.Header.Opcode == WorldPacketOpcodes.LegacyTrade)
        .Select(frame => { Assert.True(LegacyTradePacket.TryParse(frame, out var packet)); return packet; }).ToArray();

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private sealed record Player(uint Uid, ConquerTcpConnection Connection,
        CancellationTokenSource Lifetime, ConcurrentQueue<byte[]> Sent);
}
