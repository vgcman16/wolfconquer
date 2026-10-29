using System.Text.Json.Nodes;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class GameAcceptanceRunnerTests
{
    [WindowsGameAcceptanceFact]
    public async Task ActualHealthInitialRefusesMissingSavedMaximumManaRatherThanAcceptingRuntimeFallback()
    {
        using var f = new HealthFixture(includeMaximumMana: false);
        var result = await f.Invoke(); WalkResult(result, "PrerequisiteMissing");
        Assert.Null(f.Read("before.json")["report"]!["character"]!["maxMana"]);
        Assert.Contains(result["state"]!["assertions"]!.AsArray(), issue => issue!.GetValue<string>() == "savedVitalRequired:maxMana");
        Assert.False(File.Exists(Path.Combine(f.Evidence, "health-intent.json"))); f.Complete = true;
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualHealthRunnerObservesStackAndNativeZeroOrOneLastUnitReadOnlyWithPendingLiveStatus()
    {
        foreach (ushort stack in new ushort[] { 0, 1, 3 })
        {
            using var f = new HealthFixture(stack); f.AddContainers();
            WalkResult(await f.Invoke(), "WaitingForSavedHealthConsumption");
            var original = new[] { "owner.json", "before.json", "receipt.json", "health-intent.json" }
                .ToDictionary(leaf => leaf, leaf => File.ReadAllBytes(Path.Combine(f.Evidence, leaf)));
            WalkResult(await f.Invoke(true), "ClientActionPendingOrStateMismatch");
            f.SavedConsumption();
            var observed = await f.Invoke(true); WalkResult(observed, "SavedHealthConsumptionObserved");
            Assert.Empty(observed["state"]!["assertions"]!.AsArray());
            var receipt = f.Read(observed["receipt"]!.GetValue<string>());
            var after = f.Read(receipt["after"]!.GetValue<string>())["report"]!;
            Assert.Equal(80L, after["character"]!["hitpoints"]!.GetValue<long>());
            var selected = after["items"]!.AsArray().Where(row => row!["uid"]!.GetValue<uint>() == HealthFixture.Uid).ToArray();
            if (stack <= 1) Assert.Empty(selected); else Assert.Equal(stack - 1, Assert.Single(selected)!["stackSize"]!.GetValue<int>());
            foreach (var (leaf, bytes) in original) Assert.Equal(bytes, File.ReadAllBytes(Path.Combine(f.Evidence, leaf)));
            var all = Files(f.Parent);
            var result = await RunScriptAsync("Get-GameAcceptanceStatus.ps1", "-EvidenceParent", f.Parent);
            Assert.Equal(0, result.ExitCode); Assert.Empty(result.Error);
            var summary = JsonNode.Parse(result.Output)!;
            Assert.Equal(14, summary["rows"]!.AsArray().Count);
            var row = Assert.Single(summary["rows"]!.AsArray(), row => row!["scenario"]!.GetValue<string>() == "consume-health")!;
            Assert.Equal("RecordedSavedObservation", row["evidenceStatus"]!.GetValue<string>());
            Assert.Equal("SavedHealthConsumptionObserved", row["savedStateResult"]!.GetValue<string>());
            Assert.Equal("LiveClientVisualPending", row["visualStatus"]!.GetValue<string>());
            Assert.False(summary["sourceGateAccepted"]!.GetValue<bool>()); Assert.False(summary["liveGameplayProven"]!.GetValue<bool>());
            AssertFiles(f.Parent, all); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualHealthRunnerRejectsWrongHpTypeCurrencyManaAndCompleteSidecarByteDrift()
    {
        foreach (var fault in new[] { "hp", "type", "gold", "cp", "mana", "spells" })
        {
            using var f = new HealthFixture();
            var spells = new PlayerSpellSnapshotLoader(f.Routes.SpellsRoot);
            spells.SaveByCharacterUid(WalkFixture.Account, new([new(6001, 1, 7, 0, 0, 0)]));
            WalkResult(await f.Invoke(), "WaitingForSavedHealthConsumption");
            f.SavedConsumption(fault == "hp" ? 79 : 80, fault == "type" ? 1000001U : HealthFixture.Type);
            if (fault == "gold") f.Character(("Money", "101"));
            if (fault == "cp") f.Character(("ConquerPoints", "51"));
            if (fault == "mana") f.Character(("MinMana", "21"));
            if (fault == "spells")
            {
                var path = Path.Combine(f.Routes.SpellsRoot, WalkFixture.Account + ".bin"); var length = new FileInfo(path).Length;
                spells.SaveByCharacterUid(WalkFixture.Account, new([new(6001, 1, 8, 0, 0, 0)]));
                Assert.Equal(length, new FileInfo(path).Length);
            }
            var pending = await f.Invoke(true); WalkResult(pending, "ClientActionPendingOrStateMismatch");
            Assert.NotEmpty(pending["state"]!["assertions"]!.AsArray()); f.Complete = true;
        }
    }

    [WindowsGameAcceptanceFact]
    public async Task ActualHealthRunnerRefusesOldVitalsRealTruncationAndForeignSavedCharacter()
    {
        foreach (var fault in new[] { "missing", "null", "truncated", "identity", "other-row" })
        {
            using var f = new HealthFixture(); WalkResult(await f.Invoke(), "WaitingForSavedHealthConsumption");
            if (fault is "missing" or "null")
            {
                var before = f.Read("before.json");
                foreach (var field in new[] { "hitpoints", "minMana", "maxMana" })
                    if (fault == "missing") before["report"]!["character"]!.AsObject().Remove(field);
                    else before["report"]!["character"]![field] = null;
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
