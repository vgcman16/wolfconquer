using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [WindowsGameAcceptanceFact]
    public Task ActualSavedManaConsumptionRecordsExactRetainedReadOnlyObservation() => SavedResourcePositive(false);

    [WindowsGameAcceptanceFact]
    public Task ActualSavedFullRestoreConsumptionRecordsExactRetainedReadOnlyObservation() => SavedResourcePositive(true);

    private static async Task SavedResourcePositive(bool full)
    {
        foreach (ushort stack in new ushort[] { 0, 1, 3 })
        {
            using var f = new ResourceFixture(full, stack); f.AddContainers();
            WalkResult(await f.Invoke(), f.Waiting);
            var original = new[] { "owner.json", "before.json", "receipt.json", f.Intent }
                .ToDictionary(leaf => leaf, leaf => File.ReadAllBytes(Path.Combine(f.Evidence, leaf)));
            WalkResult(await f.Invoke(true), "ClientActionPendingOrStateMismatch");
            f.SavedConsumption(); var observed = await f.Invoke(true); WalkResult(observed, f.Observed);
            Assert.Empty(observed["state"]!["assertions"]!.AsArray());
            var receipt = f.Read(observed["receipt"]!.GetValue<string>());
            var report = f.Read(receipt["after"]!.GetValue<string>())["report"]!;
            Assert.Equal(30L, report["character"]!["minMana"]!.GetValue<long>());
            Assert.Equal(full ? 80L : 10L, report["character"]!["hitpoints"]!.GetValue<long>());
            var selected = report["items"]!.AsArray().Where(row => row!["uid"]!.GetValue<uint>() == ResourceFixture.Uid).ToArray();
            if (stack <= 1) Assert.Empty(selected); else Assert.Equal(stack - 1, Assert.Single(selected)!["stackSize"]!.GetValue<int>());
            foreach (var pair in original) Assert.Equal(pair.Value, File.ReadAllBytes(Path.Combine(f.Evidence, pair.Key)));
            var beforeStatus = Files(f.Parent);
            var status = await RunScriptAsync("Get-GameAcceptanceStatus.ps1", "-EvidenceParent", f.Parent);
            Assert.Equal(0, status.ExitCode); Assert.Empty(status.Error);
            var summary = JsonNode.Parse(status.Output)!; Assert.Equal(14, summary["rows"]!.AsArray().Count);
            var row = Assert.Single(summary["rows"]!.AsArray(), value => value!["scenario"]!.GetValue<string>() == f.Scenario)!;
            Assert.Equal("RecordedSavedObservation", row["evidenceStatus"]!.GetValue<string>());
            Assert.Equal(f.Observed, row["savedStateResult"]!.GetValue<string>());
            Assert.Equal("LiveClientVisualPending", row["visualStatus"]!.GetValue<string>());
            Assert.False(summary["sourceGateAccepted"]!.GetValue<bool>()); Assert.False(summary["liveGameplayProven"]!.GetValue<bool>());
            AssertFiles(f.Parent, beforeStatus); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task SavedFullRestoreMayObserveEitherOneImprovingVitalWithoutImposingAnHpTriggerRule()
    {
        foreach (var expected in new[] { (Hp: 10L, Mana: 30L), (Hp: 80L, Mana: 20L) })
        {
            using var f = new ResourceFixture(true, expectedHp: expected.Hp, expectedMana: expected.Mana);
            WalkResult(await f.Invoke(), f.Waiting); f.SavedConsumption(); WalkResult(await f.Invoke(true), f.Observed);
            f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task SavedResourceIntentRequiresImprovementAndActualSavedVitals()
    {
        foreach (var value in new[] { (Full: false, Hp: 80L, Mana: 20L), (Full: false, Hp: 80L, Mana: 19L),
            (Full: true, Hp: 9L, Mana: 30L), (Full: true, Hp: 80L, Mana: 19L), (Full: true, Hp: 10L, Mana: 20L) })
        {
            using var f = new ResourceFixture(value.Full, expectedHp: value.Hp, expectedMana: value.Mana);
            WalkResult(await f.Invoke(), "PrerequisiteMissing"); Assert.False(File.Exists(Path.Combine(f.Evidence, f.Intent))); f.Complete = true;
        }
        foreach (var full in new[] { false, true })
        {
            using var f = new ResourceFixture(full, includeMaximumMana: false);
            var result = await f.Invoke(); WalkResult(result, "PrerequisiteMissing");
            Assert.Contains(result["state"]!["assertions"]!.AsArray(), issue => issue!.GetValue<string>() == "savedVitalRequired:maxMana");
            Assert.False(File.Exists(Path.Combine(f.Evidence, f.Intent))); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualSavedResourceComparisonRejectsWrongVitalsTypesMoneyMapAndSidecarBytes()
    {
        foreach (var full in new[] { false, true })
            foreach (var fault in new[] { "mana", "hp", "type", "money", "map", "spells" })
            {
                using var f = new ResourceFixture(full);
                var spells = new PlayerSpellSnapshotLoader(f.Routes.SpellsRoot);
                spells.SaveByCharacterUid(WalkFixture.Account, new([new(6001, 1, 7, 0, 0, 0)]));
                WalkResult(await f.Invoke(), f.Waiting);
                f.SavedConsumption(hp: fault == "hp" ? (full ? 79 : 11) : null,
                    mana: fault == "mana" ? 29 : null, type: fault == "type" ? f.Type + 1 : null);
                if (fault == "money") f.Character(("Money", "101"));
                if (fault == "map") f.Character(("X", (f.Read("before.json")["report"]!["character"]!["x"]!.GetValue<long>() + 1)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture)));
                if (fault == "spells") spells.SaveByCharacterUid(WalkFixture.Account, new([new(6001, 1, 8, 0, 0, 0)]));
                var result = await f.Invoke(true); WalkResult(result, "ClientActionPendingOrStateMismatch");
                Assert.NotEmpty(result["state"]!["assertions"]!.AsArray()); f.Complete = true;
            }
    }

    [WindowsGameAcceptanceFact]
    public async Task SavedResourceComparisonRefusesMissingNullUnknownDuplicateAndRealIncompleteGraphs()
    {
        foreach (var fault in new[] { "missing", "null", "unknown-container", "duplicate-row", "truncated", "identity", "other-row" })
        {
            using var f = new ResourceFixture(true); WalkResult(await f.Invoke(), f.Waiting);
            if (fault is "missing" or "null" or "unknown-container" or "duplicate-row")
            {
                var before = f.Read("before.json"); var report = before["report"]!;
                if (fault == "missing") report["character"]!.AsObject().Remove("minMana");
                if (fault == "null") report["character"]!["hitpoints"] = null;
                if (fault == "unknown-container") report["items"]![0]!["container"] = "unknown";
                if (fault == "duplicate-row")
                {
                    report["items"]!.AsArray().Add(report["items"]![0]!.DeepClone());
                    report["physicalItemCount"] = 2; report["inventoryCount"] = 2;
                }
                f.Write("before.json", before); f.RefreshHashes();
            }
            f.SavedConsumption();
            if (fault == "truncated") f.AddRows(204);
            if (fault == "identity") f.Character(("UID", (WalkFixture.Account + 1).ToString()));
            if (fault == "other-row") f.AddRows(1);
            WalkResult(await f.Invoke(true), "ClientActionPendingOrStateMismatch"); f.Complete = true;
        }
    }
}
