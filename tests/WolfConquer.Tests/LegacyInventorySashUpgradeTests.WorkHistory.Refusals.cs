using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("missing-head")] [InlineData("missing-lineage")] [InlineData("missing-both")]
    [InlineData("unknown-completed")] [InlineData("empty-shard")] [InlineData("head-hash")]
    [InlineData("truncated-head")] [InlineData("unknown-index-property")] [InlineData("pending-bound")]
    [InlineData("reset-anchor")] [InlineData("foreign-root")] [InlineData("case-alias")]
    public Task FreshHistoryOwnerRefusesMissingChangedUnboundOrUnknownMetadataWithoutDiscardingIt(string fault) =>
        RunStackPackageAsync(fixture => fixture.SetStackInventory(StackAllCompatible()), async (fixture, join) =>
        {
            await join.FrameAsync(PackageJoinRequest(false));
            var records = AssertHistoryChain(fixture, join, 1, 0);
            var operation = Assert.Single(records).Key;
            var path = HistoryCompletedPath(fixture, join, operation);
            var root = HistoryRoot(fixture, join);
            await join.DisposeAsync();
            var index = ReadHistoryIndex(fixture, join);
            switch (fault)
            {
                case "missing-head": File.Delete(path); break;
                case "missing-lineage":
                    File.Delete(Assert.Single(Directory.GetFiles(Path.Combine(root, "lineages"), "*", SearchOption.AllDirectories))); break;
                case "missing-both":
                    File.Delete(path);
                    File.Delete(Assert.Single(Directory.GetFiles(Path.Combine(root, "lineages"), "*", SearchOption.AllDirectories))); break;
                case "unknown-completed":
                    var unknown = Guid.NewGuid().ToString("N");
                    var target = HistoryCompletedPath(fixture, join, unknown);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.WriteAllBytes(target, File.ReadAllBytes(path)); break;
                case "empty-shard":
                    var empty = Enumerable.Range(0, 256).Select(value => value.ToString("x2"))
                        .First(name => !Directory.Exists(Path.Combine(root, "completed", name)));
                    Directory.CreateDirectory(Path.Combine(root, "completed", empty)); break;
                case "head-hash": index["CompletedHeadSha256"] = new string('a', 64); break;
                case "truncated-head": File.WriteAllText(path, "{"); break;
                case "unknown-index-property": index["Accept"] = true; break;
                case "pending-bound":
                    var pending = new JsonArray();
                    for (var count = 0; count < 257; count++)
                    {
                        var row = records[operation].DeepClone().AsObject();
                        row["Completed"] = false;
                        pending.Add(row);
                    }
                    index["Pending"] = pending; break;
                case "reset-anchor":
                    index["CompletedHead"] = Guid.Empty.ToString("N");
                    index["CompletedHeadSha256"] = new string('0', 64);
                    index["CompletedCount"] = 0UL; break;
                case "foreign-root":
                    var envelope = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
                    envelope["Work"]!["Users"] = Path.Combine(fixture.Root, "unowned-users");
                    File.WriteAllText(path, envelope.ToJsonString()); break;
                case "case-alias":
                    var temporary = path + ".owned-rename";
                    File.Move(path, temporary);
                    File.Move(temporary, Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path).ToUpperInvariant())); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            if (fault is "head-hash" or "unknown-index-property" or "pending-bound" or "reset-anchor")
                File.WriteAllText(HistoryIndexPath(fixture, join), index.ToJsonString());
            var files = fixture.PackageFiles();
            var directories = Directory.GetDirectories(fixture.Root, "*", SearchOption.AllDirectories).Order().ToArray();
            var sends = fixture.Sent.Count;
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
                Assert.Equal(sends, fixture.Sent.Count);
                Assert.True(owner.TryReadProducerIntents(join.Domain, out var intents, out reason), reason);
                Assert.Empty(intents);
            }
        });
}
