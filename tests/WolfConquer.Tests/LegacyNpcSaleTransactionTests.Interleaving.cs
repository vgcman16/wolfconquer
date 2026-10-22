using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNpcSaleTransactionTests
{
    [Fact]
    public async Task SaleWaitsForExistingSharedOperationAndCommitsFromItsLatestPublishedWallet()
    {
        const uint uid = 1_299_081;
        Seed(uid);
        var owner = NewOwner();
        var player = await Login(owner, uid);
        var barrier = ActualOperationBarrier(owner);
        var original = Current(owner, player);
        var characterBytes = File.ReadAllBytes(UserPath(uid));
        var itemBytes = File.ReadAllBytes(ItemPath(uid));
        var first = player.Sent.Count;
        Task? sale = null;
        WorldSession? latest = null;
        try
        {
            using (await barrier.EnterSharedAsync())
            {
                sale = Sell(owner, player);
                Assert.False(sale.IsCompleted);
                Assert.Same(original, Current(owner, player));
                Assert.Equal(characterBytes, File.ReadAllBytes(UserPath(uid)));
                Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath(uid)));
                Assert.DoesNotContain(player.Sent.Skip(first), IsSaleSuccessFrame);

                // Publish the same wallet mutation as native warehouse deposit's
                // managed UpdateSessionWallet through the real store/callback.
                // The existing shared operation finishes while Sale is queued.
                // Money's raw attribute intentionally remains its old value, as
                // that ordinary owner does; the typed Money and disk are current.
                var attributes = new Dictionary<string, string>(original.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                {
                    ["WHMoney"] = "150",
                };
                latest = original with { Character = original.Character with { Money = 900, Attributes = attributes } };
                owner.Sessions.Set(player.Connection, latest);
                var saved = CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid)));
                Assert.Equal(900, saved.Money);
                Assert.Equal("150", saved.Attributes["WHMoney"]);
                Assert.Same(latest, Current(owner, player));
                Assert.False(sale.IsCompleted);
                Assert.DoesNotContain(player.Sent.Skip(first), IsSaleSuccessFrame);
            }

            Assert.NotNull(sale);
            await sale.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotNull(latest);
            AssertSaved(uid, SaleResult(latest, 1_200, 150));
            Assert.Equal(1_200, Current(owner, player).Character.Money);
            Assert.Single(player.Sent.Skip(first), IsRemoval);
            Assert.Equal(2, MoneyUpdateCount(player, first));
            await AssertReplayCannotPay(owner, player);
        }
        finally
        {
            // A broken nested dispatch/shared lease must fail the test rather
            // than leave its real queued operation alive after the fixture.
            if (sale is { IsCompleted: false })
            {
                player.Lifetime.Cancel();
                try { await sale.WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (OperationCanceledException) { }
            }
        }
    }

    [Fact]
    public async Task FirstRemovalCallbackCanImmediatelyAcquireExclusiveWorldLease()
    {
        const uint uid = 1_299_091;
        Seed(uid);
        var owner = NewOwner();
        var barrier = ActualOperationBarrier(owner);
        var armed = false;
        var observed = 0;
        var immediatelyAvailable = false;
        Task cleanup = Task.CompletedTask;
        var player = await Login(owner, uid, bytes =>
        {
            if (!armed || !IsRemoval(bytes)) return;
            observed++;
            using var cancellation = new CancellationTokenSource();
            var acquisition = barrier.EnterExclusiveAsync(cancellation.Token);
            immediatelyAvailable = acquisition.IsCompletedSuccessfully;
            if (immediatelyAvailable) acquisition.Result.Dispose();
            else
            {
                cancellation.Cancel();
                cleanup = DisposeCancelledProbe(acquisition);
            }
            Assert.True(immediatelyAvailable, "No Sell or outer dispatch lease may span the first socket send.");
        });
        var expected = SaleResult(Current(owner, player), 1_300, 50);
        armed = true;

        await Sell(owner, player);
        await cleanup.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, observed);
        // Handler diagnostics catch send-delegate exceptions, so retain this
        // assertion outside the callback as well.
        Assert.True(immediatelyAvailable, owner.Log.ToString());
        AssertSaved(uid, expected);
        await AssertReplayCannotPay(owner, player);
    }

    [Fact]
    public async Task SaleRetainsNativeInt32MultiplyBeforeDivisionInsteadOfWideningPriceArithmetic()
    {
        const uint uid = 1_299_101;
        Seed(uid, durability: 5, maximumDurability: 10, money: 200_000_000);
        var owner = NewOwner(goldWorth: uint.MaxValue);
        var field = typeof(WorldServerFrameHandler).GetField("_worldData", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var catalog = Assert.IsType<WorldDataCatalog>(field.GetValue(owner.Handler));
        Assert.Equal(uint.MaxValue, Assert.Single(catalog.ItemsById[(int)ItemId]).GoldWorth);
        var player = await Login(owner, uid);
        var delta = unchecked((int)(uint.MaxValue / 3) * 5) / 10;
        Assert.Equal(-143_165_576, delta);
        var expected = SaleResult(Current(owner, player), 200_000_000L + delta, 50);
        var first = player.Sent.Count;

        await Sell(owner, player);

        AssertSaved(uid, expected);
        Assert.Equal(56_834_424, Current(owner, player).Character.Money);
        Assert.Single(player.Sent.Skip(first), IsRemoval);
        Assert.Equal(2, MoneyUpdateCount(player, first));
        await AssertReplayCannotPay(owner, player);
    }

    [Fact]
    public async Task NoPayoutSaleCanonicalizesAbsentWarehouseMoneyWithoutMoneyNotifications()
    {
        const uint uid = 1_299_111;
        Seed(uid, durability: 0);
        File.WriteAllLines(UserPath(uid), File.ReadAllLines(UserPath(uid)).Where(line =>
            !line.StartsWith("WHMoney=", StringComparison.OrdinalIgnoreCase)));
        var owner = NewOwner();
        var player = await Login(owner, uid);
        Assert.False(Current(owner, player).Character.Attributes.ContainsKey("WHMoney"));
        var expected = SaleResult(Current(owner, player), 1_000, 0);
        var first = player.Sent.Count;

        await Sell(owner, player);

        AssertSaved(uid, expected);
        Assert.Equal(1_000, Current(owner, player).Character.Money);
        Assert.Equal("0", Current(owner, player).Character.Attributes["WHMoney"]);
        Assert.Equal("0", CharacterSnapshotParser.ParseText(File.ReadAllText(UserPath(uid))).Attributes["WHMoney"]);
        Assert.Equal(KeptUid, Assert.Single(Current(owner, player).PlayerItems.InventoryItems).Uid);
        Assert.Single(player.Sent.Skip(first), IsRemoval);
        Assert.Equal(0, MoneyUpdateCount(player, first));
        await AssertReplayCannotPay(owner, player);
    }

    private static WorldOperationBarrier ActualOperationBarrier(Owner owner)
    {
        var field = typeof(WorldServerFrameHandler).GetField("_worldOperations", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<WorldOperationBarrier>(field.GetValue(owner.Handler));
    }

    private static async Task DisposeCancelledProbe(ValueTask<IDisposable> acquisition)
    {
        try { (await acquisition).Dispose(); }
        catch (OperationCanceledException) { }
    }
}
