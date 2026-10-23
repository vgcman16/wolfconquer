namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player873 physical mutable list; one owner per genuine Player birth.
    internal List<LegacyNativeClone> MyClones = new List<LegacyNativeClone>();
}
