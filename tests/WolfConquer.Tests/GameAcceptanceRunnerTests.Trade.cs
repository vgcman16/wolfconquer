using System.Text.Json.Nodes;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [WindowsGameAcceptanceFact]
    public async Task ActualSavedTradePairRecordsTwoPlayerReadOnlyObservation()
    {
        using var f = new TradeFixture();
        WalkResult(await f.Invoke(), "WaitingForSavedTrade");
        var original = new[] { "owner.json", "trade-plan.json", "trade-intent.json", "before.json", "before-counterparty.json", "receipt.json" }
            .ToDictionary(leaf => leaf, leaf => File.ReadAllBytes(Path.Combine(f.Evidence, leaf)));
        WalkResult(await f.Invoke(true), "ClientActionPendingOrStateMismatch");
        f.SavedTransfer(); var output = await f.Invoke(true); WalkResult(output, "SavedTradeObserved");
        Assert.Equal("AtomicityPending", output["state"]!["atomicityStatus"]!.GetValue<string>());
        Assert.Empty(output["state"]!["assertions"]!.AsArray());
        var receipt = f.Read(output["receipt"]!.GetValue<string>());
        var first = f.Read(receipt["after"]!.GetValue<string>())["report"]!;
        var second = f.Read(receipt["state"]!["afterCounterparty"]!.GetValue<string>())["report"]!;
        Assert.Equal(TradeFixture.A, first["accountId"]!.GetValue<uint>());
        Assert.Equal(TradeFixture.B, second["accountId"]!.GetValue<uint>());
        Assert.Equal(93L, first["character"]!["gold"]!.GetValue<long>());
        Assert.Equal(107L, second["character"]!["gold"]!.GetValue<long>());
        Assert.Contains(second["items"]!.AsArray(), row => row!["uid"]!.GetValue<uint>() == TradeFixture.ItemA && row["stackSize"]!.GetValue<int>() == 0);
        foreach (var pair in original) Assert.Equal(pair.Value, File.ReadAllBytes(Path.Combine(f.Evidence, pair.Key)));
        var summary = await f.Status(); Assert.Equal(0, summary["actualExitCode"]!.GetValue<int>());
        Assert.Equal("RecordedSavedObservation", TradeFixture.Row(summary)["evidenceStatus"]!.GetValue<string>());
        Assert.False(summary["liveGameplayProven"]!.GetValue<bool>());
        Assert.False(summary["sourceGateAccepted"]!.GetValue<bool>());
        var bySecond = await f.Status(TradeFixture.B);
        Assert.Equal(TradeFixture.B, TradeFixture.Row(bySecond)["counterpartyAccountId"]!.GetValue<uint>());
        Assert.Equal("RecordedSavedObservation", TradeFixture.Row(bySecond)["evidenceStatus"]!.GetValue<string>());
        f.Complete = true;
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualSavedTradePairResumeUsesRetainedPlanWhenExternalInputIsGone()
    {
        using var f = new TradeFixture(); WalkResult(await f.Invoke(), "WaitingForSavedTrade");
        File.Delete(f.PlanPath); f.SavedTransfer(); WalkResult(await f.Invoke(true), "SavedTradeObserved");
        var owner = f.Read("owner.json");
        // Status reads retained files only. These strings are context identifiers,
        // never executables/configuration/plan paths to open during a summary.
        owner["dataToolPath"] = Path.Combine(f.Parent, "missing-tool.dll");
        owner["configPath"] = Path.Combine(f.Parent, "missing-config.json");
        owner["tradePlanPath"] = Path.Combine(f.Parent, "missing-plan.json"); f.Write("owner.json", owner);
        var summary = await f.Status(); Assert.Equal(0, summary["actualExitCode"]!.GetValue<int>());
        Assert.Equal("RecordedSavedObservation", TradeFixture.Row(summary)["evidenceStatus"]!.GetValue<string>());
        f.Complete = true;
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualTradeResumeRefusesSecondPlayerMoneyLocationAndSidecarChanges()
    {
        foreach (var fault in new[] { "money", "location", "sidecar" })
        {
            using var f = new TradeFixture(); WalkResult(await f.Invoke(), "WaitingForSavedTrade"); f.SavedTransfer();
            if (fault == "money") f.Character(TradeFixture.B, ("Money", "108"));
            if (fault == "location") f.Character(TradeFixture.B, ("X", "411"));
            if (fault == "sidecar") f.ChangeSecondSidecar();
            var result = await f.Invoke(true); WalkResult(result, "ClientActionPendingOrStateMismatch");
            Assert.NotEmpty(result["state"]!["assertions"]!.AsArray());
            Assert.NotEqual("RecordedSavedObservation", TradeFixture.Row(await f.Status())["evidenceStatus"]!.GetValue<string>());
            f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualTradeResumeRejectsChangedPairToolConfigurationIntentAndBeforeWithoutChildCommand()
    {
        foreach (var fault in new[] { "pair", "tool", "configuration", "intent", "before", "plan", "mode" })
        {
            using var f = new TradeFixture(); WalkResult(await f.Invoke(), "WaitingForSavedTrade");
            var arguments = f.Arguments;
            if (fault == "pair") arguments[Array.IndexOf(arguments, "-CounterpartyAccountId") + 1] = (TradeFixture.B + 1).ToString();
            if (fault is "tool" or "configuration")
            {
                var owner = f.Read("owner.json"); owner[fault == "tool" ? "dataToolSha256" : "configSha256"] = new string('A', 64);
                f.Write("owner.json", owner);
            }
            if (fault == "intent") { var intent = f.Read("trade-intent.json"); intent["items"]![0]!["stackSize"] = 2; f.Write("trade-intent.json", intent); }
            if (fault == "before") { var before = f.Read("before-counterparty.json"); before["report"]!["character"]!["gold"] = 101; f.Write("before-counterparty.json", before); }
            if (fault == "plan") { var plan = f.Read("trade-plan.json"); plan["participants"]![0]!["accountId"] = TradeFixture.B; f.Write("trade-plan.json", plan); }
            if (fault == "mode") arguments = arguments.Take(Array.IndexOf(arguments, "-CounterpartyAccountId")).ToArray();
            var result = await f.Invoke(true, arguments); WalkResult(result, "PrerequisiteMissingOrOutcomeUnproven", 1);
            Assert.Empty(result["steps"]!.AsArray()); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task SavedTradeStatusRechecksBothReportsDespiteRehashedEditableObservedReceipt()
    {
        foreach (var fault in new[] { "money", "identity", "truncation", "missing-vital", "unknown-row", "sidecar", "flags", "promoted-status" })
        {
            using var f = new TradeFixture(); WalkResult(await f.Invoke(), "WaitingForSavedTrade"); f.SavedTransfer();
            var output = await f.Invoke(true); WalkResult(output, "SavedTradeObserved");
            var leaf = output["receipt"]!.GetValue<string>(); var receipt = f.Read(leaf);
            var secondLeaf = receipt["state"]!["afterCounterparty"]!.GetValue<string>(); var second = f.Read(secondLeaf);
            var report = second["report"]!;
            if (fault == "money") report["character"]!["gold"] = 108;
            if (fault == "identity") report["character"]!["uid"] = TradeFixture.B + 1;
            if (fault == "truncation") report["summariesTruncated"] = true;
            if (fault == "missing-vital") report["character"]!.AsObject().Remove("minMana");
            if (fault == "unknown-row") report["items"]![0]!["container"] = "unknown";
            if (fault == "sidecar") Assert.Single(report["files"]!.AsArray(), row => row!["kind"]!.GetValue<string>() == "Spells")!["contentSha256"] = new string('B', 64);
            if (fault == "flags") receipt["state"]!["atomicityStatus"] = "Proven";
            if (fault == "promoted-status") receipt["state"]!["status"] = "ServerStateVerified";
            f.Write(secondLeaf, second); receipt["state"]!["afterSha256"]![1] = f.Hash(secondLeaf); f.Write(leaf, receipt);
            var summary = await f.Status(); Assert.Equal(1, summary["actualExitCode"]!.GetValue<int>());
            Assert.Equal("Invalid", TradeFixture.Row(summary)["evidenceStatus"]!.GetValue<string>()); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task TradePairRequiresCompleteExplicitArgumentsAndStrictPlanBeforeInspection()
    {
        foreach (var fault in new[] { "zero-account", "same-account", "missing-plan", "foreign-scenario", "unknown-key", "duplicate-key", "invalid-utf8" })
        {
            using var f = new TradeFixture(); var arguments = f.Arguments;
            if (fault == "zero-account") arguments[Array.IndexOf(arguments, "-CounterpartyAccountId") + 1] = "0";
            if (fault == "same-account") arguments[Array.IndexOf(arguments, "-CounterpartyAccountId") + 1] = TradeFixture.A.ToString();
            if (fault == "missing-plan") arguments = arguments.Take(Array.IndexOf(arguments, "-TradePlan")).ToArray();
            if (fault == "foreign-scenario") arguments[Array.IndexOf(arguments, "-Scenario") + 1] = "reconnect";
            if (fault == "unknown-key") { var plan = JsonNode.Parse(File.ReadAllBytes(f.PlanPath))!; plan["command"] = "save"; File.WriteAllText(f.PlanPath, plan.ToJsonString()); }
            if (fault == "duplicate-key") File.WriteAllText(f.PlanPath, "{\"schemaVersion\":1,\"schemaVersion\":1}");
            if (fault == "invalid-utf8") File.WriteAllBytes(f.PlanPath, [0xFF]);
            var result = await f.Invoke(arguments: arguments); WalkResult(result, "PrerequisiteMissingOrOutcomeUnproven", 1);
            if (result["steps"] is JsonArray steps) Assert.Empty(steps);
            f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ExistingTradeGuidedInvocationRemainsAvailableWithoutPairParameters()
    {
        using var f = new TradeFixture(); var arguments = f.Arguments.Take(Array.IndexOf(f.Arguments, "-CounterpartyAccountId")).ToArray();
        var before = Files(f.Root); var first = await RunAsync(arguments); Assert.Equal(2, first.ExitCode); Assert.Empty(first.Error);
        var initial = JsonNode.Parse(first.Output)!; Assert.Equal("GuidedActionsPending", initial["status"]!.GetValue<string>());
        Assert.Single(f.Read(initial["receipt"]!.GetValue<string>())["steps"]!.AsArray());
        var resumed = await RunAsync(arguments.Concat(["-Resume"]).ToArray()); Assert.Equal(2, resumed.ExitCode); Assert.Empty(resumed.Error);
        Assert.Equal("GuidedAssertionsPending", JsonNode.Parse(resumed.Output)!["status"]!.GetValue<string>());
        AssertFiles(f.Root, before); Assert.False(File.Exists(Path.Combine(f.Evidence, "trade-intent.json"))); f.Complete = true;
    }
}
