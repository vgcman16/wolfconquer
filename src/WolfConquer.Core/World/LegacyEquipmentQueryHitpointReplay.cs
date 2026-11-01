namespace WolfConquer.Core.World;

// DATA arithmetic only. This cannot publish a query, mutate an owner or admit an action.
internal sealed class LegacyEquipmentQueryHitpointReplay
{
    internal int Current { get; private set; }
    internal uint Maximum { get; private set; }

    internal LegacyEquipmentQueryHitpointReplay(long current) => Current = LegacyEquipmentQueryBounds.Hitpoints(current);
    internal void AddMaximum(uint addition) => Maximum = checked(Maximum + addition);
    internal void AddCurrent(uint addition) => Current = LegacyEquipmentQueryBounds.AddHitpoints(Current, addition);
    internal void ReduceMaximum(uint percent)
    {
        Maximum = LegacyEquipmentQueryBounds.ReduceHitpoints(Maximum, percent);
        if (Current > Maximum) Current = checked((int)Maximum);
    }
    internal void ClampCurrent() => Current = LegacyEquipmentQueryBounds.ClampHitpoints(Current, Maximum);
    internal void Transform(uint maximum)
    {
        // The real pipeline performs its ordinary clamp BEFORE this absolute setter.
        Current = checked((int)maximum);
        Maximum = maximum;
    }
}
