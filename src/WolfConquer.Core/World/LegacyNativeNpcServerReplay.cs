namespace WolfConquer.Core.World;

// Complete native Mode enum prerequisite only. Both NpcServerReplay codecs remain absent
// pending their genuine NpcServerQuery/NpcID/NpcType graph; no alternate query type is used.
internal static class LegacyNativeNpcServerReplay
{
    public enum Mode : ushort
    {
        Statue = 3,
        PlaceFurniture = 6,
        Cursor = 5
    }
}
