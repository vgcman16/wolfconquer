using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceStatusTests
{
    [WindowsGameAcceptanceFact]
    public async Task OwnerOnlyMissingOwnerAndIncompleteAfterStayPendingOrInvalid()
    {
        using var fixture = new EvidenceFixture(); var incomplete = fixture.Evidence("combat");
        File.Delete(Path.Combine(incomplete, "receipt.json"));
        var missing = fixture.Evidence("reconnect"); File.Delete(Path.Combine(missing, "owner.json"));
        var equip = fixture.Evidence("equip-unequip", equipped: true);
        var finalLeaf = Directory.GetFiles(equip, "resume-*-receipt.json").Single();
        var final = JsonNode.Parse(File.ReadAllBytes(finalLeaf))!; File.Delete(Path.Combine(equip, final["after"]!.GetValue<string>()));
        var result = await RunAsync(fixture.Parent);
        Assert.Equal(1, result.ExitCode); Assert.NotNull(result.Json); NoLivePass(result.Json);
        Assert.Equal("receiptMissing", Row(result.Json, "combat")["reason"]!.GetValue<string>());
        Assert.Equal("Invalid", Row(result.Json, "equip-unequip")["evidenceStatus"]!.GetValue<string>());
        Assert.Single(result.Json["diagnostics"]!.AsArray());
    }

    [WindowsGameAcceptanceFact]
    public async Task DuplicateKeysUnsupportedSchemaAndInspectorIdentityDriftAreInvalid()
    {
        using var fixture = new EvidenceFixture(); var duplicate = fixture.Evidence("combat");
        var text = File.ReadAllText(Path.Combine(duplicate, "receipt.json"));
        fixture.Raw(duplicate, "receipt.json", text.Insert(1, "\"accountId\":2749100,"));
        var schema = fixture.Evidence("reconnect"); var receipt = EvidenceFixture.Read(schema, "receipt.json");
        receipt["schemaVersion"] = 2; fixture.Write(schema, "receipt.json", receipt);
        var drift = fixture.Evidence("equip-unequip", equipped: true);
        var final = JsonNode.Parse(File.ReadAllBytes(Directory.GetFiles(drift, "resume-*-receipt.json").Single()))!;
        var afterLeaf = final["after"]!.GetValue<string>(); var after = EvidenceFixture.Read(drift, afterLeaf);
        after["report"]!["domain"] = Guid.NewGuid().ToString("D"); fixture.Write(drift, afterLeaf, after);
        var result = await RunAsync(fixture.Parent);
        Assert.Equal(1, result.ExitCode); Assert.NotNull(result.Json); NoLivePass(result.Json);
        Assert.Equal("duplicateJsonProperty", Row(result.Json, "combat")["reason"]!.GetValue<string>());
        Assert.Equal("receiptIdentityMismatch", Row(result.Json, "reconnect")["reason"]!.GetValue<string>());
        Assert.Equal("inspectorIdentityMismatch", Row(result.Json, "equip-unequip")["reason"]!.GetValue<string>());
    }

    [WindowsGameAcceptanceFact]
    public async Task FileFolderAndReceiptBoundsAndRelativePathsRefuseWithoutWrites()
    {
        using var fixture = new EvidenceFixture(); var root = fixture.Evidence("combat");
        fixture.Raw(root, "receipt.json", new string('x', 2 * 1024 * 1024 + 1));
        var large = await RunAsync(fixture.Parent);
        Assert.Equal(1, large.ExitCode); Assert.NotNull(large.Json);
        Assert.Equal("evidenceFileTooLarge", Row(large.Json, "combat")["reason"]!.GetValue<string>());
        fixture.Evidence("reconnect"); var before = fixture.Capture();
        var folders = await RunAsync(fixture.Parent, "-MaximumEvidenceFolders", "1");
        Assert.Equal(1, folders.ExitCode); Assert.NotNull(folders.Json); NoLivePass(folders.Json);
        var relative = await RunAsync("relative-evidence");
        Assert.Equal(1, relative.ExitCode); Assert.NotNull(relative.Json); NoLivePass(relative.Json);
        var unc = await RunAsync(@"\\server\share\evidence");
        Assert.Equal(1, unc.ExitCode); Assert.NotNull(unc.Json); NoLivePass(unc.Json);
        var fileParent = await RunAsync(Path.Combine(root, "before.json"));
        Assert.Equal(1, fileParent.ExitCode); Assert.NotNull(fileParent.Json); NoLivePass(fileParent.Json);
        fixture.Raw(root, EvidenceFixture.ResumeLeaf(), "{}"); before = fixture.Capture();
        var receipts = await RunAsync(fixture.Parent, "-MaximumReceiptsPerFolder", "1");
        Assert.Equal(1, receipts.ExitCode); Assert.NotNull(receipts.Json);
        Assert.Contains(receipts.Json["diagnostics"]!.AsArray(), issue => issue!["reason"]!.GetValue<string>() == "receiptCountLimit");
        fixture.AssertUnchanged(before);
    }

    [WindowsGameAcceptanceFact]
    public async Task FailedRunnerReceiptIsPendingAndCannotBePromotedBySourceCheckField()
    {
        using var fixture = new EvidenceFixture(); var root = fixture.Evidence("updater");
        var receipt = EvidenceFixture.Read(root, "receipt.json"); receipt["sourceChecks"]!["status"] = "SourceChecksVerified";
        fixture.Write(root, "receipt.json", receipt);
        var source = await RunAsync(fixture.Parent);
        Assert.Equal(0, source.ExitCode); Assert.NotNull(source.Json); NoLivePass(source.Json);
        Assert.Equal("Pending", Row(source.Json, "updater")["evidenceStatus"]!.GetValue<string>());
        fixture.Write(root, "failure-" + Guid.NewGuid().ToString("N") + ".json", new JsonObject
        {
            ["schemaVersion"] = 1, ["scenario"] = "updater", ["status"] = "PrerequisiteMissingOrOutcomeUnproven",
            ["reason"] = "test failure", ["errorType"] = "IOException", ["visualAcceptance"] = "LiveClientVisualPending"
        });
        var failed = await RunAsync(fixture.Parent);
        Assert.Equal(0, failed.ExitCode); Assert.NotNull(failed.Json); NoLivePass(failed.Json);
        Assert.Equal("PrerequisiteMissingOrOutcomeUnproven", Row(failed.Json, "updater")["savedStateResult"]!.GetValue<string>());
    }
}
