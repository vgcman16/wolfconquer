using System.Text.Json;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private sealed record UidHistoryCompletion(LegacyItemUidProducerIntentSnapshot Intent,
        LegacyMailboxPackageCompletedWorkProof Proof);

    private static async Task<UidHistoryCompletion> CompleteUidHistoryPackageAsync(Fixture fixture, PackageJoin join, byte[] request)
    {
        await join.FrameAsync(request);
        Assert.True(join.Registry.TryReadActiveProducerIntents(join.Domain, out var active, out var reason), reason);
        var intent = active.OrderBy(row => row.PreparedGeneration).Last();
        Assert.NotNull(intent.Allocation);
        Assert.True(join.ReadDebtProducer().TryGetCompletedWorkProof(intent.Request.OperationId, out var proof, out reason), reason);
        Assert.NotNull(proof);
        Assert.True(proof.MatchesProducerIntent(join.Domain, intent));
        return new(intent, proof);
    }

    private static void AssertUidHistorySnapshot(LegacyItemUidProducerIntentSnapshot expected,
        LegacyItemUidProducerIntentSnapshot actual)
    {
        Assert.Equal(expected.DomainId, actual.DomainId);
        Assert.Equal(expected.Request, actual.Request);
        Assert.Equal(expected.PreparedGeneration, actual.PreparedGeneration);
        Assert.Equal(expected.Allocation, actual.Allocation);
        Assert.Equal(expected.CopyPayload(), actual.CopyPayload());
    }

    private static string UidHistoryFile(Fixture fixture, Guid operation) =>
        Path.Combine(fixture.Items, ".managed-item-uids", "history", operation.ToString("N") + ".json");

    private static void AssertUidHistoryOnlyChanges(Fixture fixture, Dictionary<string, byte[]> before, Guid operation)
    {
        var actual = fixture.PackageFiles();
        var state = Path.GetRelativePath(fixture.Root, fixture.PackageUidState);
        var archive = Path.GetRelativePath(fixture.Root, UidHistoryFile(fixture, operation));
        Assert.Equal(before.Keys.Append(archive).Order(), actual.Keys.Order());
        foreach (var pair in before.Where(pair => pair.Key != state)) Assert.Equal(pair.Value, actual[pair.Key]);
        var initial = JsonNode.Parse(before[state])!.AsObject();
        var current = JsonNode.Parse(actual[state])!.AsObject();
        Assert.Equal(4, current["Version"]!.GetValue<int>());
        Assert.Equal(initial["Generation"]!.GetValue<ulong>() + 1, current["Generation"]!.GetValue<ulong>());
        Assert.Equal(initial["HighWater"]!.GetValue<uint>(), current["HighWater"]!.GetValue<uint>());
        Assert.Equal(operation.ToString("N"), current["ArchiveHead"]!.GetValue<string>());
        Assert.Empty(current["ProducerIntents"]!.AsArray());
        Assert.Empty(current["Operations"]!.AsArray());
        using var document = JsonDocument.Parse(actual[archive]);
        Assert.Equal(new[] { "Version", "Domain", "OperationId", "PreviousOperation", "PreviousSha256",
            "BeforeGeneration", "AfterGeneration", "BeforeStateSha256", "Intent", "Allocation" }.Order(),
            document.RootElement.EnumerateObject().Select(row => row.Name).Order());
    }
}
