using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("-1")] [InlineData("not-a-number")]
    public async Task OwnedRootFrameMalformedStaminaStopsBeforeManualPrefixAuthorizationOrAnyOutput(string raw)
    {
        var inputs = OwnedRootMagicInputs("manual");
        var actor = inputs.Owner with { Character = inputs.Owner.Character with
        {
            Attributes = new Dictionary<string, string>(inputs.Owner.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["Stamina"] = raw },
        } };
        // Retain the exact issued equipment/catalog premise, but malformed
        // saved stamina now refuses BEFORE a251 prefix could earn effects.
        await RunRejectedStaminaInputs(actor, inputs.Peer, inputs.World, raw, first: null, spell: 1000, allowAnimation: true);
    }
}
