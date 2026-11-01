using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Immutable saved DATA, issued into a binding by actual detached admission and
// advanced by cooperating committed writers. Null is genuine configured absence.
internal sealed class LegacySavedProficiencyImage
{
    private readonly byte[]? bytes;
    internal LegacySavedProficiencyImage(byte[]? image) => bytes = image?.ToArray();
    internal bool Matches(byte[]? image) => bytes is null ? image is null :
        image is not null && bytes.AsSpan().SequenceEqual(image);
    internal bool MatchesSnapshot(PlayerProficiencySnapshot expected)
    {
        var saved = bytes is null ? PlayerProficiencySnapshot.Empty : PlayerProficiencySnapshotLoader.Parse(bytes);
        return PlayerProficiencySnapshotLoader.ToLegacyBytes(saved).AsSpan()
            .SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(expected));
    }

    // Project typed changes from this actual immutable origin, retaining the
    // three opaque bytes of every retained saved UID. This grants no write.
    internal byte[] ProjectNext(PlayerProficiencySnapshot next)
    {
        var old = LegacyNativeProficiencyStorage.FreezeSessionSnapshot(bytes is null
            ? PlayerProficiencySnapshot.Empty : PlayerProficiencySnapshotLoader.Parse(bytes));
        var after = LegacyNativeProficiencyStorage.FreezeSessionSnapshot(next);
        var result = PlayerProficiencySnapshotLoader.ToLegacyBytes(after);
        if (result.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw Invalid();
        if (bytes is not null)
        {
            var offsets = old.Proficiencies.Select((row, index) => (row.Id, Offset: 4 + index * PlayerProficiencySnapshotLoader.LegacyProficiencySize))
                .ToDictionary(row => row.Id, row => row.Offset);
            for (int index = 0; index < after.Proficiencies.Count; index++)
                if (offsets.TryGetValue(after.Proficiencies[index].Id, out int prior))
                    bytes.AsSpan(prior + 9, 3).CopyTo(result.AsSpan(4 + index * PlayerProficiencySnapshotLoader.LegacyProficiencySize + 9, 3));
        }
        return result;
    }

    internal static byte[]? Read(string path)
    {
        try
        {
            var file = new FileInfo(Path.GetFullPath(path)); file.Refresh();
            if (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0) throw Invalid();
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
            {
                parent.Refresh();
                if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0) throw Invalid();
            }
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw Invalid();
            var image = new byte[checked((int)input.Length)]; input.ReadExactly(image);
            if (input.ReadByte() != -1 || input.Length != image.Length) throw Invalid();
            return image;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private static InvalidDataException Invalid() => new("savedProficiencyImageUnsafe");
}
