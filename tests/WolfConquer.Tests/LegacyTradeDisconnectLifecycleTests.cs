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
    public async Task TradeConfirmationWaitsForInFlightRepairThenCancelsChangedOfferWithoutDuplicatingItem()
    {
        var repairPaused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRepair = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseRepair = false;
        var repairPauseCount = 0;
        const uint offeredUid = 20;
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
            inventoryItems: new[] { offered }.Concat(meteors).ToArray(),
            beforeSendAsync: async (bytes, cancellationToken) =>
            {
                if (!pauseRepair || !LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var item) ||
                    item.Uid != offeredUid || item.Mode != LegacyGameItemPacket.UpdateItemMode ||
                    item.Durability != 10 || Interlocked.Exchange(ref repairPauseCount, 1) != 0) return;
                // This is the actual zero-durability repair's final send, immediately
                // before its unconditional Sessions.Set and item-file persistence.
                repairPaused.TrySetResult();
                await releaseRepair.Task.WaitAsync(cancellationToken);
            });
        await Trade(left, new(0) { UID = right.Uid });
        await Trade(right, new(1) { UID = left.Uid });
        await Trade(right, new(6) { ItemUID = offeredUid });
        await Trade(left, new(7) { Money = 300 });
        await Trade(right, new(12) { CPS = 10 });
        await Trade(right, new(9));
        Assert.True(Current(left).LegacyTradeActive);
        Assert.True(Current(right).LegacyTradeActive);
        Assert.Contains(TradePackets(left), packet => packet.Action == 9);

        var pending = new List<Task>();
        pauseRepair = true;
        try
        {
            var repair = handler.HandleFrameAsync(right.Connection,
                ReadFrame(new MsgItemUsagePacket(offeredUid, 0, MsgItemUsageAction.RepairItem, 123, 0, 0, 0).ToFrameBytes()),
                right.Lifetime.Token).AsTask();
            pending.Add(repair);
            await repairPaused.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(repair.IsCompleted);
            Assert.Equal((ushort)0, Assert.Single(Current(right).PlayerItems.InventoryItems,
                item => item.Uid == offeredUid).Durability);

            var confirmation = Trade(left, new(9));
            pending.Add(confirmation);
            Assert.False(confirmation.IsCompleted,
                "Trade must wait for the in-flight ordinary frame, not commit against its stale pre-repair snapshot.");
            Assert.DoesNotContain(Current(left).PlayerItems.InventoryItems, item => item.Uid == offeredUid);
            AssertPersistedWallet(left, 700, 50);
            AssertPersistedWallet(right, 2_000, 50);
        }
        finally
        {
            releaseRepair.TrySetResult();
            await Task.WhenAll(pending).WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal(1, Volatile.Read(ref repairPauseCount));
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        Assert.Equal(1_000, Current(left).Character.Money);
        Assert.Equal(60, Current(right).Character.ConquerPoints);
        AssertPersistedWallet(left, 1_000, 50);
        AssertPersistedWallet(right, 2_000, 60);
        Assert.DoesNotContain(Current(left).PlayerItems.InventoryItems, item => item.Uid == offeredUid);
        var repaired = Assert.Single(Current(right).PlayerItems.InventoryItems);
        Assert.Equal(offeredUid, repaired.Uid);
        Assert.Equal((ushort)10, repaired.Durability);
        Assert.Equal((ushort)10, repaired.MaximumDurability);
        Assert.DoesNotContain(Current(right).PlayerItems.InventoryItems, item => meteors.Any(meteor => meteor.Uid == item.Uid));
        var allItems = Current(left).PlayerItems.InventoryItems.Concat(Current(left).PlayerItems.WarehouseItems)
            .Concat(Current(right).PlayerItems.InventoryItems).Concat(Current(right).PlayerItems.WarehouseItems).ToArray();
        Assert.Single(allItems, item => item.Uid == offeredUid);
        Assert.Equal(allItems.Length, allItems.Select(item => item.Uid).Distinct().Count());
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current(left).PlayerItems), File.ReadAllBytes(ItemPath(left)));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(Current(right).PlayerItems), File.ReadAllBytes(ItemPath(right)));
        Assert.Single(TradePackets(left), packet => packet.Action == 5);
        Assert.Single(TradePackets(right), packet => packet.Action == 5);
        Assert.DoesNotContain(TradePackets(left), packet => packet.Action == 4);
        Assert.DoesNotContain(TradePackets(right), packet => packet.Action == 4);
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
