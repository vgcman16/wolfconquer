namespace WolfConquer.Core.World;

// Complete native Mode declarations; both whole codecs are in the codec partial.
// No NPC processor, packet attribute, or host route is installed here.
internal static partial class LegacyNativeNpcServerReplay
{
    public enum Mode : ushort
    {
        Statue = 3,
        PlaceFurniture = 6,
        Cursor = 5
    }
}
