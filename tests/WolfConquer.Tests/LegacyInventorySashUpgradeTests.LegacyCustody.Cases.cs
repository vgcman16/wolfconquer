namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Fact]
    public Task MigratedPendingV1CustodyRemainsValidAfterRealReplayCompletionUidRetirementAndFreshReopen() =>
        RunStackPackageAsync(_ => { }, async (fixture, join) =>
        {
            var work = await SeedMigratedPendingAllocatingWorkAsync(fixture, join);
            await CompleteAndRetireMigratedWorkAsync(fixture, join, work);
            var previous = join.Current;
            var saved = LoadDebtRecoveryCandidate(fixture, previous);
            var files = fixture.PackageFiles();
            await join.RestartAsync();
            AssertPackageJoinFiles(fixture, files);
            AssertHistoryBusinessFiles(fixture, work.ExpectedBusiness);
            AssertStackColdBusinessState(join, previous, saved);
            Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out var reason), reason);
            Assert.True(join.ReadDebtProducer().TryRequireCharacterWritable(Fixture.Uid, out reason), reason);
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var active, out reason), reason);
            Assert.Empty(active);
            Assert.True(join.Registry.TryReadProducerIntent(join.Domain, work.Operation, out var archived, out reason), reason);
            AssertUidHistorySnapshot(work.Intent, archived!);
            Assert.Equal(work.OriginalBytes, File.ReadAllBytes(Path.Combine(HistoryRoot(fixture, join), "legacy", work.Operation.ToString("N") + ".json")));
            var original = System.Text.Json.Nodes.JsonNode.Parse(work.OriginalBytes)!;
            Assert.False(original["Completed"]!.GetValue<bool>());
            Assert.True(Assert.Single(AssertHistoryChain(fixture, join, 1, 0)).Value["Completed"]!.GetValue<bool>());
            var first = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(false));
            AssertPackageJoinFiles(fixture, files);
            AssertPackageJoinReplay(fixture, false, first);
        });
}
