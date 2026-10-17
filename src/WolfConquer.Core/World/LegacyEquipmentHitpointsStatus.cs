namespace WolfConquer.Core.World;

// Equipment-query results are cached Status, not a live current-HP floor.
// This transient cache is separate from the independent FineRain admission stamp.
public sealed record LegacyEquipmentHitpointsStatus(long OwnerUid, uint MaximumHitpoints)
{
    public static bool TryGetForOwner(WorldSession session, out uint maximumHitpoints)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.EquipmentHitpointsStatus is { } status && status.OwnerUid == session.Character.Uid)
        {
            maximumHitpoints = status.MaximumHitpoints;
            return true;
        }

        maximumHitpoints = 0;
        return false;
    }
}
