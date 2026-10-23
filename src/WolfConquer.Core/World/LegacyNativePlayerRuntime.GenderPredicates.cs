namespace WolfConquer.Core.World;
public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native Player2871..2879, using the actual Body property.
    public bool IsBoy() { return LegacyNativeRoleCore.IsBoy(Body); }
    public bool IsGirl() { return LegacyNativeRoleCore.IsGirl(Body); }
}
