namespace WolfConquer.Core.Data;

internal static partial class CharacterSnapshotFileBaseline
{
    // Captured SQL bytes only: no path, fallback, or temporary file.
    internal static bool TryCaptureBytes(ReadOnlySpan<byte> input, string prefix, out Snapshot? snapshot, out string reason)
    {
        snapshot = null;
        reason = string.Empty;
        if (input.Length is <= 0 or > MaximumBytes) { reason = prefix + "SnapshotSizeInvalid"; return false; }
        var bytes = input.ToArray();
        var text = CharacterSnapshotFileEncoding.Decode(bytes, out var encoding);
        var preamble = encoding.GetPreamble();
        var encoded = encoding.GetBytes(text);
        if (preamble.Length + encoded.Length != bytes.Length ||
            !bytes.AsSpan(0, preamble.Length).SequenceEqual(preamble) ||
            !bytes.AsSpan(preamble.Length).SequenceEqual(encoded))
        { reason = prefix + "EncodingRoundtripInvalid"; return false; }
        var lines = new List<string>();
        using (var reader = new StringReader(text))
            while (reader.ReadLine() is { } line) lines.Add(line);
        if (!TryReadCharacterFields(lines, prefix, out var fields, out reason)) return false;
        snapshot = new(bytes, lines.ToArray(), encoding, fields);
        return true;
    }
}
