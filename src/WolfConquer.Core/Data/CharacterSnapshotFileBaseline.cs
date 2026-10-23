using System.Text;

namespace WolfConquer.Core.Data;

// Ordinary local snapshot evidence, not a multi-file transaction or symlink-race proof.
// Cooperating readers/writers must use the same SnapshotFileLockCoordinator gate.
internal static class CharacterSnapshotFileBaseline
{
    internal const int MaximumBytes = 16 * 1024 * 1024;

    internal sealed record Snapshot(byte[] Bytes, string[] Lines, Encoding Encoding,
        IReadOnlyDictionary<string, string> Fields);

    internal static bool TryValidateSafePath(string path, string prefix, out string reason)
    {
        reason = string.Empty;
        var file = new FileInfo(Path.GetFullPath(path));
        file.Refresh();
        if (!file.Exists) { reason = prefix + "SnapshotMissing"; return false; }
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
        { reason = prefix + "ReparsePathRejected"; return false; }
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        {
            parent.Refresh();
            if ((parent.Attributes & FileAttributes.ReparsePoint) == 0) continue;
            reason = prefix + "ReparsePathRejected";
            return false;
        }
        return true;
    }

    internal static bool TryReadBytes(string path, string prefix, out byte[] bytes, out string reason)
    {
        bytes = [];
        if (!TryValidateSafePath(path, prefix, out reason)) return false;
        using var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (lease.Length > MaximumBytes)
        { reason = prefix + "SnapshotTooLarge"; return false; }
        var captured = new byte[checked((int)lease.Length)];
        lease.ReadExactly(captured);
        // Never perform an unbounded second read if an uncooperative writer changes length.
        if (lease.ReadByte() != -1 || lease.Length != captured.Length)
        { reason = prefix + "SnapshotLengthChanged"; return false; }
        bytes = captured;
        return true;
    }

    internal static bool TryRead(string path, string prefix, out Snapshot? snapshot, out string reason)
    {
        snapshot = null;
        if (!TryReadBytes(path, prefix, out var bytes, out reason)) return false;
        var text = CharacterSnapshotFileEncoding.Decode(bytes, out var encoding);
        var lines = new List<string>();
        using (var reader = new StringReader(text))
            while (reader.ReadLine() is { } line) lines.Add(line);
        if (!TryReadCharacterFields(lines, prefix, out var fields, out reason)) return false;
        snapshot = new(bytes, lines.ToArray(), encoding, fields);
        return true;
    }

    private static bool TryReadCharacterFields(IReadOnlyList<string> lines, string prefix,
        out Dictionary<string, string> fields, out string reason)
    {
        fields = new(StringComparer.OrdinalIgnoreCase);
        reason = string.Empty;
        var inCharacter = false;
        var sections = 0;
        foreach (var raw in lines)
        {
            var textLine = raw.Trim();
            if (textLine.StartsWith('[') && textLine.EndsWith(']'))
            {
                inCharacter = textLine[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (inCharacter && ++sections != 1)
                { reason = prefix + "CharacterSectionAmbiguous"; return false; }
                continue;
            }
            if (!inCharacter || textLine.Length == 0 || textLine.StartsWith(';') || textLine.StartsWith('#')) continue;
            if (!TryAddCharacterField(raw, prefix, fields, out reason)) return false;
        }
        if (sections != 1)
        { reason = prefix + "CharacterSectionMissing"; return false; }
        return true;
    }

    private static bool TryAddCharacterField(string raw, string prefix,
        Dictionary<string, string> fields, out string reason)
    {
        reason = string.Empty;
        var equals = raw.IndexOf('=');
        if (equals <= 0 || raw[..equals].Trim().Length == 0)
        { reason = prefix + "CharacterLineInvalid"; return false; }
        // PIN stale detection depends on exact values, not merely equivalent parsing.
        if (!fields.TryAdd(raw[..equals].Trim(), raw[(equals + 1)..]))
        { reason = prefix + "CharacterFieldsAmbiguous"; return false; }
        return true;
    }
}
