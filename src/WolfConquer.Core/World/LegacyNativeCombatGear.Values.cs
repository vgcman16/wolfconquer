#nullable disable
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    // Complete native CombatGear1507..1533; retain split ContainsKey/TryAdd/indexer behavior.
    public void SetValue(string type, params double[] data)
    {
        if (LegacyNativeCombatGearTable.m_Load)
        {
            if (!Info.ContainsKey(type))
            {
                Info.TryAdd(type, data);
            }
            else
            {
                Info[type] = data;
            }
        }
    }

    public double[] GetValue(string type)
    {
        if (!LegacyNativeCombatGearTable.m_Load)
        {
            return null;
        }
        if (Info.ContainsKey(type))
        {
            return Info[type];
        }
        return new double[0];
    }
}
