using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("missing")] [InlineData("null")] [InlineData("number")] [InlineData("non-object")]
    public Task FreshPackageProducerRefusesMissingOrWronglyTypedPendingOperationWithoutPublishingAnOwner(string fault) =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var prepared = PrepareStackNoAllocationWork(fixture, join, before, expected);
            var index = ReadHistoryIndex(fixture, join);
            var pending = index["Pending"]!.AsArray();
            var row = Assert.Single(pending)!.AsObject();
            Assert.Equal(prepared.Recipe.OperationId.ToString("N"), row["Operation"]!.GetValue<string>());
            await join.DisposeAsync(); // Keep the fault targeted at fresh factory admission, not old-owner teardown.
            switch (fault)
            {
                case "missing": Assert.True(row.Remove("Operation")); break;
                case "null": row["Operation"] = null; break;
                case "number": row["Operation"] = 7; break;
                case "non-object": pending[0] = false; break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            File.WriteAllText(HistoryIndexPath(fixture, join), index.ToJsonString());
            var files = fixture.PackageFiles();
            var directories = Directory.GetDirectories(fixture.Root, "*", SearchOption.AllDirectories).Order().ToArray();
            var first = fixture.Sent.Count;
            var recovery = new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null, fixture.PackageMailboxRoot, fixture.PackageGearRoot);
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(LegacyItemUidRegistry.TryOpen(fixture.PackageRegistryOptions(), out var reopened, out reason), reason);
            using (var owner = Assert.IsType<LegacyItemUidRegistry>(reopened))
            {
                Assert.False(LegacyMailboxPackageProducer.TryCreate(owner, join.Domain, recovery, fixture.Users, fixture.Items,
                    fixture.PackageMailboxRoot, fixture.PackageGearRoot, out var rejected, out reason));
                Assert.Null(rejected);
                Assert.StartsWith("mailboxClaimRecoveryRequired:", reason);
                AssertPackageJoinFiles(fixture, files);
                Assert.Equal(directories, Directory.GetDirectories(fixture.Root, "*", SearchOption.AllDirectories).Order());
                Assert.Equal(first, fixture.Sent.Count);
                Assert.True(owner.TryReadActiveProducerIntents(join.Domain, out var intents, out reason), reason);
                Assert.Empty(intents);
            }
        });
}
