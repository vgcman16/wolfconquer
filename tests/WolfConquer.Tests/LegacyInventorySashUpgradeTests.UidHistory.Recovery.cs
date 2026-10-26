using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData((int)LegacyItemUidArchiveInterruption.AfterArchivePendingFlush)]
    [InlineData((int)LegacyItemUidArchiveInterruption.AfterArchivePublish)]
    [InlineData((int)LegacyItemUidArchiveInterruption.AfterPendingFlush)]
    [InlineData((int)LegacyItemUidArchiveInterruption.AfterStateReplace)]
    public Task RealInterruptedCompletedProducerRetirementReopensTheSameImmutableReceiptWithoutNewCredit(
        int cut) => RunPackageJoinAsync(async (fixture, join) =>
        {
            var completed = await CompleteUidHistoryPackageAsync(fixture, join, PackageJoinRequest(true));
            var before = join.Current;
            var first = fixture.Sent.Count;
            var images = new[] { fixture.CharacterPath, fixture.ItemPath, fixture.AtomicMailPath, fixture.PackageGearPath }
                .ToDictionary(path => path, File.ReadAllBytes);
            Assert.False(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, completed.Intent.Request.OperationId,
                completed.Proof, (LegacyItemUidArchiveInterruption)cut, out var reason));
            Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
            Assert.False(join.Registry.TryReserve(1, out var token, out _));
            Assert.Null(token);
            Assert.False(join.Registry.TryFindProducerIntent(join.Domain, completed.Intent.Request.OperationId, out _, out _, out _));
            Assert.Equal(first, fixture.Sent.Count);
            foreach (var pair in images) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            await join.RestartAsync();
            AssertDebtPinnedResourcesEqual(before, join.Current);
            foreach (var pair in images) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var active, out reason), reason);
            Assert.Empty(active);
            Assert.True(join.Registry.TryReadProducerIntent(join.Domain, completed.Intent.Request.OperationId, out var archived, out reason), reason);
            AssertUidHistorySnapshot(completed.Intent, archived!);
            var files = fixture.PackageFiles();
            first = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(true));
            AssertPackageJoinFiles(fixture, files);
            AssertPackageJoinReplay(fixture, true, first);
            AssertDebtPinnedResourcesEqual(before, join.Current);
        });
}
