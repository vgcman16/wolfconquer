using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DirectArchivedUidLookupRevalidatesItsRequestedImmutableFileBeforeReturningARange(bool missing) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var completed = await CompleteUidHistoryPackageAsync(fixture, join, PackageJoinRequest(true));
            Assert.True(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, completed.Intent.Request.OperationId,
                completed.Proof, out var reason), reason);
            var path = UidHistoryFile(fixture, completed.Intent.Request.OperationId);
            if (missing) File.Delete(path);
            else File.WriteAllText(path, "{");
            var files = fixture.PackageFiles();
            Assert.False(join.Registry.TryFindProducerIntent(join.Domain, completed.Intent.Request.OperationId,
                out var rejected, out var found, out reason));
            Assert.Null(rejected);
            Assert.False(found);
            Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
            Assert.False(join.Registry.TryReserve(1, out var token, out _));
            Assert.Null(token);
            AssertPackageJoinFiles(fixture, files);
        });

    [Fact]
    public Task CompletedUidRetirementRejectsAbsentForeignOrMissingCompletionProofWithoutDirtyOutputs() =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var completed = await CompleteUidHistoryPackageAsync(fixture, join, PackageJoinRequest(true));
            var files = fixture.PackageFiles();
            Assert.False(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, completed.Intent.Request.OperationId, null!, out _));
            Assert.False(join.Registry.TryArchiveCompletedProducerIntent(Guid.NewGuid(), completed.Intent.Request.OperationId, completed.Proof, out _));
            Assert.False(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, Guid.NewGuid(), completed.Proof, out _));
            AssertPackageJoinFiles(fixture, files);
            Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var active, out var reason), reason);
            AssertUidHistorySnapshot(completed.Intent, Assert.Single(active));
        });

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown-property")]
    [InlineData("unknown-file")]
    [InlineData("unknown-directory")]
    [InlineData("partial-stage")]
    [InlineData("unbound-copy")]
    [InlineData("range")]
    [InlineData("generation")]
    [InlineData("identity")]
    public Task FreshUidOwnerRefusesMissingMalformedUnknownOrOverlappingHistoryWithoutRepairingIt(string fault) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            var completed = await CompleteUidHistoryPackageAsync(fixture, join, PackageJoinRequest(true));
            Assert.True(join.Registry.TryArchiveCompletedProducerIntent(join.Domain, completed.Intent.Request.OperationId,
                completed.Proof, out var reason), reason);
            var archive = UidHistoryFile(fixture, completed.Intent.Request.OperationId);
            var bytes = File.ReadAllBytes(archive);
            await join.DisposeAsync();
            switch (fault)
            {
                case "missing": File.Delete(archive); break;
                case "unknown-property":
                    var unknown = JsonNode.Parse(bytes)!.AsObject();
                    unknown["Accept"] = true;
                    File.WriteAllText(archive, unknown.ToJsonString());
                    break;
                case "unknown-file": File.WriteAllText(Path.Combine(Path.GetDirectoryName(archive)!, "unknown.json"), "{}"); break;
                case "unknown-directory": Directory.CreateDirectory(Path.Combine(Path.GetDirectoryName(archive)!, "unexpected")); break;
                case "partial-stage": File.WriteAllText(Path.Combine(fixture.Items, ".managed-item-uids", "archive.pending.json"), "{"); break;
                case "unbound-copy": File.WriteAllBytes(UidHistoryFile(fixture, Guid.NewGuid()), bytes); break;
                case "range":
                case "generation":
                case "identity": SeedUidArchiveUnionFault(fixture, bytes, fault); break;
                default: throw new ArgumentOutOfRangeException(nameof(fault));
            }
            var files = fixture.PackageFiles();
            var directories = Directory.GetDirectories(fixture.Root, "*", SearchOption.AllDirectories).Order().ToArray();
            Assert.False(LegacyItemUidRegistry.TryOpen(fixture.PackageRegistryOptions(), out var rejected, out reason));
            Assert.Null(rejected);
            Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
            AssertPackageJoinFiles(fixture, files);
            Assert.Equal(directories, Directory.GetDirectories(fixture.Root, "*", SearchOption.AllDirectories).Order());
        });

    private static void SeedUidArchiveUnionFault(Fixture fixture, byte[] archived, string fault)
    {
        var history = JsonNode.Parse(archived)!.AsObject();
        var state = JsonNode.Parse(File.ReadAllBytes(fixture.PackageUidState))!.AsObject();
        var intent = history["Intent"]!.DeepClone().AsObject();
        var receipt = history["Allocation"]!.DeepClone().AsObject();
        if (fault != "identity")
        {
            var key = Guid.NewGuid().ToString("N");
            intent["OperationId"] = key; receipt["OperationId"] = key;
        }
        var generation = state["Generation"]!.GetValue<ulong>();
        intent["PreparedGeneration"] = fault == "generation"
            ? history["Intent"]!["PreparedGeneration"]!.GetValue<ulong>() : generation + 1;
        receipt["Generation"] = generation + 2;
        state["Generation"] = generation + 2;
        if (fault != "range")
        {
            var highWater = state["HighWater"]!.GetValue<uint>();
            receipt["First"] = highWater + 1;
            state["HighWater"] = highWater + checked((uint)receipt["Count"]!.GetValue<int>());
        }
        state["ProducerIntents"] = new JsonArray(intent);
        state["Operations"] = new JsonArray(receipt);
        File.WriteAllText(fixture.PackageUidState, state.ToJsonString());
    }
}
