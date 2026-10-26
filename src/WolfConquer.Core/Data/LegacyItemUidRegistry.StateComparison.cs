namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    // Record equality alone compares array references, whereas disk readback
    // must compare every immutable receipt and all scalar domain fields.
    private static bool SameState(RegistryState? left, RegistryState? right)
    {
        if (left is null || right is null) return left is null && right is null;
        if ((left with { Operations = null }) != (right with { Operations = null })) return false;
        if (left.Operations is null || right.Operations is null)
            return left.Operations is null && right.Operations is null;
        return left.Operations.AsSpan().SequenceEqual(right.Operations);
    }

    private static bool IsNext(RegistryState before, RegistryState after)
    {
        if (before.Generation == ulong.MaxValue || after.Generation != before.Generation + 1 ||
            after.HighWater <= before.HighWater || after.HighWater - (ulong)before.HighWater > MaximumReservation)
            return false;
        var expected = before with { Generation = after.Generation, HighWater = after.HighWater };
        if (SameState(expected, after)) return true; // ordinary forward-only reservation
        if (after.Version != 2 || after.Operations is null ||
            (before.Version != 1 && before.Version != 2)) return false;
        var previousCount = before.Operations?.Length ?? 0;
        if (after.Operations.Length != previousCount + 1 ||
            previousCount > 0 && !before.Operations!.AsSpan().SequenceEqual(after.Operations.AsSpan(0, previousCount)))
            return false;
        var appended = after.Operations[^1];
        if (appended.Generation != after.Generation || appended.First != before.HighWater + 1 ||
            (uint)appended.Count != after.HighWater - before.HighWater) return false;
        return SameState(expected with { Version = 2, Operations = after.Operations }, after);
    }
}
