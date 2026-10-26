namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private static bool SameState(RegistryState? left, RegistryState? right)
    {
        if (left is null || right is null) return left is null && right is null;
        if ((left with { Operations = null, ProducerIntents = null }) !=
            (right with { Operations = null, ProducerIntents = null })) return false;
        return SameRows(left.Operations, right.Operations) && SameRows(left.ProducerIntents, right.ProducerIntents);
    }

    private static bool SameRows<T>(T[]? left, T[]? right) where T : IEquatable<T> => left is null || right is null
        ? left is null && right is null : left.AsSpan().SequenceEqual(right);

    private static bool IsNext(RegistryState before, RegistryState after)
    {
        if (before.Generation == ulong.MaxValue || after.Generation != before.Generation + 1 ||
            after.HighWater < before.HighWater || after.HighWater - (ulong)before.HighWater > MaximumReservation)
            return false;
        var expected = before with { Generation = after.Generation, HighWater = after.HighWater };
        if (after.HighWater > before.HighWater && SameState(expected, after)) return true;
        if (IsProducerIntentAppend(before, after, expected)) return true;
        if (after.HighWater == before.HighWater || after.Version != Math.Max(2, before.Version) ||
            after.Operations is null) return false;
        var previousCount = before.Operations?.Length ?? 0;
        if (after.Operations.Length != previousCount + 1 ||
            previousCount > 0 && !before.Operations!.AsSpan().SequenceEqual(after.Operations.AsSpan(0, previousCount)))
            return false;
        var appended = after.Operations[^1];
        if (appended.Generation != after.Generation || appended.First != before.HighWater + 1 ||
            (uint)appended.Count != after.HighWater - before.HighWater) return false;
        return SameState(expected with { Version = Math.Max(2, before.Version), Operations = after.Operations }, after);
    }

    private static bool IsProducerIntentAppend(RegistryState before, RegistryState after, RegistryState expected)
    {
        if (after.Version != 3 || after.HighWater != before.HighWater || after.ProducerIntents is null) return false;
        var previousCount = before.ProducerIntents?.Length ?? 0;
        if (after.ProducerIntents.Length != previousCount + 1 ||
            previousCount > 0 && !before.ProducerIntents!.AsSpan().SequenceEqual(after.ProducerIntents.AsSpan(0, previousCount)) ||
            after.ProducerIntents[^1].PreparedGeneration != after.Generation) return false;
        return SameState(expected with { Version = 3, Operations = before.Operations ?? [],
            ProducerIntents = after.ProducerIntents }, after);
    }
}
