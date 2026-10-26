using System.Runtime.ExceptionServices;
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
    [InlineData("payload-hash")]
    [InlineData("unknown-directory")]
    [InlineData("orphan-v1")]
    public Task FreshRealNoAllocationProducerRefusesMixedMissingOrUnboundClosedWorkWithoutRewritingIt(string fault) =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            var before = CapturePackageJoin(fixture, join);
            var expected = StackExpectedItems(before, StackCreditAll(before.Owner.PlayerItems.InventoryItems));
            var prepared = PrepareStackNoAllocationWork(fixture, join, before, expected);
            StackPendingFiles(fixture, join, before, prepared);
            var path = DebtWorkPath(fixture, join, prepared.Recipe.OperationId);
            switch (fault)
            {
                case "mixed":
                    File.WriteAllBytes(fixture.CharacterPath, prepared.After[Path.GetRelativePath(fixture.Root, fixture.CharacterPath)]);
                    break;
                case "missing-image": File.Delete(fixture.ItemPath); break;
                case "unknown-directory": Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(path)!, "unexpected")); break;
                case "foreign-domain":
                case "unknown-property":
                case "payload-hash":
                case "orphan-v1":
                    var json = ReadHistoryWorkDocument(fixture, join, prepared.Recipe.OperationId);
                    if (fault == "foreign-domain") json["Domain"] = Guid.NewGuid().ToString("N");
                    else if (fault == "unknown-property") json["Accept"] = true;
                    else if (fault == "orphan-v1")
                    {
                        // Closed-looking v1 metadata deliberately claims a range
                        // with NO actual UID intent/receipt. It must never grant
                        // authority merely because discovery sees a known version.
                        json["Version"] = 1;
                        json["First"] = PackageHistoricalFloor + 1;
                        json["Count"] = 6;
                        Assert.True(json.Remove("AllocationKind"));
                        Assert.True(json.Remove("PayloadBase64"));
                    }
                    else
                    {
                        var payload = Convert.FromBase64String(json["PayloadBase64"]!.GetValue<string>());
                        payload[^1] ^= 1;
                        json["PayloadBase64"] = Convert.ToBase64String(payload);
                    }
                    WriteHistoryPendingDocument(fixture, join, prepared.Recipe.OperationId, json);
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            var files = fixture.PackageFiles();
            var directories = Directory.GetDirectories(fixture.Root, "*", SearchOption.AllDirectories)
                .Select(directory => Path.GetRelativePath(fixture.Root, directory)).Order().ToArray();
            var first = fixture.Sent.Count;
            await join.DisposeAsync();
            var recovery = new LegacyTradeRecoveryState(fixture.Users, fixture.Items, null,
                fixture.PackageMailboxRoot, fixture.PackageGearRoot);
            Assert.True(recovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(LegacyItemUidRegistry.TryOpen(fixture.PackageRegistryOptions(), out var reopened, out reason), reason);
            var registry = Assert.IsType<LegacyItemUidRegistry>(reopened);
            var errors = new List<Exception>();
            try
            {
                Assert.False(LegacyMailboxPackageProducer.TryCreate(registry, join.Domain, recovery,
                    fixture.Users, fixture.Items, fixture.PackageMailboxRoot, fixture.PackageGearRoot, out var rejected, out reason));
                Assert.Null(rejected);
                Assert.StartsWith("mailboxClaimRecoveryRequired:", reason);
                AssertPackageJoinFiles(fixture, files);
                Assert.Equal(directories, Directory.GetDirectories(fixture.Root, "*", SearchOption.AllDirectories)
                    .Select(directory => Path.GetRelativePath(fixture.Root, directory)).Order());
                Assert.Equal(first, fixture.Sent.Count);
                Assert.True(registry.TryReadProducerIntents(join.Domain, out var intents, out reason), reason);
                Assert.Empty(intents);
            }
            catch (Exception error) { errors.Add(error); }
            try { registry.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException("No-allocation refusal and real UID owner cleanup failed.", errors);
        });
}
