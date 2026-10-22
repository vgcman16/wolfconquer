using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    // Complete native CombatGear927..938. Metadata readiness remains its explicit gate.
    public static bool CheckSpells(ushort ID)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return false;
        }
        if (Enum.IsDefined(typeof(SpellID), ID))
        {
            return true;
        }
        return false;
    }
}
