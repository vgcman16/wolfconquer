using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

public sealed partial class CharacterSnapshotLoader
{
    internal ManagedOriginLoad CreateManagedFirstCharacterSnapshot(long uid, string name, long body, long classCode)
    {
        if (uid <= 0) throw new ArgumentOutOfRangeException(nameof(uid));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (string.IsNullOrWhiteSpace(PlayerItemsRoot)) throw new IOException("managedCreationItemsRootUnavailable");
        // New-account setup initializes its configured sidecar route before
        // committing Character, so the real raw item read can prove absence.
        Directory.CreateDirectory(PlayerItemsRoot);
        Directory.CreateDirectory(_usersRoot);
        string path = Path.Combine(_usersRoot, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        // This is the same real creation serializer, evaluated once. The
        // receipt retains its prepared bytes rather than adopting a later read.
        string content = CreateLegacyFirstCharacterSnapshotContent(uid, name, body, classCode);
        var encoding = CharacterSnapshotFileEncoding.DefaultWriteEncoding;
        var expected = encoding.GetPreamble().Concat(encoding.GetBytes(content)).ToArray();
        return CharacterSnapshotFileCoordinator.ExecuteLocked(path, () =>
        {
            if (!CharacterSnapshotFileCoordinator.TryCreateCapturedImage(path, expected, out var uncertain, out var reason))
                throw new IOException((uncertain ? "managedCreationOutcomeUnproven:" : "managedCreationRefused:") + reason);
            var loaded = TryLoadByAccountId(uid, persistExpiry: false);
            if (!loaded.Succeeded || loaded.Snapshot is null ||
                !CharacterSnapshotFileBaseline.TryReadBytes(path, "managedCreation", out var actual, out _) ||
                !actual.AsSpan().SequenceEqual(expected)) throw new IOException("managedCreationOutcomeUnproven:createdImageChanged");
            return new ManagedOriginLoad(loaded, path, expected);
        });
    }
}
