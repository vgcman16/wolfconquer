using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyExperienceContinuationCodec
{
    private sealed record Marker(int LineStart, int LineEnd, int ValueStart, int ValueEnd, string Encoded);
    private sealed record CharacterImage(string Text, Encoding Encoding, uint Uid, long Level, long Experience,
        Marker? Marker, int InsertAt, string Newline);

    internal static LegacyExperienceContinuation? ReadCharacterImage(byte[] image, long accountId, uint uid)
    {
        var captured = CaptureImage(image, accountId, uid);
        var state = captured.Marker is { } marker ? Decode(marker.Encoded) : null;
        state?.RequireCharacter(accountId, captured.Uid, captured.Level, captured.Experience);
        return state;
    }

    // DATA only: the actual writer supplies both its captured baseline and its
    // independently prepared scalar image, then atomically replaces the file.
    internal static byte[] ProjectCharacterImage(byte[] before, byte[] projectedCharacter, long accountId, uint uid,
        LegacyExperienceContinuation? expected, LegacyExperienceContinuation? next)
    {
        var original = CaptureImage(before, accountId, uid);
        var actual = original.Marker is { } old ? Decode(old.Encoded) : null;
        actual?.RequireCharacter(accountId, original.Uid, original.Level, original.Experience);
        if (actual != expected) throw LegacyExperienceContinuation.Invalid();
        var projected = CaptureImage(projectedCharacter, accountId, uid);
        if (!string.Equals(original.Marker?.Encoded, projected.Marker?.Encoded, StringComparison.Ordinal))
            throw LegacyExperienceContinuation.Invalid();
        ValidateTransition(expected, next);
        next?.RequireCharacter(accountId, projected.Uid, projected.Level, projected.Experience);
        if (expected is null && next is not null &&
            (next.InitialLevel != original.Level || next.InitialExperience != (ulong)original.Experience))
            throw LegacyExperienceContinuation.Invalid();
        if (expected is not null && next is null)
            expected.RequireCharacter(accountId, projected.Uid, projected.Level, projected.Experience);

        string text = ReplaceMarker(projected, next is null ? null : Encode(next));
        byte[] result = projected.Encoding.GetPreamble().Concat(projected.Encoding.GetBytes(text)).ToArray();
        if (result.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes ||
            ReadCharacterImage(result, accountId, uid) != next) throw LegacyExperienceContinuation.Invalid();
        return result;
    }

    private static string ReplaceMarker(CharacterImage image, string? encoded)
    {
        if (image.Marker is { } marker)
            return encoded is null ? image.Text.Remove(marker.LineStart, marker.LineEnd - marker.LineStart) :
                image.Text[..marker.ValueStart] + encoded + image.Text[marker.ValueEnd..];
        if (encoded is null) return image.Text;
        string before = image.Text[..image.InsertAt], after = image.Text[image.InsertAt..];
        string separator = before.Length != 0 && before[^1] is not ('\r' or '\n') ? image.Newline : "";
        string ending = after.Length != 0 || image.Text.Length != 0 && image.Text[^1] is '\r' or '\n' ? image.Newline : "";
        return before + separator + Key + "=" + encoded + ending + after;
    }

    private static CharacterImage CaptureImage(byte[] image, long accountId, uint uid)
    {
        if (image is null || image.Length is 0 or > CharacterSnapshotFileBaseline.MaximumBytes ||
            accountId is <= 0 or > uint.MaxValue || uid == 0) throw LegacyExperienceContinuation.Invalid();
        byte[] captured = image.ToArray();
        string text = CharacterSnapshotFileEncoding.Decode(captured, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(captured)) throw LegacyExperienceContinuation.Invalid();
        return ScanImage(text, encoding, uid);
    }

    private static CharacterImage ScanImage(string text, Encoding encoding, uint expectedUid)
    {
        bool character = false, found = false;
        long? uid = null, level = null, experience = null;
        Marker? marker = null;
        int insertAt = text.Length;
        string? newline = null;
        for (int start = 0; start < text.Length;)
        {
            int contentEnd = start;
            while (contentEnd < text.Length && text[contentEnd] is not ('\r' or '\n')) contentEnd++;
            int end = contentEnd;
            if (end < text.Length)
            {
                end++;
                if (text[contentEnd] == '\r' && end < text.Length && text[end] == '\n') end++;
                newline ??= text[contentEnd..end];
            }
            string line = text[start..contentEnd], trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                if (character) insertAt = start;
                character = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (character && found) throw LegacyExperienceContinuation.Invalid();
                found |= character;
            }
            else if (character && trimmed.Length != 0 && !trimmed.StartsWith(';') && !trimmed.StartsWith('#'))
            {
                int equals = line.IndexOf('=');
                if (equals <= 0) throw LegacyExperienceContinuation.Invalid();
                string key = line[..equals].Trim(), value = line[(equals + 1)..].Trim();
                if (key.Equals("UID", StringComparison.OrdinalIgnoreCase)) SetNumber(ref uid, value);
                else if (key.Equals("Level", StringComparison.OrdinalIgnoreCase)) SetNumber(ref level, value);
                else if (key.Equals("Experience", StringComparison.OrdinalIgnoreCase)) SetNumber(ref experience, value);
                else if (key.Equals(Key, StringComparison.OrdinalIgnoreCase))
                {
                    if (marker is not null || value.Length is 0 or > MaxEncodedLength) throw LegacyExperienceContinuation.Invalid();
                    string raw = line[(equals + 1)..];
                    int leading = raw.Length - raw.TrimStart().Length;
                    marker = new(start, end, start + equals + 1 + leading,
                        contentEnd - (raw.Length - raw.TrimEnd().Length), value);
                    _ = Decode(value); // Reject malformed presence even before scalar alignment.
                }
            }
            start = end;
        }
        if (!found || uid != expectedUid || level is null or < 1 or > ushort.MaxValue || experience is null or < 0)
            throw LegacyExperienceContinuation.Invalid();
        return new(text, encoding, expectedUid, level.Value, experience.Value, marker, insertAt, newline ?? "\r\n");
    }

    private static void SetNumber(ref long? destination, string value)
    {
        if (destination is not null || !long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            throw LegacyExperienceContinuation.Invalid();
        destination = parsed;
    }
}
