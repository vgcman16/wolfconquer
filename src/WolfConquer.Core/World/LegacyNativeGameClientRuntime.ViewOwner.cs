namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient255. Only actual native map adoption may publish this reference.
    // No host terrain/catalog/session-derived map or constructor-created View is installed.
    internal LegacyNativeGameMap? Map = null;
}
