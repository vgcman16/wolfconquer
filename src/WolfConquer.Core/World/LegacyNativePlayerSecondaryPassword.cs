namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player823..829. Physical Player birth starts unverified; the
    // accepted login loader and MsgSecondaryPassword are the actual writers.
    public bool IsCheckedPass = false;
    public uint SecurityPassword = 0u;
    public uint OnReset = 0u;
    public DateTime ResetSecurityPassowrd = default;
}
