using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceStatusTests
{
    [WindowsGameAcceptanceFact]
    public async Task EmptyParentListsAllElevenPendingWithoutCreatingEvidence()
    {
        using var fixture = new EvidenceFixture(); var before = fixture.Capture();
        var result = await RunAsync(fixture.Parent);
        Assert.Equal(0, result.ExitCode); Assert.NotNull(result.Json); NoLivePass(result.Json);
        Assert.Equal(11, result.Json["rows"]!.AsArray().Count);
        Assert.All(result.Json["rows"]!.AsArray(), row => Assert.Equal("Pending", row!["evidenceStatus"]!.GetValue<string>()));
        fixture.AssertUnchanged(before);
    }

    [WindowsGameAcceptanceFact]
    public async Task RealInspectorSchemaRecordedEquipAndGuidedResultsRemainNarrowAndReadOnly()
    {
        using var fixture = new EvidenceFixture();
        var equip = fixture.Evidence("equip-unequip", equipped: true);
        var guided = fixture.Evidence("combat"); var before = fixture.Capture();
        var result = await RunAsync(fixture.Parent, "-AccountId", EvidenceFixture.Account.ToString());
        Assert.Equal(0, result.ExitCode); Assert.NotNull(result.Json); NoLivePass(result.Json);
        var row = Row(result.Json, "equip-unequip");
        Assert.Equal(Path.GetFileName(equip), row["evidenceId"]!.GetValue<string>());
        Assert.Equal("SavedEquipObserved", row["savedStateResult"]!.GetValue<string>());
        Assert.Equal("RecordedSavedObservation", row["evidenceStatus"]!.GetValue<string>());
        Assert.Equal("Pending", Row(result.Json, "combat")["evidenceStatus"]!.GetValue<string>());
        Assert.Equal(Path.GetFileName(guided), Row(result.Json, "combat")["evidenceId"]!.GetValue<string>());
        fixture.AssertUnchanged(before);
        var table = await RunAsync(fixture.Parent, "-Format", "Table");
        Assert.Equal(0, table.ExitCode); Assert.Contains("LiveClientVisualPending", table.Output);
        Assert.DoesNotContain(fixture.Owned.ConfigurationPath, table.Output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Money=", table.Output, StringComparison.Ordinal);
        fixture.AssertUnchanged(before);
    }

    [WindowsGameAcceptanceFact]
    public async Task MalformedLatestDoesNotFallBackToOlderSavedObservationAndMixedEvidenceSurvives()
    {
        using var fixture = new EvidenceFixture();
        var equip = fixture.Evidence("equip-unequip", equipped: true); fixture.Evidence("combat");
        fixture.Raw(equip, EvidenceFixture.ResumeLeaf(), "{malformed");
        var mismatch = fixture.Evidence("reconnect");
        var receipt = EvidenceFixture.Read(mismatch, "receipt.json"); receipt["accountId"] = EvidenceFixture.Account + 1;
        fixture.Write(mismatch, "receipt.json", receipt); var before = fixture.Capture();
        var result = await RunAsync(fixture.Parent);
        Assert.Equal(1, result.ExitCode); Assert.NotNull(result.Json); NoLivePass(result.Json);
        Assert.Equal("InvalidEvidence", result.Json["status"]!.GetValue<string>());
        Assert.Equal("Invalid", Row(result.Json, "equip-unequip")["evidenceStatus"]!.GetValue<string>());
        Assert.Null(Row(result.Json, "equip-unequip")["savedStateResult"]);
        Assert.Equal("receiptIdentityOrSchemaMismatch", Row(result.Json, "reconnect")["reason"]!.GetValue<string>());
        Assert.Equal("Pending", Row(result.Json, "combat")["evidenceStatus"]!.GetValue<string>());
        fixture.AssertUnchanged(before);
    }

    [WindowsGameAcceptanceFact]
    public async Task EditedVisualPassAndChangedRetainedSnapshotAreInvalid()
    {
        using var fixture = new EvidenceFixture(); var equip = fixture.Evidence("equip-unequip", equipped: true);
        var finalLeaf = Directory.GetFiles(equip, "resume-*-receipt.json").Single();
        var final = JsonNode.Parse(File.ReadAllBytes(finalLeaf))!; final["visualAcceptance"] = "LiveClientPassed";
        fixture.Write(equip, Path.GetFileName(finalLeaf), final);
        var visual = await RunAsync(fixture.Parent);
        Assert.Equal(1, visual.ExitCode); Assert.NotNull(visual.Json); NoLivePass(visual.Json);
        Assert.Equal("unverifiedVisualPromotionInReceipt", Row(visual.Json, "equip-unequip")["reason"]!.GetValue<string>());
        final["visualAcceptance"] = "LiveClientVisualPending"; fixture.Write(equip, Path.GetFileName(finalLeaf), final);
        var afterLeaf = final["after"]!.GetValue<string>(); var after = EvidenceFixture.Read(equip, afterLeaf);
        after["report"]!["character"]!["gold"] = 9999; fixture.Write(equip, afterLeaf, after);
        var changed = await RunAsync(fixture.Parent);
        Assert.Equal(1, changed.ExitCode); Assert.NotNull(changed.Json);
        Assert.Equal("equipRetainedEvidenceMismatch", Row(changed.Json, "equip-unequip")["reason"]!.GetValue<string>());
    }
}
