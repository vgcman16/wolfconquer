using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using ProtoBuf.Meta;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    public static string ExecuteMailboxPackage(string scenario, string guard)
    {
        Require(scenario is "Mode5" or "ClaimAll" or "MissingWire", "Known mailbox package child scenario required.");
        Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(), "Genuine Default must start fresh.");
        // Keep the natural fresh gate untouched. Only the complete real loader
        // may enable it; the existing scope restores its exact prior state.
        var scope = new Scope(seedGate: false);
        var errors = new List<Exception>();
        string? result = null;
        try
        {
            RunMailboxPackageChild(scope, scenario);
            result = JsonSerializer.Serialize(new
            {
                Guard = guard, Scenario = scenario, Completed = true,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                Data = new { Passed = true, Loaded = Table.m_Load, Scenario = scenario },
            });
        }
        catch (Exception error) { errors.Add(error); }
        try { scope.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Native package child action and cleanup failed.", errors);
        return result!;
    }

    private static void RunMailboxPackageChild(Scope scope, string scenario)
    {
        Require(!Table.m_Load, "Fresh process must begin with the natural closed native gate.");
        RequireNoProof();
        if (scenario == "MissingWire")
        {
            LegacyInventorySashUpgradeTests.RunNativePiratePackageMissingWireComponentAsync().GetAwaiter().GetResult();
            Require(!Table.m_Load && scope.Log.ToString() == "", "Missing wire refusal must preserve the natural closed gate.");
            RequireNoProof();
        }
        else
        {
            Table.Load(scope.Prefix);
            var proof = RequireProof();
            Require(Table.m_Load && scope.Log.ToString() == "", "Complete owned native table load must succeed without caught faults.");
            Require(Table.Location == scope.Prefix && Table.Info.Count == 1 && Table.Info[2].Count == 1 &&
                Values(Table.Info[2][3]).SequenceEqual(Enumerable.Range(1, 19).Select(value => (uint)value)) &&
                Rank.Ranks.Count == 18, "Complete actual gear/rank owner publication required.");
            VerifyContracts();
            LegacyInventorySashUpgradeTests.RunNativePiratePackageComponentAsync(scenario == "ClaimAll").GetAwaiter().GetResult();
            Require(Table.m_Load && scope.Log.ToString() == "", "Component must retain the genuine loaded gate and produce no native fault log.");
            Require(proof.IsCurrent() && ReferenceEquals(proof, RequireProof()), "Actual Pirate component must retain its original genuine completed-load witness.");
        }
    }
}
