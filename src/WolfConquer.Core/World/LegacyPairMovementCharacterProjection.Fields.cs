namespace WolfConquer.Core.World;

internal static partial class LegacyPairMovementCharacterProjection
{
    private readonly record struct Field(int Start, int Length, string Value);

    // IniDocument's complete parser validates ordinary syntax. This scanner
    // additionally rejects ambiguous Character keys/sections and retains exact
    // numeric spans, including all surrounding whitespace and line endings.
    private static Dictionary<string, Field> ReadFields(string text)
    {
        var fields = new Dictionary<string, Field>(StringComparer.OrdinalIgnoreCase);
        bool inside = false, found = false;
        for (int start = 0; start < text.Length;)
        {
            int delimiter = text.AsSpan(start).IndexOfAny('\r', '\n');
            int end = delimiter < 0 ? text.Length : start + delimiter;
            var line = text.AsSpan(start, end - start); var trimmed = line.Trim();
            if (!trimmed.IsEmpty && trimmed[0] is not (';' or '#'))
            {
                if (trimmed[0] == '[' && trimmed[^1] == ']')
                {
                    inside = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                    if (inside)
                    {
                        if (found) throw new InvalidDataException("pairMovementCharacterSectionAmbiguous");
                        found = true;
                    }
                }
                else if (inside) AddField(fields, line, start);
            }
            start = end;
            if (start < text.Length && text[start++] == '\r' && start < text.Length && text[start] == '\n')
                start++;
        }
        if (!found) throw new InvalidDataException("pairMovementCharacterSectionMissing");
        return fields;
    }

    private static void AddField(Dictionary<string, Field> fields, ReadOnlySpan<char> line, int absoluteStart)
    {
        int equals = line.IndexOf('=');
        if (equals <= 0) throw new InvalidDataException("pairMovementCharacterFieldInvalid");
        var name = line[..equals].Trim().ToString();
        int first = equals + 1, end = line.Length;
        while (first < end && char.IsWhiteSpace(line[first])) first++;
        while (end > first && char.IsWhiteSpace(line[end - 1])) end--;
        if (name.Length == 0 || !fields.TryAdd(name,
                new(absoluteStart + first, end - first, line[first..end].ToString())))
            throw new InvalidDataException("pairMovementCharacterKeyAmbiguous");
    }
}
