using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Config = WolfConquer.Core.World.LegacyNativeServerConfig;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    private static object RunRuntimeScenario(Scope scope, string scenario)
    {
        using var files = new RuntimeMetadataFiles(scope);
        var globalLocation = Config.DbLocation;
        if (scenario == "RuntimeConfiguration")
        {
            var before = new GraphSnapshot();
            Require(LegacyNativeCombatGearRuntime.TryLoad(null, null, out var absent, out var reason) && absent is null && reason == "",
                "Both absent must hold the native feature without adopting ambient globals.");
            foreach (var pair in new (string? Root, string? Rank)[]
            {
                (files.DirectoryPath, null), (null, files.RankPath), (" ", files.RankPath),
                ("relative", files.RankPath), (files.DirectoryPath, "relative"),
                (files.DirectoryPath, Path.Combine(files.DirectoryPath, "missing-rank.txt")),
            })
            {
                Require(!LegacyNativeCombatGearRuntime.TryLoad(pair.Root, pair.Rank, out var rejected, out reason) &&
                    rejected is null && reason.Length != 0, "Invalid explicit startup configuration must refuse.");
                before.RequireUnchanged(); RequireNoProof();
            }
            var actual = LoadRuntime(files);
            Require(LegacyNativeCombatGearRuntime.TryLoad(null, null, out absent, out reason) && absent is null && reason == "" && actual.IsCurrent,
                "Absent configuration must not adopt or disturb an independently published ambient graph.");
        }
        else if (scenario == "RuntimePublication")
        {
            var owner = LoadRuntime(files);
            var first = RequireProof();
            Require(owner.IsCurrent, "Actual runtime must retain its newly issued proof.");
            files.RewriteGear("bad row\n");
            var before = new GraphSnapshot();
            Require(!LegacyNativeCombatGearRuntime.TryLoad(files.DirectoryPath, files.RankPath, out var bad, out _) && bad is null,
                "A bad reload cannot create a runtime owner.");
            before.RequireUnchanged(); Require(owner.IsCurrent && ReferenceEquals(first, RequireProof()), "Failed reload displaced the valid owner.");
            files.RewriteGear(GearRow());
            var second = LoadRuntime(files);
            Require(second.IsCurrent && !owner.IsCurrent && !ReferenceEquals(first, RequireProof()),
                "Equal-content complete reload must supersede the exact prior owner.");
            var row = Table.Info[2][3]; row.RawColumns[0]++;
            Require(!second.IsCurrent, "Raw source alias mutation must invalidate semantic content proof.");
            row.RawColumns[0]--;
            Require(second.IsCurrent, "Restored exact content should satisfy its retained witness.");
        }
        else if (scenario == "RuntimeExtendedSchemaHeld")
        {
            var owner = LoadRuntime(files); var before = new GraphSnapshot(); var proof = RequireProof();
            // Real retained22 width. Neither competing semantic interpretation
            // may become globally ready until matching client evidence resolves it.
            const string retained = "1@@201@@1@@300@@60@@30@@0@@0@@0@@17640000@@0@@0@@17650000@@0@@0@@0@@0@@0@@240@@0@@0@@0@@\n";
            files.RewriteGear(retained);
            Require(!LegacyNativeCombatGearRuntime.TryLoad(files.DirectoryPath, files.RankPath, out var held, out var heldReason) &&
                held is null && heldReason == "nativeCombatGearSchema22Unresolved",
                "Unresolved22 interpretation must refuse global semantic publication.");
            before.RequireUnchanged(); Require(owner.IsCurrent && ReferenceEquals(proof, RequireProof()), "Unresolved schema replaced prior graph.");
            files.RewriteGear(GearRow() + GearRow().TrimEnd('\n') + "@@0@@0@@bad\n");
            Require(!LegacyNativeCombatGearRuntime.TryLoad(files.DirectoryPath, files.RankPath, out held, out _) && held is null,
                "Malformed duplicate/extended tail must not be ignored.");
            before.RequireUnchanged();
        }
        else if (scenario == "RuntimeOwnedFrame") RunRuntimeOwnedFrame(scope, files);
        else throw new InvalidOperationException("Unknown runtime scenario.");
        Require(Config.DbLocation == globalLocation, "Explicit metadata startup changed global DbLocation.");
        return new { Scenario = scenario, GlobalDbLocationUnchanged = true };
    }

    private static LegacyNativeCombatGearRuntime LoadRuntime(RuntimeMetadataFiles files)
    {
        Require(LegacyNativeCombatGearRuntime.TryLoad(files.DirectoryPath, files.RankPath, out var owner, out var reason) && owner is not null,
            "Real explicit directory/rank load failed: " + reason);
        Require(owner!.MetadataDirectory == files.DirectoryPath && owner.RankFilePath == files.RankPath, "Runtime changed explicit input routes.");
        return owner;
    }
}
