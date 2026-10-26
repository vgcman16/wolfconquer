using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("mixed")]
    [InlineData("missing-image")]
    [InlineData("foreign-domain")]
    [InlineData("unknown-property")]
    [InlineData("missing-sidecar-after")]
    public Task FreshRealPackageProducerRefusesMixedMissingOrForeignRetainedWorkWithoutNormalizingOrRewritingIt(string fault) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var initial = CapturePackageJoin(fixture, join);
            var intent = PrepareDebtIntent(fixture, join, initial);
            var recipe = CreatePackageJoinRecipe(fixture, join, initial);
            var producer = ActualDebtFrameOwner<LegacyMailboxPackageProducer>(join, "_mailboxPackages");
            var after = DebtAfterFiles(fixture, initial, intent);
            var changes = PackageJoinJournalChanges(fixture, initial.Files, after);
            Assert.True(producer.TryPrepareJointWork(recipe, intent.Allocation!, changes, out var work, out var reason), reason);
            var path = DebtWorkPath(fixture, join, intent.Request.OperationId);
            switch (fault)
            {
                case "mixed":
                    File.WriteAllBytes(fixture.CharacterPath, after[Path.GetRelativePath(fixture.Root, fixture.CharacterPath)]);
                    break;
                case "missing-image":
                    File.Delete(fixture.ItemPath);
                    break;
                case "foreign-domain":
                case "unknown-property":
                    var json = ReadHistoryWorkDocument(fixture, join, intent.Request.OperationId);
                    if (fault == "foreign-domain") json["Domain"] = Guid.NewGuid().ToString("N");
                    else json["Accept"] = true;
                    WriteHistoryPendingDocument(fixture, join, intent.Request.OperationId, json);
                    break;
                case "missing-sidecar-after":
                    Assert.False(work!.TryCommit(LegacyMailboxPackageInterruption.AfterJointCommit, out _, out var durable, out reason));
                    Assert.True(durable);
                    File.Delete(path);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            var files = fixture.PackageFiles(); // Explicit complete owned fault postimage, not a replacement persistence callback.
            var first = fixture.Sent.Count;
            await join.DisposeAsync();
            var recovery = new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null,
                fixture.PackageMailboxRoot, fixture.PackageGearRoot);
            Assert.True(recovery.TryEnsureInitialized(out reason), reason);
            Assert.True(LegacyItemUidRegistry.TryOpen(fixture.PackageRegistryOptions(), out var reopened, out reason), reason);
            using (var owner = Assert.IsType<LegacyItemUidRegistry>(reopened))
            {
                Assert.False(LegacyMailboxPackageProducer.TryCreate(owner, join.Domain, recovery,
                    fixture.Users, fixture.Items, fixture.PackageMailboxRoot, fixture.PackageGearRoot,
                    out var rejected, out reason));
                Assert.Null(rejected);
                Assert.StartsWith("mailboxClaimRecoveryRequired:", reason);
                AssertPackageJoinFiles(fixture, files);
                Assert.Equal(first, fixture.Sent.Count);
                Assert.True(owner.TryReadProducerIntent(join.Domain, intent.Request.OperationId, out var retained, out reason), reason);
                Assert.Equal(intent.Request, retained!.Request);
                Assert.Equal(intent.Allocation, retained.Allocation);
                Assert.Equal(intent.CopyPayload(), retained.CopyPayload());
            }
        });
}
