namespace WolfConquer.Core.World;

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient241/245, same physical fields/default initializers.
    internal LegacyNativeMsgLoginClient OnLogin = default(LegacyNativeMsgLoginClient);

    internal LegacyNativeServerFlag ClientFlag = LegacyNativeServerFlag.None;
}
