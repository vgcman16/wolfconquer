using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using ProtoBuf.Meta;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

// Only the measured fresh child invokes these compiled entries. Default is never reset.
internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    public static string Execute(string scenario, string guard)
    {
        Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(), "Genuine Default must start fresh.");
        return ExecuteOwnedChild(scenario, guard, scope => scenario switch
        {
            "WirePackedOpt" => RunPackedWireOptionScenario(scope),
            "EnumNonzeroLiterals" or "EnumForeignMapped" or "EnumPassthruMutation" or "EnumMappingMutation" => RunEnumWireScenario(scope, scenario),
            "RegistrationOwned" or "RegistrationForeignExact" or "RegistrationForeignExactFrozen" => RunRegistrationProvenanceScenario(scope, scenario),
            "RegistrationFrozenFactory" or "RegistrationFrozenSurrogate" or "RegistrationFrozenPacked" => RunFrozenRegistrationMutationScenario(scope, scenario),
            "RepeatedLoad" or "ConflictFirst" or "ConflictLate" or "FrozenFirst" or "FrozenLate" or "UnrelatedNinja" => RunRegistryScenario(scope, scenario),
            _ when scenario.StartsWith("Stage", StringComparison.Ordinal) => RunStageScenario(scope, scenario),
            _ when scenario.StartsWith("Proof", StringComparison.Ordinal) => RunProofScenario(scope, scenario),
            _ => RunLoaderScenario(scope, scenario),
        });
    }

    private static string ExecuteOwnedChild(string scenario, string guard, Func<Scope, object?> action)
    {
        var scope = new Scope();
        var errors = new List<Exception>();
        string? result = null;
        try
        {
            object? details = action(scope);
            bool ready = Table.TryCaptureMailboxLoadProof(out var proof);
            Require(!ready || proof is not null && proof.IsCurrent(), "Captured readiness must have a current real proof.");
            result = JsonSerializer.Serialize(new
            {
                Guard = guard, Scenario = scenario, Completed = true,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                Data = new { Scenario = scenario, Passed = true, Loaded = Table.m_Load, Ready = ready,
                    Location = Table.Location, RegistryTypes = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Count(),
                    Log = scope.Log.ToString(), Details = details },
            });
        }
        catch (Exception error) { errors.Add(error); }
        try { scope.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Native loader child action and cleanup failed.", errors);
        return result!;
    }

    private static Table.MailboxLoadProof RequireProof()
    {
        Require(Table.TryCaptureMailboxLoadProof(out var proof) && proof is not null && proof.IsCurrent(),
            "Only a complete actual load may grant a current mailbox load proof.");
        return proof!;
    }

    private static void RequireNoProof()
    {
        Require(!Table.TryCaptureMailboxLoadProof(out var proof) && proof is null,
            "Incomplete, assigned or mutated metadata must not issue a mailbox load proof.");
    }
}
