using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyAuthenticatedCharacterCreationProjection
{
    private static byte[] SpellImage(long code)
    {
        ushort[] ids = code switch
        {
            1000 => [1046], 2000 => [1020, 1015, 1040], 4000 => [8002], 5000 => [6001], 6000 => [10390],
            7000 => [11050], 8000 => [12210], 9000 => [15670], 10000 => [1000, 1005], 16000 => [12870], _ => throw Invalid(),
        };
        return PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(ids.Select(id =>
            new PlayerSpellSnapshotEntry(id, Level: 0, Experience: 0, PreviousLevel: 0, SoulLevel: 0, UseSpellSoul: 0)).ToArray()));
    }
}
