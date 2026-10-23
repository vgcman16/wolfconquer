namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
    // Whole native Player4611..4624. Player's threshold differs from MonsterRole.
    public short GetMyDistance(ushort X2, ushort Y2)
    {
        return LegacyNativeRoleCore.GetDistance(X, Y, X2, Y2);
    }

    public short OldGetDistance(ushort X2, ushort Y2)
    {
        return LegacyNativeRoleCore.GetDistance(Px, Py, X2, Y2);
    }

    public bool InView(ushort X2, ushort Y2, byte distance)
    {
        return OldGetDistance(X2, Y2) > distance && GetMyDistance(X2, Y2) <= distance;
    }
}
