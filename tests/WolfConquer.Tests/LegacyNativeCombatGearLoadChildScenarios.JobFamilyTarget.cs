namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    // Four explicit scenario identities; the existing measured child entry,
    // genuine Default and actual metadata loader remain required.
    private static (string Scenario, uint Job) SelectPirateTargetScenario(string scenario) => scenario switch
    {
        "Target7052Mode5" => ("Mode5", 7052),
        "Target7052ClaimAll" => ("ClaimAll", 7052),
        "Target7057Mode5" => ("Mode5", 7057),
        "Target7057ClaimAll" => ("ClaimAll", 7057),
        _ => (scenario, 7051), // Preserve every existing successful and fault scenario.
    };
}
