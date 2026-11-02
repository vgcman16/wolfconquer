namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private const string RelicDecomposeNamespaceKind = "RelicDecompose";
    private static bool IsRelicDecomposeNamespace(OperationReservation row) =>
        row.NamespaceReservation is { Version: 1, Kind: RelicDecomposeNamespaceKind } && row.Writer == RelicDecomposeWriter && row.Count is >= 9 and <= 441 && row.Count % 9 == 0;

    private static bool IsRelicDecomposeNamespaceAppend(RegistryState before, RegistryState after, RegistryState expected)
    {
        var previous = before.Operations ?? [];
        if (after.Version != Math.Max(8, before.Version) || after.HighWater - (ulong)before.HighWater is < 9 or > 441 || after.Operations is null ||
            after.Operations.Length != previous.Length + 1 ||
            !previous.AsSpan().SequenceEqual(after.Operations.AsSpan(0, previous.Length))) return false;
        var added = after.Operations[^1];
        return IsRelicDecomposeNamespace(added) && added.Generation == after.Generation && added.First == before.HighWater + 1 &&
            (uint)added.Count == after.HighWater - before.HighWater &&
            SameState(expected with { Version = Math.Max(8, before.Version), Operations = after.Operations,
                ProducerIntents = before.ProducerIntents ?? [] }, after);
    }

    private static bool IsRelicDecomposeNamespaceRetirement(RegistryState before, RegistryState after, RegistryState expected)
    {
        if (before.Version != 8 || after.Version != 8 || after.HighWater != before.HighWater ||
            before.Operations is null || after.Operations is null || before.Operations.Length != after.Operations.Length + 1) return false;
        var removed = false; var next = 0;
        foreach (var row in before.Operations)
        {
            if (next < after.Operations.Length && row == after.Operations[next]) next++;
            else if (!removed && IsRelicDecomposeNamespace(row)) removed = true;
            else return false;
        }
        return removed && SameState(expected with { Operations = after.Operations }, after);
    }
}
