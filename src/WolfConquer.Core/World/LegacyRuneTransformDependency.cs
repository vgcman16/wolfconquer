using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed record LegacyRuneTransformField(string Key, string Value);

// Historical current inputs, never an accepted after-HP value or live authority.
// The real Rune producer captures this from its Store-owned stable input image.
internal sealed partial record LegacyRuneTransformDependency
{
    private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        LegacyTransformState.TransformationIdAttribute,
        LegacyTransformState.TransformationIdAliasAttribute,
        LegacyTransformState.ActiveAttribute,
        LegacyTransformState.ActiveAliasAttribute,
        LegacyTransformState.ExpiresAtUtcAttribute,
        LegacyTransformState.ExpiresAtUtcAliasAttribute,
        LegacyTransformState.TransformationExpiresAtUtcAliasAttribute,
        LegacyTransformState.MaxHitpointsAttribute,
        LegacyTransformState.MaxHitpointsAliasAttribute,
    };
    private readonly LegacyRuneTransformField[] fields;

    // JSON exposes only the closed field records. Neither constructor input nor
    // a returned array can rewrite a captured dependency after preparation.
    public LegacyRuneTransformField[] Fields => fields.ToArray();

    internal LegacyRuneTransformDependency(LegacyRuneTransformField[] fields)
    {
        this.fields = fields?.ToArray() ?? throw Invalid();
    }

    internal static LegacyRuneTransformDependency Capture(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (character.Attributes is null) throw Invalid();
        var result = new LegacyRuneTransformDependency(character.Attributes
            .Where(pair => Keys.Contains(pair.Key))
            .Select(pair => new LegacyRuneTransformField(pair.Key, pair.Value))
            .OrderBy(row => row.Key, StringComparer.Ordinal).ToArray());
        result.Validate();
        return result;
    }

    internal void Validate()
    {
        if (fields.Length > Keys.Count) throw Invalid();
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? previous = null;
        long length = 0;
        foreach (var field in fields)
        {
            if (field is null || field.Key is null || field.Value is null || !Keys.Contains(field.Key) ||
                !present.Add(field.Key) || previous is not null && StringComparer.Ordinal.Compare(previous, field.Key) >= 0)
                throw Invalid();
            length += (long)field.Key.Length + field.Value.Length;
            if (length > CharacterSnapshotFileBaseline.MaximumBytes) throw Invalid();
            previous = field.Key;
        }
    }

    private static InvalidDataException Invalid() => new("Rune transform dependency requires complete closed current field presence and raw text.");
}
