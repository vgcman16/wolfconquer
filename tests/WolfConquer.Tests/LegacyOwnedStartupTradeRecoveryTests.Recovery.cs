using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStartupTradeRecoveryTests
{
    [Theory]
    [InlineData("missing")] [InlineData("equal-aliases")] [InlineData("alias-only")]
    public Task GenuineProducerPreservesOptionalAndEqualWarehouseCurrencyAliasesThroughOwnedRecovery(string warehouse) =>
        WithFixture(async f =>
        {
            await f.StageAsync(3);
            foreach (var uid in new[] { LegacyOwnedStartupTradeRecoveryFixture.LeftUid, LegacyOwnedStartupTradeRecoveryFixture.RightUid })
            {
                var before = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(f.Before[f.CharacterPath(uid)], out _));
                var after = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(f.After[f.CharacterPath(uid)], out _));
                Assert.True(LegacyWarehouseMoneyState.TryRead(before.Attributes, out var beforeValue));
                Assert.True(LegacyWarehouseMoneyState.TryRead(after.Attributes, out var afterValue));
                Assert.Equal(warehouse == "missing" ? 0L : 555L, beforeValue);
                Assert.Equal(beforeValue, afterValue);
                Assert.Equal(warehouse == "equal-aliases", before.Attributes.ContainsKey("WHMoney"));
                Assert.Equal(warehouse != "missing", before.Attributes.ContainsKey("WarehouseMoney"));
            }
            await f.CloseAsync(); f.Open(); f.AssertRecoveredImages();
            await f.CloseAsync();
            var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, warehouse: warehouse);

    [Theory]
    [InlineData("none", true)] [InlineData("none", false)]
    [InlineData("left-only", true)] [InlineData("left-only", false)]
    public Task ActualItemOnlyExchangeGiftAndOneSidedEscrowRecoverWithoutInventingAMissingClaim(
        string currency, bool reciprocalItems) => WithFixture(async f =>
        {
            await f.StageAsync(3);
            await f.CloseAsync();
            var runtime = f.Open(); f.AssertRecoveredImages();
            await f.AssertFreshFrameAsync(runtime);
            await f.CloseAsync();
            var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, currency: currency, reciprocalItems: reciprocalItems);

    [Theory]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)] [InlineData(4, true)]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)] [InlineData(4, false)]
    public Task GenuineTradeExchangeOrRefundFinishesEveryReplacementPrefixBeforeFreshAdmission(int boundary, bool complete) =>
        WithFixture(async f =>
        {
            await f.StageAsync(boundary);
            if (complete && boundary == 3)
            {
                // One transferred Items image and the other owner's BEFORE
                // temporarily hold the same physical UID. Only the captured
                // pair permits recovery; ordinary raw scans still reject it.
                Assert.False(LegacyItemUidRawCatalog.TryScan(f.Data.Items, f.Data.Mailbox, f.Data.Redeem,
                    f.Data.Claim, out _, out var reason));
                Assert.NotEmpty(reason);
            }
            await f.CloseAsync();
            f.AssertLeaseAvailable();
            var runtime = f.Open();
            f.AssertRecoveredImages();
            Assert.Throws<IOException>(() => new FileStream(f.Data.OwnerLease, FileMode.Open,
                FileAccess.ReadWrite, FileShare.None).Dispose());
            await f.AssertFreshFrameAsync(runtime);
            f.AssertRecoveredImages();
            await f.CloseAsync();
            f.AssertLeaseAvailable();
            var recovered = f.Data.Files();
            f.Open();
            f.AssertRecoveredImages();
            f.Data.AssertFiles(recovered);
        }, complete);

    [Theory]
    [InlineData(1, true)] [InlineData(2, true)] [InlineData(3, true)] [InlineData(4, true)]
    [InlineData(1, false)] [InlineData(2, false)] [InlineData(3, false)] [InlineData(4, false)]
    public Task GenuinePairStageFlushInterruptionRetainsTheActualFourTargetProducerImages(int ordinal, bool complete) =>
        WithFixture(async f =>
        {
            await f.StopAfterStageFlushedAsync(ordinal);
            var stages = f.OrderedTargets.Select(f.StagePath).ToArray();
            for (var index = 0; index < f.OrderedTargets.Count; index++)
            {
                var path = f.OrderedTargets[index];
                Assert.Equal(f.Before[path], File.ReadAllBytes(path));
                if (index < ordinal) Assert.Equal(f.After[path], File.ReadAllBytes(stages[index]));
                else Assert.False(File.Exists(stages[index]));
            }
            await f.CloseAsync();
            f.Open();
            f.AssertRecoveredImages();
            Assert.All(stages, path => Assert.False(File.Exists(path)));
            await f.CloseAsync();
            var recovered = f.Data.Files();
            f.Open(); f.AssertRecoveredImages(); f.Data.AssertFiles(recovered);
        }, complete);

    [Theory]
    [InlineData(0, "empty")] [InlineData(1, "first-byte")] [InlineData(2, "middle")] [InlineData(3, "last-byte-missing")]
    public Task ARealFlushedPairStageMayContainOnlyItsWrittenAfterImagePrefix(int ordinal, string prefix) =>
        WithFixture(async f =>
        {
            await f.StopAfterStageFlushedAsync(4);
            var path = f.StagePath(f.OrderedTargets[ordinal]);
            var actual = File.ReadAllBytes(path);
            var length = prefix switch
            {
                "empty" => 0, "first-byte" => 1, "middle" => actual.Length / 2,
                "last-byte-missing" => actual.Length - 1, _ => throw new ArgumentOutOfRangeException(nameof(prefix)),
            };
            File.WriteAllBytes(path, actual.AsSpan(0, length).ToArray());
            await f.CloseAsync();
            f.Open(); f.AssertRecoveredImages();
            Assert.False(File.Exists(path));
        });

    [Fact]
    public Task GenericRegistryOpenAndBootstrapCannotBorrowTheOwnedPairRecoveryCapability() => WithFixture(async f =>
    {
        await f.StageAsync(3);
        await f.CloseAsync();
        var before = f.Data.Files();
        var success = LegacyItemUidRegistry.TryOpen(f.Data.RegistryOptions(), f.Data.ExpectedDomain,
            out var opened, out var reason);
        if (opened is not null) f.Own(opened);
        Assert.False(success, reason);
        Assert.Null(opened); Assert.NotEmpty(reason);
        var bootstrapSuccess = LegacyItemUidRegistry.TryBootstrap(f.Data.RegistryOptions(), out var bootstrapped, out reason);
        if (bootstrapped is not null) f.Own(bootstrapped);
        Assert.False(bootstrapSuccess, reason);
        Assert.Null(bootstrapped); Assert.NotEmpty(reason);
        f.Data.AssertFiles(before); f.AssertLeaseAvailable();
        f.Open(); f.AssertRecoveredImages();
    });
}
