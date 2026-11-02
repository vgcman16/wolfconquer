namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    private const string RelicFuseNamespaceKind = "RelicFuse";
    private static bool IsRelicFuseNamespace(OperationReservation row) =>
        row.NamespaceReservation is { Version: 1, Kind: RelicFuseNamespaceKind } && row.Writer == RelicFuseWriter && row.Count == 1;

    private static bool IsRelicFuseNamespaceAppend(RegistryState before, RegistryState after, RegistryState expected)
    {
        var previous = before.Operations ?? [];
        if (after.Version != 7 || after.HighWater - (ulong)before.HighWater != 1 || after.Operations is null ||
            after.Operations.Length != previous.Length + 1 ||
            !previous.AsSpan().SequenceEqual(after.Operations.AsSpan(0, previous.Length))) return false;
        var added = after.Operations[^1];
        return IsRelicFuseNamespace(added) && added.Generation == after.Generation && added.First == before.HighWater + 1 &&
            SameState(expected with { Version = 7, Operations = after.Operations,
                ProducerIntents = before.ProducerIntents ?? [] }, after);
    }

    private static bool IsRelicFuseNamespaceRetirement(RegistryState before, RegistryState after, RegistryState expected)
    {
        if (before.Version != 7 || after.Version != 7 || after.HighWater != before.HighWater ||
            before.Operations is null || after.Operations is null || before.Operations.Length != after.Operations.Length + 1) return false;
        var removed = false; var next = 0;
        foreach (var row in before.Operations)
        {
            if (next < after.Operations.Length && row == after.Operations[next]) next++;
            else if (!removed && IsRelicFuseNamespace(row)) removed = true;
            else return false;
        }
        return removed && SameState(expected with { Operations = after.Operations }, after);
    }
}
