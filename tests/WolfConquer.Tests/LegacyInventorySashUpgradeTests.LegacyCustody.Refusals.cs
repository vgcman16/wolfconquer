using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("changed-counterpart")] [InlineData("missing-counterpart")] [InlineData("pending-archived-identity")]
    public Task RetainedLegacyCustodyCannotApproveAMissingChangedOrRetiredPendingCounterpart(string fault) =>
        RunStackPackageAsync(_ => { }, async (fixture, join) =>
        {
            var work = await SeedMigratedPendingAllocatingWorkAsync(fixture, join);
            await CompleteAndRetireMigratedWorkAsync(fixture, join, work);
            await join.DisposeAsync();
            var path = HistoryCompletedPath(fixture, join, work.Operation.ToString("N"));
            var index = ReadHistoryIndex(fixture, join);
            switch (fault)
            {
                case "missing-counterpart": File.Delete(path); break;
                case "changed-counterpart":
                    var envelope = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
                    var body = envelope["Work"]!.AsObject();
                    Assert.NotEqual(new string('a', 64), body["After"]![1]!.GetValue<string>());
                    body["After"]!.AsArray()[1] = new string('a', 64);
                    File.WriteAllText(path, envelope.ToJsonString());
                    index["CompletedHeadSha256"] = HistoryDigest(File.ReadAllBytes(path));
                    File.WriteAllText(HistoryIndexPath(fixture, join), index.ToJsonString());
                    // Rebind the container hashes to the malformed counterpart,
                    // leaving the exact custody bytes/marker and real UID recipe
                    // unchanged. Custody-to-current equality must still refuse.
                    var record = JsonSerializer.Deserialize<LegacyMailboxPackageWorkStore.Record>(body.ToJsonString())!;
                    var canonical = JsonSerializer.SerializeToUtf8Bytes(record,
                        new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull });
                    var lineagePath = Assert.Single(Directory.GetFiles(Path.Combine(HistoryRoot(fixture, join), "lineages"), "*", SearchOption.AllDirectories));
                    var lineage = JsonNode.Parse(File.ReadAllBytes(lineagePath))!.AsObject();
                    lineage["RecordSha256"] = HistoryDigest(canonical);
                    File.WriteAllText(lineagePath, lineage.ToJsonString());
                    break;
                case "pending-archived-identity":
                    var retained = Path.Combine(fixture.Root, "owned-retained-current-custody-counterpart");
                    Assert.StartsWith(Path.GetFullPath(fixture.Root) + Path.DirectorySeparatorChar, Path.GetFullPath(HistoryRoot(fixture, join)));
                    Assert.StartsWith(Path.GetFullPath(fixture.Root) + Path.DirectorySeparatorChar, Path.GetFullPath(retained));
                    Directory.CreateDirectory(retained);
                    Directory.Move(Path.Combine(HistoryRoot(fixture, join), "completed"), Path.Combine(retained, "completed"));
                    Directory.Move(Path.Combine(HistoryRoot(fixture, join), "lineages"), Path.Combine(retained, "lineages"));
                    index["CompletedHead"] = Guid.Empty.ToString("N");
                    index["CompletedHeadSha256"] = new string('0', 64);
                    index["CompletedCount"] = 0UL;
                    index["Pending"] = new JsonArray(JsonNode.Parse(work.OriginalBytes));
                    File.WriteAllText(HistoryIndexPath(fixture, join), index.ToJsonString());
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
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
                Assert.True(owner.TryReadProducerIntent(join.Domain, work.Operation, out var archived, out reason), reason);
                AssertUidHistorySnapshot(work.Intent, archived!);
            }
        });
}
