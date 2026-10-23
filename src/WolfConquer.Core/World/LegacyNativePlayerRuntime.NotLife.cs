namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native Player1560..1570. The setter really throws in the source;
    // do not reinterpret it as a writable inverse of Alive/HitPoints.
    public bool NotLife
    {
        get
        {
            return HitPoints < 1;
        }
        set
        {
            throw new NotImplementedException();
        }
    }
}
