using System.Text.Json;

namespace WolfConquer.Core.Data;

internal sealed partial class LegacyItemUidRegistry
{
    internal const string RuneDecomposeWriter = "rune-decompose-v1";
    private sealed record NamespaceReservation(int Version, string Kind);
    private const string RuneDecomposeNamespaceKind = "RuneDecompose";

    private static bool IsRuneDecomposeNamespace(OperationReservation row) =>
        row.NamespaceReservation is { Version: 1, Kind: RuneDecomposeNamespaceKind } &&
        row.Writer == RuneDecomposeWriter;

    private static void ValidateNamespaceReservationJson(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException("UID namespace reservation must be an object.");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (property.Name is not ("Version" or "Kind") || !names.Add(property.Name))
                throw new JsonException("Unknown or duplicate UID namespace reservation property.");
        if (names.Count != 2 || value.GetProperty("Version").ValueKind != JsonValueKind.Number ||
            !value.GetProperty("Version").TryGetInt32(out var version) || version != 1 ||
            value.GetProperty("Kind").ValueKind != JsonValueKind.String ||
            value.GetProperty("Kind").GetString() != RuneDecomposeNamespaceKind)
            throw new JsonException("Unsupported UID namespace reservation kind or version.");
    }

    private static bool IsRuneDecomposeNamespaceAppend(RegistryState before, RegistryState after, RegistryState expected)
    {
        var previous = before.Operations ?? [];
        if (after.Version != 5 || after.HighWater == before.HighWater || after.Operations is null ||
            after.Operations.Length != previous.Length + 1 ||
            !previous.AsSpan().SequenceEqual(after.Operations.AsSpan(0, previous.Length))) return false;
        var added = after.Operations[^1];
        return IsRuneDecomposeNamespace(added) && added.Generation == after.Generation &&
            added.First == before.HighWater + 1 && (uint)added.Count == after.HighWater - before.HighWater &&
            SameState(expected with { Version = 5, Operations = after.Operations,
                ProducerIntents = before.ProducerIntents ?? [] }, after);
    }

    private static bool IsRuneDecomposeNamespaceRetirement(RegistryState before, RegistryState after, RegistryState expected)
    {
        if (before.Version != 5 || after.Version != 5 || after.HighWater != before.HighWater ||
            before.Operations is null || after.Operations is null ||
            before.Operations.Length != after.Operations.Length + 1) return false;
        var removed = -1;
        var next = 0;
        for (var index = 0; index < before.Operations.Length; index++)
        {
            if (next < after.Operations.Length && before.Operations[index] == after.Operations[next]) next++;
            else if (removed < 0 && IsRuneDecomposeNamespace(before.Operations[index])) removed = index;
            else return false;
        }
        return removed >= 0 && SameState(expected with { Operations = after.Operations }, after);
    }
}
