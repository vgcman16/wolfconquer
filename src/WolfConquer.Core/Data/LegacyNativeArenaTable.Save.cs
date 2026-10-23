using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

// Native ArenaTable really has no fields, explicit ctor or type initializer.
// Whole native Save8..16 is ready independently; Load/Reset remain typed-held.
internal partial class LegacyNativeArenaTable
{
    internal void Save()
    {
        using LegacyNativeDbWrite write = new LegacyNativeDbWrite("Arena.ini");
        foreach (LegacyNativeArenaUser value in LegacyNativeArenaRankings.ArenaPoll.Values)
        {
            write.Add(value.ToString());
        }
        write.Execute(LegacyNativeDbWriteMode.Open);
    }
}
