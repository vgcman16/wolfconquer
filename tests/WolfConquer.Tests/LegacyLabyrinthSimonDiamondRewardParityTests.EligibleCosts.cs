using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyLabyrinthSimonDiamondRewardParityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ZeroStackRowsCannotSupplyTheSeventeenthDiamond(bool includeSixteenPositiveUnits)
    {
        var original = includeSixteenPositiveUnits
            ? Items(InventoryItem(10, CloudDiamondItemId, 16, seed: 24),
                InventoryItem(20, CloudDiamondItemId, 0, seed: 25))
            : Items(Enumerable.Range(0, 17).Select(index => InventoryItem(
                checked((uint)(10 + index)), CloudDiamondItemId, 0, seed: 26)).ToArray());
        await RunHarnessAsync(15, original, async harness =>
        {
            Assert.True(await harness.InvokeAsync());
            Assert.Same(harness.OriginalSession, harness.CurrentSession);
            Assert.Equal(0, harness.CommitCount);
            Assert.Equal(SimonNpcId, harness.CurrentSession.ActiveNpcId);
            Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
            AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
            AssertExactMissingDialog(harness.ClientSent, "CloudDiamond");
            AssertNoItemMutationPackets(harness.ClientSent);
        });
    }

    [Theory]
    [InlineData((byte)0, (ushort)0)]
    [InlineData((byte)2, (ushort)17)]
    [InlineData((byte)7, (ushort)17)]
    public async Task EmptyOrNoncanonicalBoundDiamondsCannotAuthorizeReward(byte bound, ushort stack)
    {
        var original = Items(InventoryItem(10, CloudDiamondItemId, stack, bound, seed: 20));
        await RunHarnessAsync(15, original, async harness =>
        {
            Assert.True(await harness.InvokeAsync());
            Assert.Same(harness.OriginalSession, harness.CurrentSession);
            Assert.Equal(0, harness.CommitCount);
            Assert.Equal(SimonNpcId, harness.CurrentSession.ActiveNpcId);
            Assert.Equal(harness.OriginalItemBytes, File.ReadAllBytes(harness.ItemPath));
            AssertItemsEqual(harness.OriginalSession.PlayerItems, harness.ReloadItems());
            AssertExactMissingDialog(harness.ClientSent, "CloudDiamond");
            AssertNoItemMutationPackets(harness.ClientSent);
        });
    }

    [Theory]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    [InlineData((byte)7)]
    public async Task SimonSkipsEmptyAndBoundRowsBeforeFirstEligibleDiamond(byte bound)
    {
        var original = Items(InventoryItem(10, CloudDiamondItemId, 17, bound, seed: 21),
            InventoryItem(15, CloudDiamondItemId, 0, seed: 22),
            InventoryItem(20, CloudDiamondItemId, 20, seed: 23));
        await RunHarnessAsync(15, original, async harness =>
        {
            Assert.True(await harness.InvokeAsync());
            var before = harness.OriginalSession.PlayerItems;
            var after = harness.CurrentSession.PlayerItems;
            foreach (var uid in new uint[] { 10, 15 })
                AssertInventoryItemEqual(Assert.Single(before.InventoryItems, item => item.Uid == uid),
                    Assert.Single(after.InventoryItems, item => item.Uid == uid));
            AssertInventoryItemEqual(Assert.Single(before.InventoryItems, item => item.Uid == 20).WithStackSize(3),
                Assert.Single(after.InventoryItems, item => item.Uid == 20));
            var update = AssertGameItem(harness.ClientSent[0]);
            Assert.Equal(20U, update.Uid);
            Assert.Equal((ushort)3, update.StackSize);
            Assert.Equal((byte)0, update.Bound);
            AssertSourceOrderedSuccessPackets(harness.ClientSent, 1, [AncestorBoxItemId]);
            AssertItemsEqual(after, harness.ReloadItems());
            Assert.True(harness.PersistenceMatchedAtCommit);
            Assert.True(harness.SessionCommittedBeforeFirstSend);
        });
    }

    private async Task RunHarnessAsync(byte optionId, PlayerItemSnapshot originalItems, Func<SimonHarness, Task> body)
    {
        var harness = CreateHarness(optionId, originalItems);
        Exception? primary = null;
        try { await body(harness); }
        catch (Exception exception) { primary = exception; }
        Exception? cleanup = null;
        try { harness.Dispose(); }
        catch (Exception exception) { cleanup = exception; }
        if (primary is not null && cleanup is not null)
            throw new AggregateException("Test body and fixture cleanup failed.", primary, cleanup);
        if (primary is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
        if (cleanup is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(cleanup).Throw();
    }

}
