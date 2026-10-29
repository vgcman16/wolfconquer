using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [WindowsGameAcceptanceFact]
    public async Task ActualSavedResourceStatusRechecksTamperedDataEvenAfterEditableHashesAreRefreshed()
    {
        var faults = new[] { "mana", "hp", "money", "stack", "digest", "intent", "vital-type", "unknown-container", "status", "flags", "source", "step" };
        for (int index = 0; index < faults.Length; index++)
        {
            var fault = faults[index]; using var f = new ResourceFixture(index % 2 == 0);
            new PlayerSpellSnapshotLoader(f.Routes.SpellsRoot).SaveByCharacterUid(WalkFixture.Account, new([new(6001, 1, 7, 0, 0, 0)]));
            WalkResult(await f.Invoke(), f.Waiting); f.SavedConsumption(); var observed = await f.Invoke(true); WalkResult(observed, f.Observed);
            var leaf = observed["receipt"]!.GetValue<string>(); var receipt = f.Read(leaf);
            var afterLeaf = receipt["after"]!.GetValue<string>(); var after = f.Read(afterLeaf); var report = after["report"]!;
            if (fault == "mana") report["character"]!["minMana"] = 29;
            if (fault == "hp") report["character"]!["hitpoints"] = f.Full ? 79 : 11;
            if (fault == "money") report["character"]!["gold"] = 101;
            if (fault == "vital-type") report["character"]!["minMana"] = "30";
            if (fault is "stack" or "unknown-container")
            {
                var item = Assert.Single(report["items"]!.AsArray(), row => row!["uid"]!.GetValue<uint>() == ResourceFixture.Uid)!;
                if (fault == "stack") item["stackSize"] = 1; else item["container"] = "unknown";
            }
            if (fault == "digest")
            {
                var file = Assert.Single(report["files"]!.AsArray(), row => row!["kind"]!.GetValue<string>() == "Spells")!;
                var hash = file["contentSha256"]!.GetValue<string>(); file["contentSha256"] = (hash[0] == 'A' ? "B" : "A") + hash[1..];
            }
            if (fault == "intent")
            {
                var intent = f.Read(f.Intent); intent["expectedMana"] = 31; f.Write(f.Intent, intent); f.RefreshHashes();
                receipt["state"]!["intentSha256"] = f.Hash(f.Intent);
            }
            if (fault == "status") receipt["state"]!["status"] = "ServerStateVerified";
            if (fault == "flags") receipt["state"]!["liveStoreSynchronized"] = true;
            if (fault == "source") receipt["sourceChecks"]!["status"] = "SourceChecksVerified";
            if (fault == "step") receipt["steps"]![0]!["name"] = "send-operator-test-kit";
            f.Write(afterLeaf, after); receipt["state"]!["afterSha256"] = f.Hash(afterLeaf); f.Write(leaf, receipt);
            var before = Files(f.Parent); var result = await RunScriptAsync("Get-GameAcceptanceStatus.ps1", "-EvidenceParent", f.Parent);
            Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error); var summary = JsonNode.Parse(result.Output)!;
            Assert.Equal("InvalidEvidence", summary["status"]!.GetValue<string>());
            Assert.Equal("Invalid", Assert.Single(summary["rows"]!.AsArray(), row => row!["scenario"]!.GetValue<string>() == f.Scenario)!["evidenceStatus"]!.GetValue<string>());
            Assert.False(summary["sourceGateAccepted"]!.GetValue<bool>()); Assert.False(summary["liveGameplayProven"]!.GetValue<bool>());
            AssertFiles(f.Parent, before); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualSavedResourceIntentsRefuseMalformedDuplicateUnknownForeignAndMixedInputsBeforeResumeChild()
    {
        foreach (var fault in new[] { "malformed", "duplicate", "unknown", "foreign", "changed-mana", "mixed-vital", "wrong-type" })
        {
            using var f = new ResourceFixture(false); WalkResult(await f.Invoke(), f.Waiting);
            var intent = f.Read(f.Intent); var path = Path.Combine(f.Evidence, f.Intent);
            if (fault == "malformed") File.WriteAllText(path, "{bad");
            else if (fault == "duplicate")
            {
                var raw = intent.ToJsonString(); var duplicate = raw.Replace("\"expectedMana\":30", "\"expectedMana\":30,\"expectedMana\":31", StringComparison.Ordinal);
                Assert.NotEqual(raw, duplicate); File.WriteAllText(path, duplicate);
            }
            else
            {
                if (fault == "unknown") intent["sendCommand"] = "never-executed";
                if (fault == "foreign") intent["domain"] = Guid.NewGuid().ToString("N");
                if (fault == "changed-mana") intent["expectedMana"] = 31;
                if (fault == "mixed-vital") intent["expectedHitpoints"] = 80;
                if (fault == "wrong-type") intent["expectedItemId"] = 723726;
                f.Write(f.Intent, intent); f.RefreshHashes();
            }
            var refused = await f.Invoke(true); WalkResult(refused, "PrerequisiteMissingOrOutcomeUnproven", 1);
            Assert.Empty(refused["steps"]!.AsArray()); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task SavedResourceArgumentsCannotDispatchRunSourceChecksChangePinsOrUseAnUnknownClass()
    {
        foreach (var full in new[] { false, true })
        {
            using var f = new ResourceFixture(full);
            foreach (var flag in new[] { "-SendTestKit", "-RunSourceChecks" })
            {
                var files = Files(f.Parent); WalkResult(await f.Invoke(false, flag), "PrerequisiteMissingOrOutcomeUnproven", 1); AssertFiles(f.Parent, files);
            }
            WalkResult(await f.Invoke(), f.Waiting);
            foreach (var flag in new[] { "-SendTestKit", "-RunSourceChecks" })
            {
                var files = Files(f.Parent); WalkResult(await f.Invoke(true, flag), "PrerequisiteMissingOrOutcomeUnproven", 1); AssertFiles(f.Parent, files);
            }
            foreach (var key in new[] { "-ExpectedItemUid", "-ExpectedItemId", "-ExpectedMana" }.Concat(full ? new[] { "-ExpectedHitpoints" } : Array.Empty<string>()))
            {
                var args = f.Arguments.ToArray(); var at = Array.IndexOf(args, key) + 1;
                args[at] = (long.Parse(args[at], System.Globalization.CultureInfo.InvariantCulture) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                var files = Files(f.Root); var result = await RunAsync(args.Concat(["-Resume"]).ToArray());
                Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error); AssertFiles(f.Root, files);
                var steps = JsonNode.Parse(result.Output)!["steps"];
                Assert.True(steps is null || steps.AsArray().Count == 0);
            }
            f.Complete = true;
        }
        using (var f = new ResourceFixture(false))
        {
            WalkResult(await f.Invoke(false, "-ExpectedHitpoints", "80"), "PrerequisiteMissingOrOutcomeUnproven", 1);
            var args = f.Arguments.ToList(); var at = args.IndexOf("-ExpectedMana"); args.RemoveRange(at, 2);
            var result = await RunAsync(args.ToArray()); Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
            args = f.Arguments.ToList(); args[args.IndexOf("-Scenario") + 1] = "consume-unknown";
            result = await RunAsync(args.ToArray()); Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
            Assert.False(Directory.Exists(f.Evidence)); f.Complete = true;
        }
        using (var f = new ResourceFixture(true))
        {
            var args = f.Arguments.ToList(); var at = args.IndexOf("-ExpectedHitpoints"); args.RemoveRange(at, 2);
            var result = await RunAsync(args.ToArray()); Assert.Equal(1, result.ExitCode); Assert.Empty(result.Error);
            Assert.False(Directory.Exists(f.Evidence)); f.Complete = true;
        }
        foreach (var input in new[] { (Full: false, Type: 1000000U), (Full: false, Type: 1002031U), (Full: true, Type: 1001000U) })
        {
            using var f = new ResourceFixture(input.Full, type: input.Type);
            WalkResult(await f.Invoke(), "PrerequisiteMissingOrOutcomeUnproven", 1); Assert.False(Directory.Exists(f.Evidence)); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task SavedResourceStatusDoesNotOpenRetainedToolOrConfigurationPaths()
    {
        using var f = new ResourceFixture(false); WalkResult(await f.Invoke(), f.Waiting); f.SavedConsumption(); WalkResult(await f.Invoke(true), f.Observed);
        var owner = f.Read("owner.json"); owner["dataToolPath"] = Path.Combine(f.Parent, "never-open-this-tool.dll");
        owner["configPath"] = Path.Combine(f.Parent, "never-open-this-config.json"); f.Write("owner.json", owner);
        var before = Files(f.Parent); var result = await RunScriptAsync("Get-GameAcceptanceStatus.ps1", "-EvidenceParent", f.Parent);
        Assert.Equal(0, result.ExitCode); Assert.Empty(result.Error); var summary = JsonNode.Parse(result.Output)!;
        Assert.Equal("RecordedSavedObservation", Assert.Single(summary["rows"]!.AsArray(), row => row!["scenario"]!.GetValue<string>() == f.Scenario)!["evidenceStatus"]!.GetValue<string>());
        Assert.False(summary["liveGameplayProven"]!.GetValue<bool>()); AssertFiles(f.Parent, before); f.Complete = true;
    }
}
