using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [WindowsNpcSaleTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ActualBankCharacterOrItemsSharingRefusalKeepsWalletAndSameOwnerRetries(bool itemTarget, bool denyRead)
    {
        const uint uid = 1_299_946;
        Seed(uid, warehouseAlias: true);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid); var before = Current(owner, player);
        var native = player.Connection.NativeGameClient!;
        Assert.NotNull(native.OrdinarySpellBinding);
        AssertActualWarehouseWallet(owner, player, 1_000, 50, alias: true);
        var character = File.ReadAllBytes(UserPath(uid)); var items = File.ReadAllBytes(ItemPath(uid));
        int first = player.Sent.Count;
        using (File.Open(itemTarget ? ItemPath(uid) : UserPath(uid), FileMode.Open, FileAccess.Read,
            denyRead ? FileShare.None : FileShare.Read))
        {
            await SendWarehouseGoldRequest(owner, player, WarehouseGoldRequest(withdraw: false));

            Assert.Same(before, Current(owner, player)); Assert.Equal(first, player.Sent.Count);
            Assert.Equal((1_000L, 50L), (native.Player.Money, native.Player.WHMoney));
        }
        Assert.Equal(character, File.ReadAllBytes(UserPath(uid))); Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        AssertActualWarehouseWallet(owner, player, 1_000, 50, alias: true);

        await SendWarehouseGoldRequest(owner, player, WarehouseGoldRequest(withdraw: false));

        AssertActualWarehouseWallet(owner, player, 700, 350, alias: true);
        Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        AssertWarehouseGoldFrames(player.Sent.Skip(first).ToArray(), uid, WarehouseGoldRequest(withdraw: false), 700, 350);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Fact]
    public async Task RepeatedBankDepositValidatesCurrentFundsWithoutDuplicatingTheAcceptedTransfer()
    {
        const uint uid = 1_299_947;
        Seed(uid, warehouseAlias: true);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid);
        Assert.NotNull(player.Connection.NativeGameClient!.OrdinarySpellBinding);
        var request = WarehouseGoldRequest(withdraw: false) with { DwParam = 700 };
        int first = player.Sent.Count;

        await SendWarehouseGoldRequest(owner, player, request);

        AssertActualWarehouseWallet(owner, player, 300, 750, alias: true);
        AssertWarehouseGoldFrames(player.Sent.Skip(first).ToArray(), uid, request, 300, 750);
        var accepted = Current(owner, player);
        var character = File.ReadAllBytes(UserPath(uid)); var items = File.ReadAllBytes(ItemPath(uid));
        int replay = player.Sent.Count;

        await SendWarehouseGoldRequest(owner, player, request);

        Assert.Same(accepted, Current(owner, player)); Assert.Equal(replay, player.Sent.Count);
        Assert.Equal(character, File.ReadAllBytes(UserPath(uid))); Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        AssertActualWarehouseWallet(owner, player, 300, 750, alias: true);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Fact]
    public async Task RealBankCommitReleasesWorldLeaseBeforeItsFirstReply()
    {
        const uint uid = 1_299_950;
        Seed(uid, warehouseAlias: true);
        var owner = NewOwner(identity: new(42)); var barrier = ActualOperationBarrier(owner);
        Player? player = null; bool armed = false; int observations = 0; bool available = false;
        Exception? observed = null; Task cleanup = Task.CompletedTask;
        player = await Login(owner, uid, _ =>
        {
            if (!armed || observations != 0) return;
            observations++;
            observed = Record.Exception(() => AssertActualWarehouseWallet(owner, player!, 700, 350, alias: true));
            using var cancellation = new CancellationTokenSource();
            var acquisition = barrier.EnterExclusiveAsync(cancellation.Token);
            available = acquisition.IsCompletedSuccessfully;
            if (available) acquisition.Result.Dispose();
            else { cancellation.Cancel(); cleanup = DisposeCancelledProbe(acquisition); }
        });
        Assert.NotNull(player.Connection.NativeGameClient!.OrdinarySpellBinding);
        int first = player.Sent.Count; armed = true;

        await SendWarehouseGoldRequest(owner, player, WarehouseGoldRequest(withdraw: false));
        await cleanup.WaitAsync(TimeSpan.FromSeconds(5));

        armed = false; Assert.Equal(1, observations); Assert.Null(observed);
        Assert.True(available, "Neither the bank mutation nor outer dispatch may retain its world lease during replies.");
        AssertWarehouseGoldFrames(player.Sent.Skip(first).ToArray(), uid, WarehouseGoldRequest(withdraw: false), 700, 350);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    public async Task InterruptedRealBankJournalKeepsHolderUnpublishedAndFreshOwnerRecoversExactBalancesOnce(
        bool withdraw, int replacements)
    {
        const uint uid = 1_299_948;
        Seed(uid, warehouseMoney: withdraw ? 350 : 50, warehouseAlias: true);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid); var original = Current(owner, player);
        var native = player.Connection.NativeGameClient!;
        Assert.NotNull(native.OrdinarySpellBinding);
        var money = withdraw ? 1_100L : 700L; var warehouse = withdraw ? 250L : 350L;
        long amount = withdraw ? 100 : 300;
        var updated = WarehouseMoneyProposal(original, money, warehouse);
        var beforeCharacter = File.ReadAllBytes(UserPath(uid)); var items = File.ReadAllBytes(ItemPath(uid));
        var expectedCharacter = ExpectedPersistencePickupCharacterBytes(beforeCharacter,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Money"] = money.ToString(CultureInfo.InvariantCulture),
                ["WHMoney"] = warehouse.ToString(CultureInfo.InvariantCulture),
                ["WarehouseMoney"] = warehouse.ToString(CultureInfo.InvariantCulture),
            });
        var tuple = (native.Player.Money, native.Player.WHMoney); int first = player.Sent.Count;

        // Directly exercise the real writer's bounded managed interruption seam.
        // It deliberately does not publish a Store/native outcome; ordinary
        // dispatch success and refusal use the actual Store in adjacent cases.
        Assert.False(LegacyNpcSalePersistence.TryPersistWarehouseMoney(original, updated, ItemsRoot,
            deposit: !withdraw, amount: amount, interruption: LegacyJournalInterruption.StopAfter(replacements), out var reason));

        Assert.StartsWith(LegacyTradeJournal.RecoveryRequired, reason, StringComparison.Ordinal);
        Assert.True(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.Equal(replacements == 0 ? beforeCharacter : expectedCharacter, File.ReadAllBytes(UserPath(uid)));
        Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        Assert.Same(original, Current(owner, player)); Assert.Equal(tuple, (native.Player.Money, native.Player.WHMoney));
        Assert.Equal(first, player.Sent.Count);
        var pending = File.ReadAllBytes(PendingPath);
        var pendingCharacter = File.ReadAllBytes(UserPath(uid));
        await AssertPendingPickupPingIsFenced(owner, player);
        Assert.Same(original, Current(owner, player)); Assert.Equal(first, player.Sent.Count);
        Assert.Equal(pending, File.ReadAllBytes(PendingPath));
        Assert.Equal(pendingCharacter, File.ReadAllBytes(UserPath(uid))); Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));

        var recoveredOwner = NewOwner(identity: new(42));
        var recovered = await Login(recoveredOwner, uid);

        AssertActualWarehouseWallet(recoveredOwner, recovered, money, warehouse, alias: true);
        Assert.Equal(expectedCharacter, File.ReadAllBytes(UserPath(uid))); Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        Assert.True(LegacyTradeJournal.TryRecover(UsersRoot, ItemsRoot, out var replayed, out reason), reason);
        Assert.Equal(0, replayed);
        // Reusing the pre-commit image must fail, never credit the recovered
        // transfer a second time or resurrect an already-cleared journal.
        Assert.False(LegacyNpcSalePersistence.TryPersistWarehouseMoney(original, updated, ItemsRoot,
            deposit: !withdraw, amount: amount, out reason));
        Assert.Equal(expectedCharacter, File.ReadAllBytes(UserPath(uid))); Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        AssertActualWarehouseWallet(recoveredOwner, recovered, money, warehouse, alias: true);
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
        await AssertPendingPickupPingIsFenced(owner, player);
        Assert.Same(original, Current(owner, player)); Assert.Equal(tuple, (native.Player.Money, native.Player.WHMoney));
    }

    [Theory]
    [InlineData("unrelated-character")]
    [InlineData("unrelated-items")]
    [InlineData("missing-users")]
    [InlineData("missing-items")]
    [InlineData("stale-character")]
    [InlineData("stale-items")]
    [InlineData("wrong-delta")]
    public async Task RealBankWriterRejectsUnrelatedDeltaMissingRoutesAndStaleImagesBeforeAnyReplacement(string scenario)
    {
        const uint uid = 1_299_949;
        Seed(uid, warehouseAlias: true);
        var owner = NewOwner(identity: new(42));
        var player = await Login(owner, uid); var holder = Current(owner, player);
        Assert.NotNull(player.Connection.NativeGameClient!.OrdinarySpellBinding);
        var original = holder; var proposed = WarehouseMoneyProposal(holder, 700, 350);
        string? itemsRoot = ItemsRoot;
        switch (scenario)
        {
            case "unrelated-character": proposed = proposed with { Character = proposed.Character with { Level = holder.Character.Level + 1 } }; break;
            case "unrelated-items": proposed = proposed with { PlayerItems = proposed.PlayerItems with { InventoryItems = [] } }; break;
            case "missing-users": original = original with { UsersRoot = null }; proposed = proposed with { UsersRoot = null }; break;
            case "missing-items": itemsRoot = null; break;
            case "stale-character": ReplaceCharacterField(uid, "Money", "999"); break;
            case "stale-items": File.WriteAllBytes(ItemPath(uid), PlayerItemSnapshotLoader.ToLegacyBytes(
                holder.PlayerItems with { InventoryItems = [] })); break;
            case "wrong-delta": proposed = WarehouseMoneyProposal(holder, 699, 350); break;
            default: throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        var character = File.ReadAllBytes(UserPath(uid)); var items = File.ReadAllBytes(ItemPath(uid));
        var native = player.Connection.NativeGameClient!;
        var tuple = (native.Player.Money, native.Player.WHMoney, native.Player.ConquerPoints,
            native.Player.BoundConquerPoints, native.ConquerPointsSave);
        int first = player.Sent.Count;

        Assert.False(LegacyNpcSalePersistence.TryPersistWarehouseMoney(original, proposed, itemsRoot,
            deposit: true, amount: 300, out var reason));

        Assert.NotEmpty(reason); Assert.Same(holder, Current(owner, player)); Assert.Equal(first, player.Sent.Count);
        Assert.Equal(character, File.ReadAllBytes(UserPath(uid))); Assert.Equal(items, File.ReadAllBytes(ItemPath(uid)));
        Assert.Equal(tuple, (native.Player.Money, native.Player.WHMoney, native.Player.ConquerPoints,
            native.Player.BoundConquerPoints, native.ConquerPointsSave));
        Assert.False(LegacyTradeJournal.HasPending(UsersRoot));
    }

    private static WorldSession WarehouseMoneyProposal(WorldSession before, long money, long warehouse)
    {
        var attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        {
            ["Money"] = money.ToString(CultureInfo.InvariantCulture),
            ["WHMoney"] = warehouse.ToString(CultureInfo.InvariantCulture),
        };
        if (attributes.ContainsKey("WarehouseMoney")) attributes["WarehouseMoney"] = attributes["WHMoney"];
        return before with { Character = before.Character with { Money = money, Attributes = attributes } };
    }
}
