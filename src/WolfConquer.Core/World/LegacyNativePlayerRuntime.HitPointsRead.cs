namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Exact native Player1147 physical zero-default storage and whole getter2609..2612.
    // READ-ONLY prerequisite: setter2613..2630 is whole held until real Team/HP-send graph exists.
    // No writable facade or host/DTO HP projection is supplied.
    private int _minhitpoints;
    public int HitPoints
    {
        get { return _minhitpoints; }
    }

    // Complete native Player2846. This is not the full mutable HP lifecycle.
    public bool Alive => HitPoints > 0;
}
