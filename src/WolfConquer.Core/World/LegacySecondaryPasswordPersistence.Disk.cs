using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacySecondaryPasswordPersistence
{
    private static bool TryReadDisk(string path, out Dictionary<string, string> fields,
        out byte[] bytes, out string reason)
    {
        fields = new(StringComparer.OrdinalIgnoreCase);
        bytes = [];
        reason = string.Empty;
        // This short lease prevents writes/deletes between the exact-byte capture and
        // decoding through the shared UTF8/BOM/Windows-1252 character reader.
        using var lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (lease.Length > 16 * 1024 * 1024)
        { reason = "secondaryPasswordSnapshotTooLarge"; return false; }
        bytes = new byte[checked((int)lease.Length)];
        lease.ReadExactly(bytes);
        var lines = CharacterSnapshotFileEncoding.ReadAllLines(path, out _);
        var inCharacter = false;
        var sections = 0;
        foreach (var raw in lines)
        {
            var text = raw.Trim();
            if (text.StartsWith('[') && text.EndsWith(']'))
            {
                inCharacter = text[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (inCharacter && ++sections != 1)
                { reason = "secondaryPasswordCharacterSectionAmbiguous"; return false; }
                continue;
            }
            if (!inCharacter || text.Length == 0 || text.StartsWith(';') || text.StartsWith('#')) continue;
            var equals = raw.IndexOf('=');
            if (equals <= 0 || raw[..equals].Trim().Length == 0)
            { reason = "secondaryPasswordCharacterLineInvalid"; return false; }
            // Preserve the exact value for stale detection, not merely a parsed PIN.
            if (!fields.TryAdd(raw[..equals].Trim(), raw[(equals + 1)..]))
            { reason = "secondaryPasswordCharacterFieldsAmbiguous"; return false; }
        }
        if (sections != 1)
        { reason = "secondaryPasswordCharacterSectionMissing"; return false; }
        return true;
    }

    private static bool TryValidateSafePath(string path, out string reason)
    {
        reason = string.Empty;
        var file = new FileInfo(Path.GetFullPath(path));
        file.Refresh();
        if (!file.Exists)
        { reason = "secondaryPasswordSnapshotMissing"; return false; }
        if ((file.Attributes & FileAttributes.ReparsePoint) != 0)
        { reason = "secondaryPasswordReparsePathRejected"; return false; }
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        {
            parent.Refresh();
            if ((parent.Attributes & FileAttributes.ReparsePoint) == 0) continue;
            reason = "secondaryPasswordReparsePathRejected";
            return false;
        }
        return true;
    }
}
