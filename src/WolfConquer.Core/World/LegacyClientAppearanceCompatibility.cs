namespace WolfConquer.Core.World;

internal static class LegacyClientAppearanceCompatibility
{
    public static uint ResolveMonsterMesh(
        uint mesh,
        string name,
        LegacyClientCompatibilityOptions? clientCompatibility)
    {
        if (clientCompatibility?.EnableNewerClientAppearanceMapping != true)
        {
            return mesh;
        }

        return mesh switch
        {
            // The staged 7896 client has adjacent replacement folders for these
            // Twin City monkey meshes, but no c3/monster/156 or c3/monster/556.
            156 => 157,
            556 => 557,
            _ => mesh,
        };
    }
}
