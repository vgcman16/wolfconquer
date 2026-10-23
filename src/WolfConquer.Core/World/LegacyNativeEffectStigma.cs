using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

internal class LegacyNativeEffectStigma
{
    public static void Execute(LegacyNativeInteractQuery? Attack, LegacyNativeGameClientRuntime user,
        LegacyNativePacket stream, Dictionary<ushort, LegacyNativeMagicType.Magic>? DBSpells)
        => LegacyNativeSelfStatusEffect.TryExecute(user, stream, stigma: true);
}
