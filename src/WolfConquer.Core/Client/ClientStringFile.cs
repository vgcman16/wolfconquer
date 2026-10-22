using System.Text;

namespace WolfConquer.Core.Client;

public sealed class ClientStringFile
{
    private readonly List<ClientTextLine> _lines;
    private readonly string[] _lineCommentPrefixes;

    private ClientStringFile(Encoding encoding, List<ClientTextLine> lines, string[] lineCommentPrefixes)
    {
        Encoding = encoding;
        _lines = lines;
        _lineCommentPrefixes = lineCommentPrefixes;
    }

    public Encoding Encoding { get; }

    public IReadOnlyList<ClientTextLine> Lines => _lines;

    public static ClientStringFile Parse(byte[] bytes, Encoding encoding, params string[] lineCommentPrefixes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(encoding);

        return new ClientStringFile(
            encoding,
            SplitLines(encoding.GetString(bytes)),
            lineCommentPrefixes.Length == 0 ? [";", "#"] : lineCommentPrefixes);
    }

    public IReadOnlyList<ClientKeyValue> KeyValues()
    {
        var values = new List<ClientKeyValue>();

        for (var index = 0; index < _lines.Count; index++)
        {
            if (TryParseKeyValue(_lines[index].Text, _lineCommentPrefixes, out var key, out var value, out var prefix, out var separator))
            {
                values.Add(new ClientKeyValue(index, key, value, prefix, separator));
            }
        }

        return values;
    }

