using System.Globalization;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyCharacterCreationNameClaim
{
    private const int MaximumNameEntries = ushort.MaxValue;
    private const long MaximumNameBytes = 256L * 1024 * 1024;

    internal static NameScan Scan(string usersRoot, string name, uint account, byte[]? ownAfter, string? transaction)
    {
        LegacyCharacterCreationAbsence.RequireSafeDirectory(usersRoot, allowMissing: false);
        var own = Path.Combine(usersRoot, account.ToString(CultureInfo.InvariantCulture) + ".ini");
        var journal = Path.Combine(usersRoot, ".managed-trade-journal");
        var stage = transaction is null ? null : own + ".managed-trade-" + transaction + ".tmp";
        var images = new Dictionary<string, byte[]>(PathComparer);
        var decodedUids = new HashSet<long>();
        int count = 0; long bytesRead = 0; bool hasJournal = false;
        foreach (var path in Directory.EnumerateFileSystemEntries(usersRoot))
        {
            if (++count > MaximumNameEntries) throw new InvalidDataException("Creation name membership exceeds 65535 entries.");
            LegacyCharacterCreationAbsence.RequireSafePath(path);
            if (PathComparer.Equals(path, journal))
            {
                LegacyCharacterCreationAbsence.RequireSafeDirectory(path, allowMissing: false);
                hasJournal = true; continue; // Actual journal owner separately checks its exact contents.
            }
            if (stage is not null && PathComparer.Equals(path, stage))
            {
                bytesRead = checked(bytesRead + RequireStage(path, ownAfter!));
                RequireNameByteBudget(bytesRead); continue;
            }
            if (Directory.Exists(path)) throw new InvalidDataException("Character name membership contains an unknown directory.");
            var leaf = Path.GetFileName(path);
            if (!leaf.EndsWith(".ini", StringComparison.Ordinal) ||
                !uint.TryParse(leaf.AsSpan(0, leaf.Length - 4), NumberStyles.None, CultureInfo.InvariantCulture, out var fileId) ||
                fileId == 0 || leaf != fileId.ToString(CultureInfo.InvariantCulture) + ".ini")
                throw new InvalidDataException("Character name membership contains an ambiguous or unknown leaf.");
            if (!CharacterSnapshotFileBaseline.TryRead(path, "creationName", out var captured, out var reason) || captured is null)
                throw new InvalidDataException(reason);
            bytesRead = checked(bytesRead + captured.Bytes.Length);
            RequireNameByteBudget(bytesRead);
            var character = ReadCharacter(captured.Bytes);
            if (character.Uid == account && !PathComparer.Equals(path, own))
                throw new InvalidDataException("Fresh creation UID is already bound to another Character account leaf.");
            if (!captured.Fields.TryGetValue("Name", out var rawName) || rawName != character.Name ||
                !decodedUids.Add(character.Uid))
                throw new InvalidDataException("Character name/UID membership is ambiguous.");
            if (PathComparer.Equals(path, own) && ownAfter is not null)
            {
                if (!captured.Bytes.AsSpan().SequenceEqual(ownAfter))
                    throw new InvalidDataException("Creation target differs from its exact Character afterimage.");
                continue;
            }
            if (string.Equals(character.Name, name, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Creation name collides with an existing complete Character name.");
            if (!images.TryAdd(Path.GetFullPath(path), captured.Bytes))
                throw new InvalidDataException("Character membership contains a path alias.");
        }
        LegacyCharacterCreationAbsence.RequireSafeDirectory(usersRoot, allowMissing: false);
        return new(images, hasJournal);
    }

    private static int RequireStage(string path, byte[] after)
    {
        if (!CharacterSnapshotFileBaseline.TryReadBytes(path, "creationStage", out var bytes, out var reason) ||
            bytes.Length > after.Length || !bytes.AsSpan().SequenceEqual(after.AsSpan(0, bytes.Length)))
            throw new InvalidDataException("Creation stage differs from its exact bounded afterimage: " + reason);
        return bytes.Length;
    }

    private static void RequireNameByteBudget(long bytesRead)
    {
        if (bytesRead > MaximumNameBytes) throw new InvalidDataException("Creation name images/stages exceed 256MiB.");
    }
}
