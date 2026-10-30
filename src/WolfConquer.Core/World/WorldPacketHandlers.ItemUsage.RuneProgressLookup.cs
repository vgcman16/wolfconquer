using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    // ChanceRune retains its separate existing metadata path. Owned Compose
    // uses its closed adopted-table witness and recorded used rows instead.
    private static bool TryGetLegacyRuneLevelRequiredProgress(WorldDataCatalog? worldData,
        uint runeFamily, uint runeLevel, out uint requiredProgress)
    {
        requiredProgress = 0;
        if (worldData is null || runeFamily > int.MaxValue || runeLevel > int.MaxValue ||
            !worldData.RuneLevelExperienceByRuneTypeAndLevel.TryGetValue((checked((int)runeFamily), checked((int)runeLevel)), out var row) ||
            row.Experience is < 0 or > uint.MaxValue) return false;
        requiredProgress = checked((uint)row.Experience); return true;
    }

    private static byte GetLegacyMaxRuneLevel(uint itemId) => (itemId / 10_000) switch
    { 401 => 1, 403 => 9, _ => 27 };

    private static bool IsLegacyRuneComposeMaterialItemId(uint itemId) => itemId is >= 4_010_000 and <= 4_100_000;
}
