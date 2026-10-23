namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player975/977, exact default prestate.
    public DateTime Protect = default(DateTime);
    private DateTime ProtectedJumpAttack = default(DateTime);

    // Whole native3447..3460.
    internal void ProtectAttack(int StampMiliSecounds)
    {
        Protect = DateTime.Now.AddMilliseconds(StampMiliSecounds);
    }
    internal void ProtectJumpAttack(int Secounds)
    {
        ProtectedJumpAttack = DateTime.Now.AddSeconds(Secounds);
    }
    internal bool AllowAttack()
    {
        return DateTime.Now >= Protect && DateTime.Now >= ProtectedJumpAttack;
    }
}
