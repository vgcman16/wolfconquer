using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial record LegacyRuneTransformDependency
{
    internal CharacterSnapshot ApplyTo(CharacterSnapshot character)
    {
        ArgumentNullException.ThrowIfNull(character);
        Validate();
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in character.Attributes)
            if (!Keys.Contains(pair.Key)) attributes.Add(pair.Key, pair.Value);
        foreach (var field in fields) attributes.Add(field.Key, field.Value);
        // Actual LegacyTransformState parsing and alias priority derive the cap.
        // This temporary view is not the persisted Character after-image.
        return character with { Attributes = attributes };
    }

    internal bool SameAs(LegacyRuneTransformDependency? other)
    {
        if (other is null || fields.Length != other.fields.Length) return false;
        for (var index = 0; index < fields.Length; index++)
            if (fields[index] is not { } field || other.fields[index] is not { } expected ||
                field.Key != expected.Key || field.Value != expected.Value) return false;
        return true;
    }
}
