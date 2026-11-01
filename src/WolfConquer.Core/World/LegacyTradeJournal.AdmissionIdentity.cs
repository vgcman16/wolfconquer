using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacyTradeJournal
{
    private static long AdmissionPlayerUid(JournalRecord record)
    {
        var character = record.Changes.Single(row => row.Kind == LegacyTradeJournalFileKind.Character);
        return record.Version == 7 ? ReadCharacterUid(character.Before ?? throw new InvalidDataException(
            "Existing admission lacks its Character preimage.")) : character.FileId;
    }

    internal static void RequireExistingAdmissionAccountMapping(string users, long account, long uid)
    {
        if (account is <= 0 or > uint.MaxValue || uid is <= 0 or > uint.MaxValue)
            throw new InvalidDataException("Existing admission identity exceeds its native domain.");
        bool found = false;
        int count = 0;
        foreach (var path in Directory.EnumerateFileSystemEntries(users))
        {
            if (!Path.GetExtension(path).Equals(".ini", StringComparison.OrdinalIgnoreCase)) continue;
            if (++count > ushort.MaxValue) throw new InvalidDataException("Existing admission account scan exceeds its bound.");
            EnsureSafePath(path);
            string name = Path.GetFileNameWithoutExtension(path);
            if (Directory.Exists(path) || !long.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var fileAccount) ||
                fileAccount is <= 0 or > uint.MaxValue || name != fileAccount.ToString(CultureInfo.InvariantCulture) ||
                !CharacterSnapshotFileBaseline.TryReadBytes(path, "ordinaryAdmissionIdentity", out var image, out _))
                throw new InvalidDataException("Existing admission found a malformed configured account file.");
            if (fileAccount == account)
            {
                if (ReadCharacterUid(image) != uid) throw new InvalidDataException("Existing account has a different player UID.");
                found = true;
            }
            else if (ReadAdmissionIdentityCandidates(image).Contains(uid))
                throw new InvalidDataException("Existing player belongs to a different account file.");
        }
        if (!found) throw new InvalidDataException("Existing player lacks its configured account file.");
    }

    // Foreign malformed business fields cannot authorize this account, but
    // every possible Character UID still has to be readable and excluded.
    private static HashSet<long> ReadAdmissionIdentityCandidates(byte[] image)
    {
        var text = CharacterSnapshotFileEncoding.Decode(image, out _);
        var identities = new HashSet<long>();
        var active = false;
        var sections = 0;
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } raw)
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                active = line[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (active) sections++;
                continue;
            }
            if (!active || line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            var equals = raw.IndexOf('=');
            if (equals <= 0) throw new InvalidDataException("Foreign Character identity is unprovable.");
            if (!raw[..equals].Trim().Equals("UID", StringComparison.OrdinalIgnoreCase)) continue;
            if (!long.TryParse(raw[(equals + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
                value is <= 0 or > uint.MaxValue)
                throw new InvalidDataException("Foreign Character UID is unprovable.");
            identities.Add(value);
        }
        if (sections == 0 || identities.Count == 0)
            throw new InvalidDataException("Foreign Character identity is missing.");
        return identities;
    }

    private static void RequireExistingAdmissionAccountMapping(JournalRoots roots, JournalRecord record)
    {
        if (record.Version != 7) return;
        var account = record.Changes.Single(row => row.Kind == LegacyTradeJournalFileKind.Character).FileId;
        RequireExistingAdmissionAccountMapping(roots.Users, account, AdmissionPlayerUid(record));
    }
}