    public bool TryGetValue(string key, out string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        foreach (var entry in KeyValues())
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(entry.Key, key))
            {
                value = entry.Value;
                return true;
            }
        }

        value = string.Empty;
        return false;
    }

    public bool SetValue(string key, string value)
    {
        return SetValue(key, value, occurrence: 1);
    }

    public bool SetValue(string key, string value, int occurrence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrence, 1);

        var matches = 0;
        for (var index = 0; index < _lines.Count; index++)
        {
            var text = _lines[index].Text;
            if (!TryParseKeyValue(text, _lineCommentPrefixes, out var parsedKey, out var currentValue, out var prefix, out var separator) ||
                !StringComparer.OrdinalIgnoreCase.Equals(parsedKey, key))
            {
                continue;
            }

            matches++;
            if (matches != occurrence)
            {
                continue;
            }

            _lines[index] = _lines[index] with { Text = prefix + parsedKey + NormalizeReplacementSeparator(separator, currentValue, value) + value };
            return true;
        }

        return false;
    }

    public bool SetLine(string key, string line)
    {
        return SetLine(key, line, occurrence: 1);
    }

    public bool SetLine(string key, string line, int occurrence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrence, 1);

        var matches = 0;
        for (var index = 0; index < _lines.Count; index++)
        {
            var text = _lines[index].Text;
            var prefix = string.Empty;
            if (TryParseKeyValue(text, _lineCommentPrefixes, out var parsedKey, out _, out prefix, out _))
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(parsedKey, key))
                {
                    if (!TryMatchWholeLineKey(text, key, out prefix))
                    {
                        continue;
                    }
                }
            }
            else if (!TryMatchWholeLineKey(text, key, out prefix))
            {
                continue;
            }

            matches++;
            if (matches != occurrence)
            {
                continue;
            }

            _lines[index] = _lines[index] with { Text = prefix + line };
            return true;
        }

        return ContainsLine(line);
    }

    public bool SetSectionValue(string section, string key, string value)
    {
        return SetSectionValue(section, key, value, occurrence: 1);
    }

    public bool SetSectionValue(string section, string key, string value, int occurrence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrence, 1);

        var inSection = false;
        var matches = 0;
        for (var index = 0; index < _lines.Count; index++)
        {
            var text = _lines[index].Text;
            if (TryParseSection(text, out var parsedSection))
            {
                inSection = StringComparer.OrdinalIgnoreCase.Equals(parsedSection, section);
                continue;
            }

            if (!inSection ||
                !TryParseKeyValue(text, _lineCommentPrefixes, out var parsedKey, out var currentValue, out var prefix, out var separator) ||
                !StringComparer.OrdinalIgnoreCase.Equals(parsedKey, key))
            {
                continue;
            }

            matches++;
            if (matches != occurrence)
            {
                continue;
            }

            _lines[index] = _lines[index] with { Text = prefix + parsedKey + NormalizeReplacementSeparator(separator, currentValue, value) + value };
            return true;
        }

        return false;
    }

    public bool SetSectionLine(string section, string key, string line)
    {
        return SetSectionLine(section, key, line, occurrence: 1);
    }

    public bool SetSectionLine(string section, string key, string line, int occurrence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentOutOfRangeException.ThrowIfLessThan(occurrence, 1);

        var inSection = false;
        var matches = 0;
        for (var index = 0; index < _lines.Count; index++)
        {
            var text = _lines[index].Text;
            if (TryParseSection(text, out var parsedSection))
            {
                inSection = StringComparer.OrdinalIgnoreCase.Equals(parsedSection, section);
                continue;
            }

            if (!inSection ||
                !TryParseKeyValue(text, _lineCommentPrefixes, out var parsedKey, out _, out var prefix, out _) ||
                !StringComparer.OrdinalIgnoreCase.Equals(parsedKey, key))
            {
                continue;
            }

            matches++;
            if (matches != occurrence)
            {
                continue;
            }

            _lines[index] = _lines[index] with { Text = prefix + line };
            return true;
        }

        return ContainsSectionLine(section, line);
    }

    public byte[] ToBytes()
    {
        var builder = new StringBuilder();
        foreach (var line in _lines)
        {
            builder.Append(line.Text);
            builder.Append(line.LineEnding);
        }

        return Encoding.GetBytes(builder.ToString());
    }

    private bool ContainsLine(string line)
    {
        foreach (var textLine in _lines)
        {
            if (StringComparer.Ordinal.Equals(textLine.Text, line))
            {
                return true;
            }
        }

        return false;
    }

    private bool ContainsSectionLine(string section, string line)
    {
        var inSection = false;
        foreach (var textLine in _lines)
        {
            if (TryParseSection(textLine.Text, out var parsedSection))
            {
                inSection = StringComparer.OrdinalIgnoreCase.Equals(parsedSection, section);
                continue;
            }

            if (inSection && StringComparer.Ordinal.Equals(textLine.Text, line))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryMatchWholeLineKey(string text, string key, out string prefix)
    {
        prefix = string.Empty;

        if (StringComparer.Ordinal.Equals(text, key) ||
            text.StartsWith(key + ",", StringComparison.Ordinal))
        {
            return true;
        }

        var keyStart = 0;
        while (keyStart < text.Length && char.IsWhiteSpace(text[keyStart]))
        {
            keyStart++;
        }

        if (keyStart >= text.Length || keyStart + key.Length > text.Length)
        {
            return false;
        }

        if (!text.AsSpan(keyStart, key.Length).Equals(key.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var keyEnd = keyStart + key.Length;
        if (keyEnd < text.Length && !char.IsWhiteSpace(text[keyEnd]))
        {
            return false;
        }

        prefix = text[..keyStart];
        return true;
    }

    private static bool TryParseKeyValue(
        string line,
        IReadOnlyCollection<string> lineCommentPrefixes,
        out string key,
        out string value,
        out string prefix,
        out string separator)
    {
        key = string.Empty;
        value = string.Empty;
        prefix = string.Empty;
        separator = string.Empty;

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed.StartsWith('['))
        {
            return false;
        }

        foreach (var commentPrefix in lineCommentPrefixes)
        {
            if (trimmed.StartsWith(commentPrefix, StringComparison.Ordinal))
            {
                return false;
            }
        }

        var keyStart = 0;
        while (keyStart < line.Length && char.IsWhiteSpace(line[keyStart]))
        {
            keyStart++;
        }

        if (keyStart >= line.Length)
        {
            return false;
        }

        var equalsIndex = line.IndexOf('=', keyStart);
        var keyEnd = equalsIndex >= 0 ? equalsIndex : keyStart;
        if (equalsIndex < 0)
        {
            while (keyEnd < line.Length && !char.IsWhiteSpace(line[keyEnd]))
            {
                keyEnd++;
            }
        }

        while (keyEnd > keyStart && char.IsWhiteSpace(line[keyEnd - 1]))
        {
            keyEnd--;
        }

        if (keyEnd <= keyStart)
        {
            return false;
        }

        var valueStart = equalsIndex >= 0 ? equalsIndex + 1 : keyEnd;
        while (valueStart < line.Length && char.IsWhiteSpace(line[valueStart]))
        {
            valueStart++;
        }

        if (equalsIndex < 0 && valueStart >= line.Length)
        {
            return false;
        }

        prefix = line[..keyStart];
        key = line[keyStart..keyEnd];
        separator = line[keyEnd..valueStart];
        value = line[valueStart..];
        return true;
    }

    private static string NormalizeReplacementSeparator(string separator, string currentValue, string replacementValue)
    {
        // Explicit leading layout whitespace belongs to the replacement rather
        // than accumulating with indentation retained from an earlier value.
        if (replacementValue.Length > 0 && char.IsWhiteSpace(replacementValue[0]))
        {
            var equalsIndex = separator.IndexOf('=');
            return equalsIndex >= 0 ? separator[..(equalsIndex + 1)] : string.Empty;
        }

        if (currentValue.Length == 0)
        {
            var equalsIndex = separator.IndexOf('=');
            if (equalsIndex >= 0 &&
                separator.AsSpan(equalsIndex + 1).Trim().Length == 0)
            {
                return separator[..(equalsIndex + 1)];
            }
        }

        foreach (var value in separator)
        {
            if (value > '\u007f')
            {
                return separator.Contains('=', StringComparison.Ordinal) ? "=" : " ";
            }
        }

        return separator;
    }

    private static bool TryParseSection(string line, out string section)
    {
        var trimmed = line.Trim();
        if (trimmed.Length > 2 && trimmed[0] == '[' && trimmed[^1] == ']')
        {
            section = trimmed[1..^1];
            return true;
        }

        section = string.Empty;
        return false;
    }

    private static List<ClientTextLine> SplitLines(string text)
    {
        var lines = new List<ClientTextLine>();
        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\r' && text[index] != '\n')
            {
                continue;
            }

            var endingLength = text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n'
                ? 2
                : 1;
            lines.Add(new ClientTextLine(text[start..index], text.Substring(index, endingLength)));
            index += endingLength - 1;
            start = index + 1;
        }

        if (start < text.Length)
        {
            lines.Add(new ClientTextLine(text[start..], string.Empty));
        }

        return lines;
    }
}
