using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    private readonly List<Player> closurePlayers = [];
    private readonly WorldOperationBarrier closureOperations = new();
    private readonly List<SocialAssociationRecord> closureRelations = [];
    private LegacyTradeRecoveryState? closureRecovery;
    private WorldDataCatalog? closureCatalog;
    private bool itemClosureCleanupHandled;

    private async Task RunItemClosureAsync(Func<Task> body)
    {
        Exception? primary = null;
        try { await body(); }
        catch (Exception error) { primary = error; }
        var cleanup = new List<Exception>();
        // Direct registered calls are awaited to their actual terminal, with no
        // timeout/cancellation surrogate, worker launch, or borrowed Host here.
        foreach (var player in closurePlayers)
        {
            try { await player.Connection.CompleteTransportCloseAsync(); }
            catch (Exception error) { cleanup.Add(error); }
        }
        itemClosureCleanupHandled = true;
        if (cleanup.Count == 0)
        {
            try { log.Dispose(); }
            catch (Exception error) { cleanup.Add(error); }
            try
            {
                var target = Path.GetFullPath(root);
                var temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
                if (!string.Equals(Path.GetDirectoryName(target), temporary,
                        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    !Path.GetFileName(target).StartsWith("wolf-trade-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing cleanup outside the exact owned trade fixture root.");
                if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            }
            catch (Exception error) { cleanup.Add(error); }
        }
        else cleanup.Add(new InvalidOperationException($"Retained owned trade fixture after transport cleanup failed: {root}"));
        if (primary is not null && cleanup.Count != 0)
            throw new AggregateException(new[] { primary }.Concat(cleanup));
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup.Count != 0) throw new AggregateException(cleanup);
        // Existing IDisposable skips these already-handled cases; a cleanup fault
        // cannot replace a primary assertion or delete an unresolved fixture.
    }

    private Player AddItemClosurePlayer(uint uid, long money, long cps,
        IReadOnlyList<PlayerItemSnapshotItem> inventory, Action<byte[]>? beforeSend = null)
    {
        var player = AddPlayer(uid, money, cps, inventory, beforeSend);
        closurePlayers.Add(player);
        var current = Current(player);
        // Birth the same genuine seven-store parsed graph as file-backed gameplay,
        // rather than relying on compiler-generated IReadOnlyList expressions.
        var parsed = new PlayerItemSnapshotLoader(ItemsRoot).LoadByCharacterUid(uid);
        Assert.Equal(File.ReadAllBytes(ItemPath(player)), PlayerItemSnapshotLoader.ToLegacyBytes(parsed));
        sessions.Set(player.Connection, current with { PlayerItems = parsed });
        return player;
    }

    private WorldPacketHandlerContext ItemClosureContext(Player player)
    {
        if (closureRecovery is null)
        {
            closureRecovery = new LegacyTradeRecoveryState(UsersRoot, ItemsRoot);
            Assert.True(closureRecovery.TryEnsureInitialized(out var reason), reason);
            closureCatalog = LegacyItemWorldDataFixture.Create([], [], socialAssociations: closureRelations);
        }
        Assert.True(closureRecovery.IsReadyFor(UsersRoot, ItemsRoot));
        return Context(player) with
        {
            WorldData = closureCatalog,
            ProtocolProfile = ConquerProtocolProfile.VisibleDx8D13,
            TradeOperations = closureOperations,
            TradeRecovery = closureRecovery,
        };
    }

    private async Task SendItemClosureAsync(Player player, LegacyTradePacket packet) =>
        await handlers.HandleAsync(ItemClosureContext(player), ReadFrame(packet.ToNativeFrameBytes()));

    private async Task OpenItemClosureAsync(Player left, Player right)
    {
        await SendItemClosureAsync(left, new(0) { UID = right.Uid });
        await SendItemClosureAsync(right, new(1) { UID = left.Uid });
        Assert.True(Current(left).LegacyTradeActive);
        Assert.True(Current(right).LegacyTradeActive);
        Assert.Contains(TradeFrames(left), packet => packet.Action == 3);
        Assert.Contains(TradeFrames(right), packet => packet.Action == 3);
    }

    private async Task DepositItemClosureAsync(Player player, uint sashUid, uint itemUid)
    {
        var request = new LegacyWarehousePacket(sashUid, LegacyWarehouseAction.ShashDepositItem,
            itemUid, Capacity: 0, Items: []);
        await handlers.HandleAsync(ItemClosureContext(player), ReadFrame(request.ToFrameBytes()));
        var sash = Assert.Single(Current(player).PlayerItems.InventoryItems, item => item.Uid == sashUid);
        Assert.Single(sash.DepositedItems, item => item.Uid == itemUid);
        Assert.DoesNotContain(Current(player).PlayerItems.InventoryItems, item => item.Uid == itemUid);
        // The registered transfer owns its Exclusive commit and actual save.
        AssertPersisted(player);
    }

    private void AddItemClosurePartner(Player owner, Player target) => closureRelations.Add(
        SocialAssociationParser.ParseRow("Associate.txt", 2,
            $"{owner.Uid}/3/600/0/0/{target.Uid}/0/0/0/0/Partner/0/0/0/0/1671008/140/5//14048/"));

    private static PlayerItemSnapshotItem ItemClosureSash(uint itemId = 1_100_009,
        params PlayerItemSnapshotItem[] children) => Item(10).WithItemId(itemId).WithDepositedItems(children);

    private static PlayerItemSnapshotItem RestrictedItemClosureChild(string restriction) => new()
    {
        Uid = 11, ItemId = restriction == "unable" ? 750_000U : 1_088_001U,
        Bound = restriction == "bound" ? (byte)1 : (byte)0,
        Locked = restriction == "locked" ? (byte)2 : (byte)0,
        UnlockTimer = restriction == "locked" ? 20991231 : 0,
        Inscribed = restriction == "inscribed" ? (ushort)1 : (ushort)0,
        Durability = 5, MaximumDurability = 10, StackSize = 1,
        OwnerUid = 77, OwnerName = "NestedOwner", Signature = "closure sentinel",
    };

    private ItemClosurePreimage CaptureItemClosure(Player player) => new(Current(player),
        JsonSerializer.Serialize(Current(player)), ReadItemClosureFiles(), player.Sent.Count);

    private Dictionary<string, byte[]> ReadItemClosureFiles() => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .ToDictionary(path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);

    private void AssertItemClosureFiles(Dictionary<string, byte[]> expected)
    {
        var actual = ReadItemClosureFiles();
        Assert.Equal(expected.Keys.Order(StringComparer.OrdinalIgnoreCase), actual.Keys.Order(StringComparer.OrdinalIgnoreCase));
        foreach (var (path, bytes) in expected) Assert.Equal(bytes, actual[path]);
    }

    private void AssertItemClosureUnchanged(Player player, ItemClosurePreimage before)
    {
        Assert.Same(before.Session, Current(player));
        Assert.Equal(before.Values, JsonSerializer.Serialize(Current(player)));
        AssertItemClosureFiles(before.Files);
    }

    private static void AssertItemClosureNoFinish(Player player, int start) => Assert.DoesNotContain(
        player.Sent.Skip(start), bytes => LegacyTradePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Action == 4);

    private static void AssertItemClosureNoIncoming(Player player, int start, uint incomingUid) => Assert.DoesNotContain(
        player.Sent.Skip(start), bytes => LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet) &&
            packet.Uid == incomingUid && packet.Mode == LegacyGameItemPacket.AddItemMode);

    private void AssertItemClosureSettled(Player left, Player right, long leftMoney, long rightMoney)
    {
        AssertWallet(left, leftMoney, 50);
        AssertWallet(right, rightMoney, 60);
        Assert.False(Current(left).LegacyTradeActive);
        Assert.False(Current(right).LegacyTradeActive);
        Assert.False(trades.Contains(left.Connection));
        Assert.False(trades.Contains(right.Connection));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        AssertPersisted(left);
        AssertPersisted(right);
    }

    private sealed record ItemClosurePreimage(WorldSession Session, string Values,
        Dictionary<string, byte[]> Files, int SentCount);
}
