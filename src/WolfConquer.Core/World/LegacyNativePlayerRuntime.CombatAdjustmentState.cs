namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Exact native physical fields375/731..737/1015/1275; no attribute projections.
    public uint HookMoonAttackedPower;
    public bool OnAttackPotion = false;
    public DateTime OnAttackPotionStamp = default;
    public bool OnDefensePotion = false;
    public DateTime OnDefensePotionStamp = default;
    public int IntensifyDamage = 0;
    public bool doFocus = false;
}
