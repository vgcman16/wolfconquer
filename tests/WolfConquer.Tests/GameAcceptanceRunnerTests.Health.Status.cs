using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [WindowsGameAcceptanceFact]
    public async Task ActualHealthStatusRechecksHpItemMoneySidecarAndIntentAfterOuterHashRefresh()
    {
        foreach (var fault in new[] { "hp", "stack", "item", "money", "digest", "intent" })
        {
            using var f = new HealthFixture(); WalkResult(await f.Invoke(), "WaitingForSavedHealthConsumption");
            f.SavedConsumption(); var observed = await f.Invoke(true); WalkResult(observed, "SavedHealthConsumptionObserved");
            var leaf = observed["receipt"]!.GetValue<string>(); var receipt = f.Read(leaf);
            var afterLeaf = receipt["after"]!.GetValue<string>(); var after = f.Read(afterLeaf);
            if (fault == "hp") after["report"]!["character"]!["hitpoints"] = 79;
            if (fault == "money") after["report"]!["character"]!["gold"] = 101;
            if (fault is "stack" or "item")
            {
                var item = Assert.Single(after["report"]!["items"]!.AsArray(), row => row!["uid"]!.GetValue<uint>() == HealthFixture.Uid)!;
                item[fault == "stack" ? "stackSize" : "itemId"] = fault == "stack" ? 1 : 1000001;
            }
            if (fault == "digest")
            {
                var file = Assert.Single(after["report"]!["files"]!.AsArray(), row => row!["kind"]!.GetValue<string>() == "Spells")!;
                var hash = file["contentSha256"]!.GetValue<string>(); file["contentSha256"] = (hash[0] == 'A' ? "B" : "A") + hash[1..];
            }
            if (fault == "intent")
            {
                var intent = f.Read("health-intent.json"); intent["expectedHitpoints"] = 81; f.Write("health-intent.json", intent);
                f.RefreshHashes(); receipt["state"]!["intentSha256"] = f.Hash("health-intent.json");
            }
            f.Write(afterLeaf, after); receipt["state"]!["afterSha256"] = f.Hash(afterLeaf); f.Write(leaf, receipt);
            var before = Files(f.Parent);
            var result = await RunScriptAsync("Get-GameAcceptanceStatus.ps1", "-EvidenceParent", f.Parent);
            Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
            var summary = JsonNode.Parse(result.Output)!; Assert.Equal("InvalidEvidence", summary["status"]!.GetValue<string>());
            var row = Assert.Single(summary["rows"]!.AsArray(), row => row!["scenario"]!.GetValue<string>() == "consume-health")!;
            Assert.Equal("Invalid", row["evidenceStatus"]!.GetValue<string>());
            Assert.False(summary["sourceGateAccepted"]!.GetValue<bool>()); Assert.False(summary["liveGameplayProven"]!.GetValue<bool>());
            AssertFiles(f.Parent, before); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualHealthIntentMalformedDuplicateUnknownAndForeignInputsStopBeforeResumeChild()
    {
        foreach (var fault in new[] { "malformed", "duplicate", "unknown", "foreign", "changed-hp" })
        {
            using var f = new HealthFixture(); WalkResult(await f.Invoke(), "WaitingForSavedHealthConsumption");
            var intent = f.Read("health-intent.json"); var path = Path.Combine(f.Evidence, "health-intent.json");
            if (fault == "malformed") File.WriteAllText(path, "{bad");
            else if (fault == "duplicate")
            {
                var raw = intent.ToJsonString(); var duplicate = raw.Replace("\"expectedHitpoints\":80", "\"expectedHitpoints\":80,\"expectedHitpoints\":81", StringComparison.Ordinal);
                Assert.NotEqual(raw, duplicate); File.WriteAllText(path, duplicate);
            }
            else
            {
                if (fault == "unknown") intent["sendCommand"] = "never-executed";
                else if (fault == "foreign") intent["domain"] = Guid.NewGuid().ToString("N");
                else intent["expectedHitpoints"] = 81;
                f.Write("health-intent.json", intent); f.RefreshHashes();
            }
            var refused = await f.Invoke(true); WalkResult(refused, "PrerequisiteMissingOrOutcomeUnproven", 1);
            Assert.Empty(refused["steps"]!.AsArray()); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualHealthInitialAndResumeCannotDispatchRunSourceChecksOrChangePinnedArguments()
    {
        foreach (var resume in new[] { false, true })
        {
            using var f = new HealthFixture();
            if (resume) WalkResult(await f.Invoke(), "WaitingForSavedHealthConsumption");
            foreach (var flag in new[] { "-SendTestKit", "-RunSourceChecks" })
            {
                var before = Files(f.Parent); WalkResult(await f.Invoke(resume, flag), "PrerequisiteMissingOrOutcomeUnproven", 1);
                AssertFiles(f.Parent, before);
            }
            if (resume)
                foreach (var pin in new[] { "-ExpectedItemUid", "-ExpectedItemId", "-ExpectedHitpoints" })
                {
                    var args = f.Arguments.ToArray(); var at = Array.IndexOf(args, pin) + 1;
                    args[at] = (long.Parse(args[at], System.Globalization.CultureInfo.InvariantCulture) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var before = Files(f.Root); var result = await RunAsync(args.Concat(["-Resume"]).ToArray());
                    Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
                    Assert.DoesNotContain("SavedHealthConsumptionObserved", result.Output); AssertFiles(f.Root, before);
                }
            f.Complete = true;
        }
    }
}
