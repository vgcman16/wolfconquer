using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class OwnedEquipmentBehaviorFixture
{
    // Actual constructor DATA only: all admission, native19 metadata, saved
    // files, connection lifecycle and runtime ownership remain RunActualAsync.
    internal static Task RunWithNativeGearTerrainAsync(OwnedEquipmentBehaviorInput actor, WorldDataCatalog catalog,
        LegacyRuntimeUpdateLoopOptions options, LegacyGameMapTerrainQuery terrain,
        Func<OwnedEquipmentBehaviorFixture, Task> exercise, params OwnedEquipmentBehaviorInput[] peers)
    {
        ArgumentNullException.ThrowIfNull(options); ArgumentNullException.ThrowIfNull(terrain);
        if (options.Enabled || options.TimeProvider is null || options.TickInterval <= TimeSpan.Zero)
            throw new ArgumentException("Native gear terrain fixtures require disabled scheduling and a real clock.", nameof(options));
        return RunActualAsync(actor, catalog, options, exercise, peers, useNativeGear: true, terrain: terrain);
    }
}
