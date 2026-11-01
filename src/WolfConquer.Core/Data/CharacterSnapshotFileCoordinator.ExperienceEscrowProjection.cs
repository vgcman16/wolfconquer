using System.Text;

namespace WolfConquer.Core.Data;

internal static partial class CharacterSnapshotFileCoordinator
{
    private sealed record EscrowLine(string Text, string Ending);

    // Only the five real escrow-wallet fields are changed. Preserve all
    // unrelated bytes, mixed line endings and even unrelated duplicate keys.
    private static string ProjectExperienceEscrow(string text, IReadOnlyDictionary<string, string> values)
    {
        var lines = new List<EscrowLine>();
        for (int start = 0; start < text.Length;)
        {
            int end = start;
            while (end < text.Length && text[end] is not ('\r' or '\n')) end++;
            int next = end;
            if (next < text.Length) { next++; if (text[end] == '\r' && next < text.Length && text[next] == '\n') next++; }
            lines.Add(new(text[start..end], text[end..next])); start = next;
        }
        string newline = lines.Select(line => line.Ending).FirstOrDefault(ending => ending.Length != 0) ?? "\r\n";
        var output = new StringBuilder(text.Length); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool character = false, found = false;
        foreach (var line in lines)
        {
            var trimmed = line.Text.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                Flush();
                character = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (character && found) throw new InvalidDataException("experienceEscrowCharacterSectionAmbiguous");
                found |= character;
            }
            string content = line.Text;
            if (character && trimmed.Length != 0 && !trimmed.StartsWith(';') && !trimmed.StartsWith('#') && !trimmed.StartsWith('['))
            {
                int equals = content.IndexOf('=');
                string key = equals < 0 ? trimmed : content[..equals].Trim();
                if (values.TryGetValue(key, out var replacement))
                {
                    if (equals < 0 || !seen.Add(key)) throw new InvalidDataException("experienceEscrowWalletFieldAmbiguous");
                    string old = content[(equals + 1)..];
                    int leading = old.Length - old.TrimStart().Length, trailing = old.Length - old.TrimEnd().Length;
                    if (leading + trailing > old.Length) trailing = 0;
                    content = content[..(equals + 1)] + old[..leading] + replacement + (trailing == 0 ? "" : old[^trailing..]);
                }
            }
            output.Append(content).Append(line.Ending);
        }
        Flush();
        if (!found) throw new InvalidDataException("experienceEscrowCharacterSectionMissing");
        if (text.Length != 0 && text[^1] is not ('\r' or '\n') && output.Length != 0)
        {
            if (output[^1] == '\n') { output.Length--; if (output.Length != 0 && output[^1] == '\r') output.Length--; }
            else if (output[^1] == '\r') output.Length--;
        }
        return output.ToString();

        void Flush()
        {
            if (!character) return;
            foreach (var pair in values)
            {
                if (seen.Contains(pair.Key)) continue;
                if (output.Length != 0 && output[^1] is not ('\r' or '\n')) output.Append(newline);
                output.Append(pair.Key).Append('=').Append(pair.Value).Append(newline);
            }
        }
    }
}
