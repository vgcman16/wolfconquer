using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(1, 1)] [InlineData(1, 2)] [InlineData(1, 3)] [InlineData(1, 4)] [InlineData(1, 5)]
    [InlineData(2, 1)] [InlineData(2, 2)] [InlineData(2, 3)] [InlineData(2, 4)] [InlineData(2, 5)]
    public Task RealCompletedV1AndPendingV2LegacyLayoutsMigrateAcrossEveryTypedCutWithoutDeletingOriginalBytes(int version, int cut) =>
        RunStackPackageAsync(fixture => { if (version == 2) fixture.SetStackInventory(StackAllCompatible()); }, async (fixture, join) =>
        {
            JsonObject body;
            if (version == 1)
            {
                await join.FrameAsync(PackageJoinRequest(false));
                body = Assert.Single(AssertHistoryChain(fixture, join, 1, 0)).Value.DeepClone().AsObject();
            }
            else
            {
                var before = CapturePackageJoin(fixture, join);
                var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
                var work = PrepareStackNoAllocationWork(fixture, join, before, expected);
                body = ReadHistoryWorkDocument(fixture, join, work.Recipe.OperationId);
            }
            Assert.Equal(version, body["Version"]!.GetValue<int>());
            var operation = body["Operation"]!.GetValue<string>();
            var bytes = System.Text.Encoding.UTF8.GetBytes(body.ToJsonString());
            var domain = join.Domain;
            var root = HistoryRoot(fixture, join);
            await join.DisposeAsync();
            var retained = Path.Combine(fixture.Root, "owned-original-history-layout");
            Assert.StartsWith(Path.GetFullPath(fixture.Root) + Path.DirectorySeparatorChar, Path.GetFullPath(root));
            Assert.StartsWith(Path.GetFullPath(fixture.Root) + Path.DirectorySeparatorChar, Path.GetFullPath(retained));
            Directory.Move(root, retained); // Owned fixture only; retain the entire new-format source tree.
            Directory.CreateDirectory(root);
            var flat = Path.Combine(root, operation + ".json");
            File.WriteAllBytes(flat, bytes); // Exact valid old v1/v2 body from a genuine operation.
            var uidBefore = File.ReadAllBytes(fixture.PackageUidState);
            var business = fixture.PackageFiles();
            var recovery = new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null, fixture.PackageMailboxRoot, fixture.PackageGearRoot);
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(LegacyItemUidRegistry.TryOpen(fixture.PackageRegistryOptions(), out var reopened, out reason), reason);
            using (var owner = Assert.IsType<LegacyItemUidRegistry>(reopened))
            {
                Assert.False(LegacyMailboxPackageProducer.TryCreate(owner, domain, recovery, fixture.Users, fixture.Items,
                    fixture.PackageMailboxRoot, fixture.PackageGearRoot, (LegacyMailboxHistoryInterruption)cut, out var rejected, out reason));
                Assert.Null(rejected);
                Assert.StartsWith("mailboxClaimRecoveryRequired:", reason);
            }
            AssertHistoryBusinessFiles(fixture, business);
            Assert.Equal(uidBefore, File.ReadAllBytes(fixture.PackageUidState));
            await join.StartAsync(bootstrap: false);
            AssertHistoryBusinessFiles(fixture, business);
            Assert.Equal(uidBefore, File.ReadAllBytes(fixture.PackageUidState));
            Assert.False(File.Exists(flat));
            Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(root, "legacy", operation + ".json")));
            var marker = JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, ".layout-migration.json")))!.AsObject();
            Assert.True(marker["Completed"]!.GetValue<bool>());
            Assert.Equal(marker["Id"]!.GetValue<string>(), ReadHistoryIndex(fixture, join)["OriginMigration"]!.GetValue<string>());
            var saved = Assert.IsType<LegacyMailboxPackageWorkStore.Record>(new LegacyMailboxPackageWorkStore(join.ReadDebtProducer()).Read(Guid.ParseExact(operation, "N")));
            Assert.Equal(version, saved.Version);
            Assert.Equal(version == 1, saved.Completed);
            AssertHistoryChain(fixture, join, version == 1 ? 1 : 0, version == 2 ? 1 : 0);
            Assert.True(join.ReadDebtProducer().HasPendingAccountWork(fixture.Account, out var pending, out reason), reason);
            Assert.Equal(version == 2, pending);
            if (version == 2)
            {
                var files = fixture.PackageFiles();
                await join.FrameAsync(new LegacyMailboxModePacket(LegacyMailboxModeType.ClaimMoney, AtomicMailId, []).ToFrameBytes());
                AssertPackageJoinFiles(fixture, files);
                await join.FrameAsync(PackageJoinRequest(false));
                Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out reason), reason);
                AssertHistoryChain(fixture, join, 1, 0);
                Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(root, "legacy", operation + ".json")));
            }
        });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ExactKnownCompletionTailRecoversAfterArchivePublicationBeforeIndexReplacement(bool partialLineages) =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var prepared = PrepareStackNoAllocationWork(fixture, join, before, expected);
            var pendingIndex = File.ReadAllBytes(HistoryIndexPath(fixture, join));
            Assert.True(prepared.Work.TryCommit(out var proof, out var durable, out var reason), reason);
            Assert.True(durable);
            Assert.NotNull(proof);
            var committed = fixture.PackageFiles();
            await join.DisposeAsync();
            File.WriteAllBytes(HistoryIndexPath(fixture, join), pendingIndex); // Real observed pre-anchor image, no fabricated completion.
            if (partialLineages)
            {
                var path = Assert.Single(Directory.GetFiles(Path.Combine(HistoryRoot(fixture, join), "lineages"), "*", SearchOption.AllDirectories));
                File.Delete(path);
            }
            await join.StartAsync(bootstrap: false);
            AssertHistoryBusinessFiles(fixture, prepared.After);
            AssertHistoryChain(fixture, join, 1, 0);
            Assert.Equal(committed[Path.GetRelativePath(fixture.Root, fixture.PackageUidState)], File.ReadAllBytes(fixture.PackageUidState));
            Assert.True(join.ReadDebtProducer().TryRequireAccountWritable(fixture.Account, out reason), reason);
            var files = fixture.PackageFiles();
            await join.FrameAsync(PackageJoinRequest(false));
            AssertPackageJoinFiles(fixture, files);
        });
}
