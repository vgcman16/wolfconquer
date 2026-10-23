using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyItemUidRegistryTests
{
    [Theory]
    [InlineData("floor")]
    [InlineData("evidence")]
    [InlineData("relative")]
    [InlineData("same-path")]
    public void UnconfiguredAuthorityCannotBeInferredFromExistingDataOrFilename(string kind)
    {
        using var fixture = new Fixture();
        fixture.SeedItems(11, Fixture.Item(500));
        var options = kind switch
        {
            "floor" => fixture.Options(null),
            "evidence" => fixture.Options() with { HistoricalEvidence = "" },
            "relative" => fixture.Options() with { ItemsRoot = "PlayersItems" },
            "same-path" => fixture.Options() with { MailboxRoot = fixture.Items },
            _ => throw new InvalidOperationException(),
        };
        Assert.False(LegacyItemUidRegistry.TryBootstrap(options, out var owner, out _));
        Assert.Null(owner);
        Assert.False(Directory.Exists(fixture.Registry));
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("duplicate")]
    [InlineData("version")]
    [InlineData("floor")]
    [InlineData("domain")]
    [InlineData("missing-state")]
    public void CorruptOrMissingLedgerRefusesAndNeverResetsToDataMaximum(string kind)
    {
        using var fixture = new Fixture();
        using (var owner = fixture.Bootstrap()) Assert.Equal(new uint[] { 1001 }, Fixture.Reserve(owner, 1));
        var json = JsonNode.Parse(File.ReadAllText(fixture.State))!.AsObject();
        switch (kind)
        {
            case "unknown": json["Unexpected"] = 1; break;
            case "version": json["Version"] = 2; break;
            case "floor": json["HighWater"] = 999; break;
            case "domain": json["Domain"] = Guid.Empty.ToString("N"); break;
        }
        var text = json.ToJsonString();
        if (kind == "duplicate") text = text.Replace("\"Version\":1", "\"Version\":1,\"Version\":1", StringComparison.Ordinal);
        if (kind == "missing-state") File.Delete(fixture.State);
        else File.WriteAllText(fixture.State, text);
        var before = File.Exists(fixture.State) ? File.ReadAllBytes(fixture.State) : null;
        Assert.False(LegacyItemUidRegistry.TryOpen(fixture.Options(), out var reopened, out var reason));
        Assert.Null(reopened);
        Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
        Assert.False(LegacyItemUidRegistry.TryBootstrap(fixture.Options(), out _, out _));
        if (before is null) Assert.False(File.Exists(fixture.State));
        else Assert.Equal(before, File.ReadAllBytes(fixture.State));
    }

    [Fact]
    public void NewUnregisteredPhysicalUidAboveLedgerBlocksRestartInsteadOfAutoTrustingIt()
    {
        using var fixture = new Fixture();
        using (var owner = fixture.Bootstrap()) { }
        var state = File.ReadAllBytes(fixture.State);
        fixture.SeedItems(11, Fixture.Item(2000));
        Assert.False(LegacyItemUidRegistry.TryOpen(fixture.Options(), out _, out var reason));
        Assert.Contains("unregisteredDataUid", reason);
        Assert.Equal(state, File.ReadAllBytes(fixture.State));
    }

    [Fact]
    public void ConfiguredHistoricalFloorAndProvenanceCannotBeChangedSilentlyOnRestart()
    {
        using var fixture = new Fixture();
        using (var owner = fixture.Bootstrap()) { }
        var state = File.ReadAllBytes(fixture.State);
        Assert.False(LegacyItemUidRegistry.TryOpen(fixture.Options(999), out _, out _));
        Assert.False(LegacyItemUidRegistry.TryOpen(fixture.Options() with { HistoricalEvidence = "other lineage" }, out _, out _));
        Assert.Equal(state, File.ReadAllBytes(fixture.State));
    }

    [Fact]
    public void GenuinePendingSnapshotJournalPreventsUidBootstrapUntilItsOwnRecoveryOwnerRuns()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.SnapshotJournal);
        var pending = Path.Combine(fixture.SnapshotJournal, "pending.json");
        File.WriteAllText(pending, "unproven owned test journal");
        var bytes = File.ReadAllBytes(pending);
        Assert.False(LegacyItemUidRegistry.TryBootstrap(fixture.Options(), out _, out _));
        Assert.Equal(bytes, File.ReadAllBytes(pending));
        Assert.False(File.Exists(fixture.State));
    }

    [Fact]
    public void RealPendingPathCollisionFencesOwnerAndRetainsExactOldState()
    {
        using var fixture = new Fixture();
        using (var owner = fixture.Bootstrap())
        {
            var state = File.ReadAllBytes(fixture.State);
            Directory.CreateDirectory(fixture.Pending);
            try
            {
                Assert.False(owner.TryReserve(2, out var range, out var reason));
                Assert.Null(range);
                Assert.StartsWith(LegacyItemUidRegistry.RecoveryRequired, reason);
                Assert.Equal(state, File.ReadAllBytes(fixture.State));
                Assert.False(owner.TryReserve(1, out _, out _));
            }
            finally { Directory.Delete(fixture.Pending, false); }
        }
        using var restarted = fixture.Open();
        Assert.Equal(new uint[] { 1001 }, Fixture.Reserve(restarted, 1));
    }

    [Theory]
    [InlineData("generation")]
    [InlineData("backwards")]
    [InlineData("domain")]
    [InlineData("foreign-root")]
    public void UnprovenPendingChainNeverAuthorizesReservationOrDestroysEvidence(string kind)
    {
        using var fixture = new Fixture();
        using (var owner = fixture.Bootstrap())
            Assert.False(owner.TryReserve(3, LegacyItemUidInterruption.AfterPendingFlush, out _, out _));
        var json = JsonNode.Parse(File.ReadAllText(fixture.Pending))!.AsObject();
        switch (kind)
        {
            case "generation": json["Generation"] = 99; break;
            case "backwards": json["HighWater"] = 1000; break;
            case "domain": json["Domain"] = Guid.NewGuid().ToString("N"); break;
            case "foreign-root": json["Items"] = fixture.Mail; break;
        }
        File.WriteAllText(fixture.Pending, json.ToJsonString());
        var state = File.ReadAllBytes(fixture.State);
        var pending = File.ReadAllBytes(fixture.Pending);
        Assert.False(LegacyItemUidRegistry.TryOpen(fixture.Options(), out var ownerAfterFailure, out _));
        Assert.Null(ownerAfterFailure);
        Assert.Equal(state, File.ReadAllBytes(fixture.State));
        Assert.Equal(pending, File.ReadAllBytes(fixture.Pending));
    }
}
