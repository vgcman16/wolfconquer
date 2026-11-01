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
        var seen = new HashSet<long>();
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
            long player = ReadCharacterUid(image);
            if (!seen.Add(player)) throw new InvalidDataException("Configured accounts duplicate a player UID.");
            if (player != uid) continue;
            if (fileAccount != account) throw new InvalidDataException("Existing player belongs to a different account file.");
            found = true;
        }
        if (!found) throw new InvalidDataException("Existing player lacks its configured account file.");
    }

    private static void RequireExistingAdmissionAccountMapping(JournalRoots roots, JournalRecord record)
    {
        if (record.Version != 7) return;
        var account = record.Changes.Single(row => row.Kind == LegacyTradeJournalFileKind.Character).FileId;
        RequireExistingAdmissionAccountMapping(roots.Users, account, AdmissionPlayerUid(record));
    }
}
