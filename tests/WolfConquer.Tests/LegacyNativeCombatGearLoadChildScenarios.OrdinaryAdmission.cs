using System.Runtime.InteropServices;
using System.Text.Json;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    public static string ExecuteOrdinaryAdmission(string scenario, string guard)
    {
        Require(scenario is "CommittedOrdinaryIntent" or "UidFreePendingFrame" or "UidFreePendingRecovery",
            "Known ordinary restart child scenario required.");
        var root = Environment.GetEnvironmentVariable("WOLF_ORDINARY_RESTART_ROOT")
            ?? throw new InvalidOperationException("Owned ordinary fixture root required.");
        LegacyNativeHairfaceStorageLoginTests.RequireOrdinaryRestartOwnedRoot(root);
        // Real once-only source registrar, without a native loaded flag or any
        // ordinary producer construction. Default remains genuine and fresh.
        _ = new NativeCombatGearRegisteredTestContext();
        if (scenario == "CommittedOrdinaryIntent")
            LegacyNativeHairfaceStorageLoginTests.RunDefaultOrdinaryRestartChildAsync(root).GetAwaiter().GetResult();
        else
            LegacyNativeHairfaceStorageLoginTests.RunUidFreePendingJournalChildAsync(root,
                frameConstructor: scenario == "UidFreePendingFrame").GetAwaiter().GetResult();
        return JsonSerializer.Serialize(new { Guard = guard, Scenario = scenario, Completed = true,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            Data = new { Passed = true, Scenario = scenario, SyntheticOnly = true } });
    }
}
