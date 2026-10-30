using System.Collections.Concurrent;
namespace WolfConquer.Core.World;

// Native trade state; currency admission requires a real durable escrow owner.
internal partial class LegacyNativeTradeRuntime
{
    public readonly LegacyNativeGameClientRuntime Owner;
    public LegacyNativeGameClientRuntime? Target;
    public uint ConquerPoints;
    public long Money;
    public bool WindowOpen;
    public bool Confirmed;
    public readonly ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Items;

    public LegacyNativeTradeRuntime(LegacyNativeGameClientRuntime _owner)
    {
        ArgumentNullException.ThrowIfNull(_owner);
        Owner = _owner;
        Items = new ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>();
    }
}
