using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [WindowsGameAcceptanceFact]
    public async Task ActualWalkRunnerObservesOnlyChangedExactSavedDestinationAndStatusRemainsNarrow()
    {
        using var f = new WalkFixture();
        f.AddRealContainerRows();
        WalkResult(await f.Invoke(), "WaitingForSavedWalk");
        var items = f.Read("before.json")["report"]!["items"]!.AsArray();
        foreach (var container in new[] { "inventory", "equipment", "warehouse", "relicFuse", "mythsoulBag", "rune", "wardrobe" })
        {
            Assert.Contains(items, row => row!["container"]!.GetValue<string>() == container);
            Assert.Contains(items, row => row!["container"]!.GetValue<string>() == container + ".deposited");
        }
        var original = new[] { "owner.json", "before.json", "receipt.json", "walk-intent.json" }
            .ToDictionary(leaf => leaf, leaf => File.ReadAllBytes(Path.Combine(f.Evidence, leaf)));
        WalkResult(await f.Invoke(resume: true), "ClientActionPendingOrStateMismatch");
        f.Character(("X", "409"), ("Y", "355"));
        WalkResult(await f.Invoke(resume: true), "ClientActionPendingOrStateMismatch");
        f.Destination();
        var observed = await f.Invoke(resume: true); WalkResult(observed, "SavedWalkObserved");
        Assert.Empty(observed["state"]!["assertions"]!.AsArray());
        foreach (var (leaf, bytes) in original) Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(f.Evidence, leaf)));
        var allBefore = Files(f.Parent);
        var status = await RunScriptAsync("Get-GameAcceptanceStatus.ps1", "-EvidenceParent", f.Parent);
        Assert.Equal(0, status.ExitCode); Assert.Empty(status.Error);
        var summary = JsonNode.Parse(status.Output)!;
        Assert.False(summary["sourceGateAccepted"]!.GetValue<bool>()); Assert.False(summary["liveGameplayProven"]!.GetValue<bool>());
        var row = Assert.Single(summary["rows"]!.AsArray(), row => row!["scenario"]!.GetValue<string>() == "login-walk-save")!;
        Assert.Equal("SavedWalkObserved", row["savedStateResult"]!.GetValue<string>());
        Assert.Equal("RecordedSavedObservation", row["evidenceStatus"]!.GetValue<string>());
        Assert.Equal("LiveClientVisualPending", row["visualStatus"]!.GetValue<string>());
        Assert.False(row["liveGameplayProven"]!.GetValue<bool>()); AssertFiles(f.Parent, allBefore);
        f.Complete = true;
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualWalkRunnerRefusesUnknownRetainedItemContainerWithOtherwiseCompleteRows()
    {
        using var f = new WalkFixture(); f.AddRealContainerRows();
        WalkResult(await f.Invoke(), "WaitingForSavedWalk");
        var before = f.Read("before.json");
        var rune = Assert.Single(before["report"]!["items"]!.AsArray(), row => row!["container"]!.GetValue<string>() == "rune")!;
        rune["container"] = "unrecognized";
        f.Write("before.json", before); f.RefreshRetainedHashes(); f.Destination();
        var pending = await f.Invoke(resume: true); WalkResult(pending, "ClientActionPendingOrStateMismatch");
        Assert.Contains(pending["state"]!["assertions"]!.AsArray(), issue => issue!.GetValue<string>() == "unknownItemContainer");
        f.Complete = true;
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualWalkStatusRefusesChangedCurrencyOrSidecarDigestEvenWithRefreshedAfterReference()
    {
        foreach (var fault in new[] { "currency", "digest" })
        {
            using var f = new WalkFixture();
            WalkResult(await f.Invoke(), "WaitingForSavedWalk"); f.Destination();
            var observed = await f.Invoke(resume: true); WalkResult(observed, "SavedWalkObserved");
            var receiptLeaf = observed["receipt"]!.GetValue<string>();
            var receipt = f.Read(receiptLeaf); var afterLeaf = receipt["after"]!.GetValue<string>();
            var after = f.Read(afterLeaf);
            if (fault == "currency") after["report"]!["character"]!["gold"] = after["report"]!["character"]!["gold"]!.GetValue<long>() + 1;
            else
            {
                var file = Assert.Single(after["report"]!["files"]!.AsArray(), file => file!["kind"]!.GetValue<string>() == "Items")!;
                var digest = file["contentSha256"]!.GetValue<string>();
                file["contentSha256"] = (digest[0] == 'A' ? "B" : "A") + digest[1..];
            }
            f.Write(afterLeaf, after);
            receipt["state"]!["afterSha256"] = f.Hash(afterLeaf); f.Write(receiptLeaf, receipt);
            var before = Files(f.Parent);
            var result = await RunScriptAsync("Get-GameAcceptanceStatus.ps1", "-EvidenceParent", f.Parent);
            Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
            var summary = JsonNode.Parse(result.Output)!;
            Assert.Equal("InvalidEvidence", summary["status"]!.GetValue<string>());
            var row = Assert.Single(summary["rows"]!.AsArray(), row => row!["scenario"]!.GetValue<string>() == "login-walk-save")!;
            Assert.Equal("Invalid", row["evidenceStatus"]!.GetValue<string>());
            Assert.NotEqual("RecordedSavedObservation", row["evidenceStatus"]!.GetValue<string>());
            Assert.False(summary["sourceGateAccepted"]!.GetValue<bool>()); Assert.False(summary["liveGameplayProven"]!.GetValue<bool>());
            AssertFiles(f.Parent, before); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualWalkRunnerRejectsSelectedCurrencySameSizeSpellDriftAndForeignCharacter()
    {
        using var f = new WalkFixture();
        var spellLoader = new PlayerSpellSnapshotLoader(f.Routes.SpellsRoot);
        spellLoader.SaveByCharacterUid(WalkFixture.Account,
            new PlayerSpellSnapshot(new[] { new PlayerSpellSnapshotEntry(6001, 1, 7, 0, 0, 0) }));
        WalkResult(await f.Invoke(), "WaitingForSavedWalk"); f.Destination();
        f.Character(("Money", "101"));
        WalkResult(await f.Invoke(resume: true), "ClientActionPendingOrStateMismatch");
        f.Character(("Money", "100"));
        var spells = Path.Combine(f.Routes.SpellsRoot, WalkFixture.Account + ".bin");
        var original = File.ReadAllBytes(spells);
        spellLoader.SaveByCharacterUid(WalkFixture.Account,
            new PlayerSpellSnapshot(new[] { new PlayerSpellSnapshotEntry(6001, 1, 8, 0, 0, 0) }));
        Assert.Equal(original.Length, File.ReadAllBytes(spells).Length);
        WalkResult(await f.Invoke(resume: true), "ClientActionPendingOrStateMismatch");
        File.WriteAllBytes(spells, original);
        f.Character(("UID", (WalkFixture.Account + 1).ToString()));
        WalkResult(await f.Invoke(resume: true), "ClientActionPendingOrStateMismatch");
        f.Complete = true;
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualWalkRunnerRefusesActualTruncatedInspectorInsteadOfAcceptingDestination()
    {
        using var f = new WalkFixture(); WalkResult(await f.Invoke(), "WaitingForSavedWalk");
        f.AddRealReservedRows(204); f.Destination();
        var pending = await f.Invoke(resume: true); WalkResult(pending, "ClientActionPendingOrStateMismatch");
        Assert.Contains(pending["state"]!["assertions"]!.AsArray(), issue => issue!.GetValue<string>() == "completeSavedProjectionRequired");
        f.Complete = true;
    }

    [WindowsGameAcceptanceFact]
    public async Task OldDigestlessRetainedInspectorCannotQualifyTheWalkEvenWithMatchingCounts()
    {
        using var f = new WalkFixture(); WalkResult(await f.Invoke(), "WaitingForSavedWalk");
        var before = f.Read("before.json");
        foreach (var file in before["report"]!["files"]!.AsArray()) file!.AsObject().Remove("contentSha256");
        f.Write("before.json", before); f.RefreshRetainedHashes(); f.Destination();
        WalkResult(await f.Invoke(resume: true), "ClientActionPendingOrStateMismatch");
        f.Complete = true;
    }

    [WindowsGameAcceptanceFact]
    public async Task MalformedUnknownAndForeignWalkIntentStopBeforeAnyResumeChildCommand()
    {
        foreach (var fault in new[] { "malformed", "duplicate", "unknown", "foreign" })
        {
            using var f = new WalkFixture(); WalkResult(await f.Invoke(), "WaitingForSavedWalk");
            var intent = f.Read("walk-intent.json");
            if (fault == "malformed") File.WriteAllText(Path.Combine(f.Evidence, "walk-intent.json"), "{bad");
            else if (fault == "duplicate")
            {
                var raw = intent.ToJsonString();
                var duplicated = raw.Replace("\"expectedX\":411", "\"expectedX\":411,\"expectedX\":412", StringComparison.Ordinal);
                Assert.NotEqual(raw, duplicated);
                File.WriteAllText(Path.Combine(f.Evidence, "walk-intent.json"), duplicated);
            }
            else
            {
                if (fault == "unknown") intent["sendCommand"] = "never-executed";
                else intent["domain"] = Guid.NewGuid().ToString("N");
                f.Write("walk-intent.json", intent); f.RefreshRetainedHashes();
            }
            var stopped = await f.Invoke(resume: true); WalkResult(stopped, "PrerequisiteMissingOrOutcomeUnproven", 1);
            Assert.Empty(stopped["steps"]!.AsArray());
            f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ResumeCannotDispatchRunSourceChecksOrChangeTheRetainedDestination()
    {
        using var f = new WalkFixture(); WalkResult(await f.Invoke(), "WaitingForSavedWalk");
        foreach (var flag in new[] { "-SendTestKit", "-RunSourceChecks" })
        {
            var before = Files(f.Parent); WalkResult(await f.Invoke(true, flag), "PrerequisiteMissingOrOutcomeUnproven", 1);
            AssertFiles(f.Parent, before);
        }
        var changed = f.Arguments.ToArray(); changed[Array.IndexOf(changed, "-ExpectedX") + 1] = "412";
        var refused = await RunAsync(changed.Concat(new[] { "-Resume" }).ToArray());
        Assert.Equal(1, refused.ExitCode); Assert.DoesNotContain("SavedWalkObserved", refused.Output);
        f.Complete = true;
    }
}
