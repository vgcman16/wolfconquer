using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Native TeamArenaTable has genuine implicit no-state birth, not eager worker birth.
// Whole native Save8..16 only; dependent full Load/Reset are separately held.
internal partial class LegacyNativeTeamArenaTable
{
    internal void Save()
    {
        using LegacyNativeDbWrite write = new LegacyNativeDbWrite("TeamArena.ini");
        foreach (LegacyNativeTeamArenaUser value in LegacyNativeTeamArenaRankings.ArenaPoll.Values)
        {
            write.Add(value.ToString());
        }
        write.Execute(LegacyNativeDbWriteMode.Open);
    }
}
