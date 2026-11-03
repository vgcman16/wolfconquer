using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyCapturedCharacterPrefix
{
    private static bool TryPrepareOnlineBytes(CharacterSnapshotFileBaseline.Snapshot baseline, out byte[] result, out string reason)
    {
        result = [];
        reason = "capturedPrefixOnlineMissing";
        string text = CharacterSnapshotFileEncoding.Decode(baseline.Bytes, out _);
        bool inCharacter = false;
        int start = 0;
        while (start < text.Length)
        {
            int end = start;
            while (end < text.Length && text[end] is not ('\r' or '\n')) end++;
            string raw = text[start..end];
            string trimmed = raw.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
                inCharacter = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
            else if (inCharacter && trimmed.Length != 0 && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                int equals = raw.IndexOf('=');
                if (equals > 0 && raw[..equals].Trim().Equals("Online", StringComparison.OrdinalIgnoreCase))
                {
                    int first = equals + 1, last = raw.Length;
                    while (first < last && char.IsWhiteSpace(raw[first])) first++;
                    while (last > first && char.IsWhiteSpace(raw[last - 1])) last--;
                    if (first == last) { reason = "capturedPrefixOnlineInvalid"; return false; }
                    string updated = string.Concat(text.AsSpan(0, start + first), "1", text.AsSpan(start + last));
                    byte[] payload = baseline.Encoding.GetBytes(updated);
                    byte[] preamble = baseline.Encoding.GetPreamble();
                    result = new byte[checked(preamble.Length + payload.Length)];
                    preamble.CopyTo(result, 0);
                    payload.CopyTo(result, preamble.Length);
                    reason = string.Empty;
                    return true;
                }
            }
            start = end;
            if (start < text.Length && text[start] == '\r') start++;
            if (start < text.Length && text[start] == '\n') start++;
        }
        return false;
    }
}
