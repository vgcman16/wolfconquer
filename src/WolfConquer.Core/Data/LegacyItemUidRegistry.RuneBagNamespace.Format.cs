namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal const string RuneBagWriter = "rune-bag-return-v1";
    private const string RuneBagNamespaceKind = "RuneBagReturn";

    private static bool IsRuneBagNamespace(OperationReservation row) =>
        row.NamespaceReservation is { Version: 1, Kind: RuneBagNamespaceKind } && row.Writer == RuneBagWriter && row.Count == 1;

    private static bool IsRuneBagNamespaceAppend(RegistryState before, RegistryState after, RegistryState expected)
    {
        var previous = before.Operations ?? [];
        if (after.Version != Math.Max(6, before.Version) || after.HighWater - (ulong)before.HighWater != 1 || after.Operations is null ||
            after.Operations.Length != previous.Length + 1 ||
            !previous.AsSpan().SequenceEqual(after.Operations.AsSpan(0, previous.Length))) return false;
        var added = after.Operations[^1];
        return IsRuneBagNamespace(added) && added.Generation == after.Generation && added.First == before.HighWater + 1 &&
            SameState(expected with { Version = Math.Max(6, before.Version), Operations = after.Operations,
                ProducerIntents = before.ProducerIntents ?? [] }, after);
    }

    private static bool IsRuneBagNamespaceRetirement(RegistryState before, RegistryState after, RegistryState expected)
    {
        if (before.Version is not (6 or 7) || after.Version != before.Version || after.HighWater != before.HighWater ||
            before.Operations is null || after.Operations is null || before.Operations.Length != after.Operations.Length + 1)
            return false;
        var removed = false; var next = 0;
        foreach (var row in before.Operations)
        {
            if (next < after.Operations.Length && row == after.Operations[next]) next++;
            else if (!removed && IsRuneBagNamespace(row)) removed = true;
            else return false;
        }
        return removed && SameState(expected with { Operations = after.Operations }, after);
    }
}
