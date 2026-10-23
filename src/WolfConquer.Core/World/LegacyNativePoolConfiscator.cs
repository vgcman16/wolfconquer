using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static class LegacyNativePoolConfiscator
{
    // Native Pool116: genuine no-worker ConfiscatorTable implicit birth, not delayed/null authority.
    public static LegacyNativeConfiscatorTable QueueContainer = new LegacyNativeConfiscatorTable();
}
