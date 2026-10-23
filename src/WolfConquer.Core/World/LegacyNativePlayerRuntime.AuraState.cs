using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player981..985. Physical zero/null owner, not an aura acquisition producer.
    internal LegacyNativeMsgUpdate.Flags UseAura = LegacyNativeMsgUpdate.Flags.Normal;
    internal LegacyNativeMagicType.Magic? Aura;
    public int AuraTimer = 0;
}
