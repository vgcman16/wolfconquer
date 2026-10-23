namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Native Player455/979/989/991, actual physical state not capability snapshots.
    public bool AttackHit = false;
    public uint ShieldBlockDamage = 0u;
    public DateTime AttackStamp = default(DateTime);
    public DateTime SpellAttackStamp = default(DateTime);
}

public sealed partial class LegacyNativeGameClientRuntime
{
    // Native GameClient233/235. No scheduler/host caller is installed.
    internal LegacyNativeInteractQuery? AutoAttack = null;
    public bool OnAutoAttack = false;
}
