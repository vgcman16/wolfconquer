using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player961..965 physical declarations. No automatic subclass birth
    // or DataCore caller; internal only where genuine native owner types require.
    internal LegacyNativeSubClassRuntime? SubClass;

    public uint SubClassHasPoints;

    internal LegacyNativeDbLevExp.Sort ActiveSublass;
}
