using System.Text;

namespace WolfConquer.Core.World;

// Only requested INI values are replaced. Unrelated text, individual line endings
// and final-newline presence survive; inserted fields use the existing local style.
internal static class LegacyOrdinaryAdmissionCharacterText
{
    private sealed record Line(string Text, string Ending);

    internal static string Project(string text, IReadOnlyDictionary<string, string> character,
        IReadOnlyDictionary<string, string>? paid)
    {
        var lines = Split(text);
        var newline = lines.Select(row => row.Ending).FirstOrDefault(value => value.Length != 0) ?? Environment.NewLine;
        var output = new StringBuilder(text.Length);
        var sections = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyDictionary<string, string>? values = null;
        var tracked = false;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in lines)
        {
            var trimmed = row.Text.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                Flush();
                var section = trimmed[1..^1].Trim();
                tracked = section.Equals("Character", StringComparison.OrdinalIgnoreCase) ||
                    section.Equals("PaidNobility", StringComparison.OrdinalIgnoreCase);
                values = section.Equals("Character", StringComparison.OrdinalIgnoreCase) ? character :
                    section.Equals("PaidNobility", StringComparison.OrdinalIgnoreCase) ? paid : null;
                keys.Clear();
                if (tracked && !sections.Add(section)) throw new InvalidDataException("Ambiguous admission section.");
            }
            var content = row.Text;
            if (tracked && trimmed.Length != 0 && !trimmed.StartsWith(';') && !trimmed.StartsWith('#') &&
                !trimmed.StartsWith('['))
            {
                var equals = content.IndexOf('=');
                if (equals <= 0) throw new InvalidDataException("Malformed admission field.");
                var key = content[..equals].Trim();
                if (!keys.Add(key)) throw new InvalidDataException("Ambiguous admission field.");
                if (values is not null && values.TryGetValue(key, out var replacement))
                {
                    var old = content[(equals + 1)..];
                    var leading = old.Length - old.TrimStart().Length;
                    var trailing = old.Length - old.TrimEnd().Length;
                    if (leading + trailing > old.Length) trailing = 0;
                    content = content[..(equals + 1)] + old[..leading] + replacement +
                        (trailing == 0 ? "" : old[^trailing..]);
                }
            }
            output.Append(content).Append(row.Ending);
        }
        Flush();
        if (!sections.Contains("Character")) throw new InvalidDataException("Missing Character section.");
        if (paid is not null && !sections.Contains("PaidNobility"))
        {
            Separate(); output.Append("[PaidNobility]").Append(newline);
            foreach (var pair in paid) output.Append(pair.Key).Append('=').Append(pair.Value).Append(newline);
        }
        // Separating new fields needs a newline between records, but must not
        // add a terminal newline to an originally unterminated final record.
        if (text.Length != 0 && text[^1] is not ('\r' or '\n') && output.Length != 0)
        {
            if (output[^1] == '\n')
            {
                output.Length--;
                if (output.Length != 0 && output[^1] == '\r') output.Length--;
            }
            else if (output[^1] == '\r') output.Length--;
        }
        return output.ToString();

        void Separate()
        {
            if (output.Length != 0 && output[^1] is not ('\r' or '\n')) output.Append(newline);
        }
        void Flush()
        {
            if (values is null) return;
            foreach (var pair in values)
            {
                if (keys.Contains(pair.Key)) continue;
                Separate(); output.Append(pair.Key).Append('=').Append(pair.Value).Append(newline);
            }
        }
    }

    private static List<Line> Split(string text)
    {
        var result = new List<Line>();
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] is not ('\r' or '\n')) continue;
            var end = index + 1;
            if (text[index] == '\r' && end < text.Length && text[end] == '\n') end++;
            result.Add(new(text[start..index], text[index..end]));
            start = end; index = end - 1;
        }
        if (start < text.Length) result.Add(new(text[start..], ""));
        return result;
    }
}
