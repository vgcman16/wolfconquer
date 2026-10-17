using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static uint CalculateLegacyPlayerRangeStatusReturnDamage(
        WorldSession attacker,
        WorldSession target,
        WorldDataCatalog? worldData)
    {
        var attackerFields = CalculateLegacyRuntimeStatusFields(attacker, worldData);
        var targetFields = CalculateLegacyRuntimeStatusFields(target, worldData);
        return LegacyCombatDamageCalculator.CalculateLegacyExtraAttack(
            0,
            attackerFields[LegacyPlayerStatusPacket.PhysicalDamageIncreaseIndex],
            targetFields[LegacyPlayerStatusPacket.PhysicalDamageDecreaseIndex]);
    }
}
