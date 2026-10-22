using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Genuine native Pool66 physical field and initializer. No loader/startup call.
// DBLevExp's complete row/Sort declaration already exists and is reused unchanged.
internal static class LegacyNativePoolLevelInfo
{
    public static Dictionary<LegacyNativeDbLevExp.Sort, Dictionary<byte, LegacyNativeDbLevExp>> LevelInfo =
        new Dictionary<LegacyNativeDbLevExp.Sort, Dictionary<byte, LegacyNativeDbLevExp>>();
}
