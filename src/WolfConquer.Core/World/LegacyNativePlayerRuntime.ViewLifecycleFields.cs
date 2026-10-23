using System.Collections.Concurrent;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Exact source327 initializer. Reuse this sole slot during later ordered birth;
    // frozen birth's duplicate proposal must not be intaken as a second declaration.
    internal ConcurrentDictionary<uint, LegacyNativeMonsterRole> ListPets = new();

    // Native941/943/951, preserving actual struct default and nullable client ref.
    public bool OnFairy = false;
    internal LegacyNativeMsgTransformFairy FairySpawn;
    public LegacyNativeGameClientRuntime? ObjInteraction;
}
