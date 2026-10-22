using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static CombatGear CreateNativeOrdinaryMagicNeutralLoadedGear(int type)
    {
        // Declared custom catalog input, NOT a claim to reproduce stock gear stats.
        // Native CQ_COMBAT_GEAR accepts Type/Level; Load requires that pair before
        // retaining an equipped raw item. All stats, ten native skills and score0.
        var fields = Enumerable.Repeat("0", 23).ToArray();
        fields[0] = type.ToString(CultureInfo.InvariantCulture);
        fields[1] = fields[0];
        fields[2] = "1";
        fields[3] = "100";
        fields[^1] = "";
        return CombatGearParser.ParseRow("fixture://native-ordinary-magic-neutral-loaded-" + fields[0],
            1, string.Join("@@", fields));
    }
}
