namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Exact native Player1147 physical zero-default storage and whole getter2609..2612.
    // The original public setter/Team-send graph remains held. Genuine ordinary
    // Store/admission publication uses the separate prepared private scalar path;
    // this getter is not a managed-state alias or independent owner authority.
    private int _minhitpoints;
    public int HitPoints
    {
        get { return _minhitpoints; }
    }

    // Complete native Player2846. This is not the full mutable HP lifecycle.
    public bool Alive => HitPoints > 0;
}
