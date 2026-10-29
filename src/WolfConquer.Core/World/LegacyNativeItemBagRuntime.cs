using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// The two native bags keep actual item aliases and uint index keys while sharing
// the real synchronized collection owner. Item eligibility/UID rules belong to consumers.
internal class LegacyNativeItemBagRuntime : LegacyNativeDbSafeDictionary<uint, LegacyNativeGameItemRuntime>
{
    public LegacyNativeItemBagRuntime() { }
    public LegacyNativeItemBagRuntime(int cap) : base(cap) { }
}