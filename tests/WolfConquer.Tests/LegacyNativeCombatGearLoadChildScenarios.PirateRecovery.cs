using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using ProtoBuf.Meta;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    public static string ExecutePirateRecovery(string scenario, string guard)
    {
        var (fixtureScenario, job) = SelectPirateTargetScenario(scenario);
        Require(fixtureScenario is "Mode5" or "ClaimAll" or "MissingTables" or "BadTables" or "MetadataMiss" or
            "DuplicateItem" or "DuplicateRune" or "Noncanonical" or "MissingAnima" or "OwnerSwap" or "MetadataSwap" or "StaleOwner" or
            "Publish-valid" or "Publish-unadmitted" or "Publish-rebound-player" or "Publish-rebound-owner" or "Publish-loaded-phase" or "Publish-stale-session" or
            "Publish-callback-store" or "Publish-callback-exp" or "OrdinaryGearValid" or "OrdinaryGearMissingTables" or "OrdinaryGearUnknown" or "OrdinaryGearSchema22Refused" or "OrdinaryGearRevelator",
            "Known cold Pirate recovery scenario required.");
        Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(), "Fresh genuine Default required.");
        var scope = new Scope(seedGate: false); // No artificial gate enable/disable before the real loader.
        var errors = new List<Exception>();
        string? result = null;
        OrdinaryGearRuntimeFiles? ordinaryFiles = null;
        try
        {
            SeedPirateRecoveryTables(scope, fixtureScenario);
            Require(!Table.m_Load && !Table.TryCaptureMailboxLoadProof(out _), "Natural cold table state required.");
            var ordinary = fixtureScenario.StartsWith("OrdinaryGear", StringComparison.Ordinal);
            LegacyNativeCombatGearRuntime? nativeRuntime = null;
            if (ordinary)
            {
                ordinaryFiles = new OrdinaryGearRuntimeFiles(scope, fixtureScenario == "OrdinaryGearSchema22Refused");
                if (fixtureScenario == "OrdinaryGearSchema22Refused")
                {
                    Require(!LegacyNativeCombatGearRuntime.TryLoad(ordinaryFiles.DirectoryPath, ordinaryFiles.RankPath,
                        out nativeRuntime, out var schemaReason) && nativeRuntime is null && schemaReason == "nativeCombatGearSchema22Unresolved",
                        "Unresolved 22-column schema must refuse the actual startup publisher.");
                    Require(!Table.m_Load && !Table.TryCaptureMailboxLoadProof(out _), "Refused startup must not publish a native owner.");
                }
                else if (fixtureScenario != "OrdinaryGearMissingTables")
                    Require(LegacyNativeCombatGearRuntime.TryLoad(ordinaryFiles.DirectoryPath, ordinaryFiles.RankPath,
                        out nativeRuntime, out var reason) && nativeRuntime is not null, "Actual runtime metadata load failed: " + reason);
            }
            else if (fixtureScenario != "MissingTables")
            {
                Table.Load(scope.Prefix);
                if (fixtureScenario == "BadTables") Require(!Table.TryCaptureMailboxLoadProof(out _), "Incomplete table load must refuse.");
                else Require(Table.TryCaptureMailboxLoadProof(out var proof) && proof!.IsCurrent(), "Complete real table load required.");
            }
            if (fixtureScenario == "OrdinaryGearRevelator")
                WorldServerFrameHandlerTests.RunOwnedRevelatorStartupAsync(nativeRuntime!, ordinaryFiles!.GearRows,
                    ordinaryFiles.JadeRows).GetAwaiter().GetResult();
            else if (ordinary)
                LegacyAuthenticatedOrdinaryCreationTests.RunOrdinaryGearLoadedScenarioAsync(fixtureScenario, nativeRuntime,
                    ordinaryFiles!.GearRows, ordinaryFiles.JadeRows).GetAwaiter().GetResult();
            else if (fixtureScenario.StartsWith("Publish-", StringComparison.Ordinal))
                LegacyInventorySashUpgradeTests.RunDetachedPirateRecoveryScenarioAsync(fixtureScenario[8..]).GetAwaiter().GetResult();
            else LegacyInventorySashUpgradeTests.RunColdPirateRecoveryScenarioAsync(fixtureScenario, job).GetAwaiter().GetResult();
            result = JsonSerializer.Serialize(new
            {
                Guard = guard, Scenario = scenario, Completed = true,
                FrameworkDescription = RuntimeInformation.FrameworkDescription,
                Data = new { Passed = true, Scenario = scenario, Loaded = Table.m_Load, FixtureClass = job },
            });
        }
        catch (Exception error) { errors.Add(error); }
        try { ordinaryFiles?.Dispose(); } catch (Exception error) { errors.Add(error); }
        try { scope.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Cold Pirate recovery and owned table cleanup failed.", errors);
        return result!;
    }

    private static void SeedPirateRecoveryTables(Scope scope, string scenario)
    {
        var gear = GearRow(1, 701, 1) + GearRow(2, 701, 2);
        if (scenario != "MetadataMiss") gear += GearRow(3, 702, 1);
        if (scenario == "OrdinaryGearRevelator")
        {
            var first = new uint[19]; first[0] = 1; first[1] = 701; first[2] = 1;
            var second = new uint[19]; second[0] = 2; second[1] = 701; second[2] = 2;
            gear = string.Join("@@", first) + "\n" + string.Join("@@", second) + "\n";
        }
        scope.Write("db-combat_gear.txt", gear);
        static string JadeRow(uint id, uint rune)
        {
            var fields = new uint[15];
            fields[0] = id; fields[9] = rune; fields[10] = 1; fields[14] = 701;
            return string.Join("@@", fields) + "\n";
        }
        scope.Write("db-daoqi_dict_type.txt", JadeRow(9001, 1) + JadeRow(9002, 2) + JadeRow(9003, 1) +
            (scenario == "BadTables" ? "truncated\n" : ""));
    }
}
