using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Pure finite catalog input for the actual Frame fixture, without widening
    // any production admission API or replacing an owner/persistence operation.
    internal static WorldDataCatalog CreateMagicCostFrameUnboundCatalog() =>
        CreateMagicCatalogCore(CreateMonsterFamily(), [], [],
            [CreateMagicRow(1000, sort: 1, useMana: 50, useStamina: 10, damage: 100, range: 4, coldTime: 1000)]);
}
