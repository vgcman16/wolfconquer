using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupVendorRecoveryTests
{
    [Theory]
    [InlineData(0, false, false)] [InlineData(1, false, false)] [InlineData(2, false, false)] [InlineData(3, false, false)] [InlineData(4, false, false)]
    [InlineData(0, true, false)] [InlineData(1, true, false)] [InlineData(2, true, false)] [InlineData(3, true, false)] [InlineData(4, true, false)]
    [InlineData(0, false, true)] [InlineData(1, false, true)] [InlineData(2, false, true)] [InlineData(3, false, true)] [InlineData(4, false, true)]
    [InlineData(0, true, true)] [InlineData(1, true, true)] [InlineData(2, true, true)] [InlineData(3, true, true)] [InlineData(4, true, true)]
    public Task ActualMoneyCpAndZeroPricePurchaseIntentsRecoverEveryReplacementPrefixBeforeAdmission(
        int boundary, bool cp, bool zero) => WithFixture(async f =>
        {
            await f.StageAsync(boundary);
            if (boundary == 3)
            {
                Assert.False(LegacyItemUidRawCatalog.TryScan(f.Data.Items, f.Data.Mailbox, f.Data.Redeem,
                    f.Data.Claim, out _, out var reason));
                Assert.NotEmpty(reason); // Actual delivered closure remains in the seller's physical BEFORE.
            }
            await f.CloseAsync(); f.AssertLeaseAvailable();
            var runtime = f.Open(); f.AssertRecoveredImages();
            Assert.Throws<IOException>(() => new FileStream(f.Data.OwnerLease, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None).Dispose());
            await f.AssertFreshFrameAsync(runtime); f.AssertRecoveredImages(); await f.AssertReplayRefusedAsync();
            await f.CloseAsync(); f.AssertLeaseAvailable();
            var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, cp, zero ? 0 : null);

    [Theory]
    [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)] [InlineData(4, false)]
    [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)] [InlineData(4, true)]
    public Task ActualVendorStageFlushesRetainTheRealFourTargetIntentUntilOwnedRecovery(int ordinal, bool cp) =>
        WithFixture(async f =>
        {
            await f.StopAfterStageFlushedAsync(ordinal);
            var stages = f.OrderedTargets.Select(f.StagePath).ToArray();
            for (var index = 0; index < f.OrderedTargets.Count; index++)
            {
                var target = f.OrderedTargets[index];
                Assert.Equal(f.Before[target], File.ReadAllBytes(target));
                if (index < ordinal) Assert.Equal(f.After[target], File.ReadAllBytes(stages[index]));
                else Assert.False(File.Exists(stages[index]));
            }
            await f.CloseAsync(); f.Open(); f.AssertRecoveredImages();
            Assert.All(stages, path => Assert.False(File.Exists(path)));
            await f.CloseAsync(); var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, cp);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualZeroPriceFlushCompleteStagesRecoverWithoutInferringTheHistoricalCurrency(bool cp) =>
        WithFixture(async f =>
        {
            await f.StopAfterStageFlushedAsync(4);
            await f.CloseAsync(); f.Open(); f.AssertRecoveredImages();
            await f.CloseAsync(); var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, cp, amount: 0);

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 1)] [InlineData(2, 2)] [InlineData(3, 3)]
    public Task ARealVendorFlushedStageMayRetainOnlyItsWrittenAfterImagePrefix(int ordinal, int prefix) =>
        WithFixture(async f =>
        {
            await f.StopAfterStageFlushedAsync(4);
            var path = f.StagePath(f.OrderedTargets[ordinal]); var bytes = File.ReadAllBytes(path);
            var length = prefix switch { 0 => 0, 1 => 1, 2 => bytes.Length / 2, 3 => bytes.Length - 1,
                _ => throw new ArgumentOutOfRangeException(nameof(prefix)) };
            File.WriteAllBytes(path, bytes.AsSpan(0, length).ToArray());
            await f.CloseAsync(); f.Open(); f.AssertRecoveredImages(); Assert.False(File.Exists(path));
        });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualThirtyNineRootBuyerMayReceiveItsFortiethCompleteClosure(bool cp) => WithFixture(async f =>
    {
        await f.StageAsync(3); await f.CloseAsync();
        f.Open(); f.AssertRecoveredImages();
        Assert.Equal(40, new PlayerItemSnapshotLoader(f.Data.Items)
            .LoadByCharacterUid(LegacyOwnedStartupVendorRecoveryFixture.BuyerUid).InventoryItems.Count);
    }, cp, buyerCount: 39);

    [Fact]
    public Task GenericRegistryOpenAndBootstrapCannotUseVendorPendingCustody() => WithFixture(async f =>
    {
        await f.StageAsync(3); await f.CloseAsync(); var files = f.Data.Files();
        var opened = LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain, out var registry, out var reason);
        if (registry is not null) f.Own(registry);
        Assert.False(opened, reason); Assert.Null(registry); Assert.NotEmpty(reason);
        var bootstrapped = LegacyItemUidRegistry.TryBootstrap(f.Data.RegistryOptions(), out var bootstrap, out reason);
        if (bootstrap is not null) f.Own(bootstrap);
        Assert.False(bootstrapped, reason); Assert.Null(bootstrap); Assert.NotEmpty(reason);
        f.Data.AssertFiles(files); f.AssertLeaseAvailable();
    });
}
