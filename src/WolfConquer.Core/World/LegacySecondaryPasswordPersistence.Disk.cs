using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal static partial class LegacySecondaryPasswordPersistence
{
    private static bool TryReadDisk(string path, out Dictionary<string, string> fields,
        out byte[] bytes, out string reason)
    {
        fields = new(StringComparer.OrdinalIgnoreCase);
        bytes = [];
        if (!CharacterSnapshotFileBaseline.TryRead(path, "secondaryPassword", out var snapshot, out reason))
            return false;
        var current = snapshot!;
        fields = new(current.Fields, StringComparer.OrdinalIgnoreCase);
        bytes = current.Bytes;
        return true;
    }

    private static bool TryValidateSafePath(string path, out string reason) =>
        CharacterSnapshotFileBaseline.TryValidateSafePath(path, "secondaryPassword", out reason);
}
