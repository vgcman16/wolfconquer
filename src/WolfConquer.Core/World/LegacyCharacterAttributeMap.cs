namespace WolfConquer.Core.World;

// Proposed DATA only. Actual session/native/persistence owners still decide
// whether any resulting projection may commit.
internal static class LegacyCharacterAttributeMap
{
    internal static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> source)
    {
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(source);
        var copy = new Dictionary<string, string>(proof.Fields, proof.CapturedComparer);
        if (!proof.IsCurrentPlain()) throw new InvalidDataException("characterAttributeSourceChanged");
        return copy;
    }

    internal static void Set(Dictionary<string, string> proposed, string key, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        proposed[Resolve(proposed, key) ?? key] = value;
    }

    internal static bool Remove(Dictionary<string, string> proposed, string key)
    {
        var actual = Resolve(proposed, key);
        return actual is not null && proposed.Remove(actual);
    }

    internal static bool TryRead(Dictionary<string, string> proposed, string key, out string value)
    {
        var actual = Resolve(proposed, key);
        value = actual is null ? string.Empty : proposed[actual];
        return actual is not null;
    }

    // Initial DATA validation before an effectful outer operation. This is not
    // a lease: its later real commit still requires current owner/file proofs.
    internal static void Validate(IReadOnlyDictionary<string, string> source, params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        var copy = Copy(source);
        foreach (var key in keys) _ = Resolve(copy, key);
    }

    private static string? Resolve(Dictionary<string, string> proposed, string key)
    {
        ArgumentNullException.ThrowIfNull(proposed);
        ArgumentException.ThrowIfNullOrEmpty(key);
        var comparer = proposed.Comparer;
        if (proposed.GetType() != typeof(Dictionary<string, string>) ||
            !ReferenceEquals(comparer, StringComparer.Ordinal) && !ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase) &&
            !ReferenceEquals(comparer, EqualityComparer<string>.Default))
            throw new InvalidOperationException("characterAttributeProposedMapUnsupported");
        string? match = null;
        foreach (var candidate in proposed.Keys)
            if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
            {
                if (match is not null) throw new InvalidDataException("characterAttributeOwnedFieldAmbiguous");
                match = candidate;
            }
        return match;
    }
}
