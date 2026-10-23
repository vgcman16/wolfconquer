using System.Collections.Concurrent;
namespace WolfConquer.Core.World;

// Complete native Trade physical state and constructor9..29, not an exchange facade.
internal partial class LegacyNativeTradeRuntime
{
    public LegacyNativeGameClientRuntime Owner;
    public LegacyNativeGameClientRuntime? Target;
    public uint ConquerPoints;
    public long Money;
    public bool WindowOpen;
    public bool Confirmed;
    public ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Items;

    public LegacyNativeTradeRuntime(LegacyNativeGameClientRuntime _owner)
    {
        Owner = _owner;
        Items = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
    }
}
