using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyPreparedBasicPhysicalProficiencyWrite
{
    private static void RequireMatchesSnapshot(byte[]? image, PlayerProficiencySnapshot expected)
    {
        var parsed = image is null ? PlayerProficiencySnapshot.Empty : PlayerProficiencySnapshotLoader.Parse(image);
        var frozen = LegacyNativeProficiencyStorage.FreezeSessionSnapshot(parsed);
        if (!PlayerProficiencySnapshotLoader.ToLegacyBytes(frozen).AsSpan()
            .SequenceEqual(PlayerProficiencySnapshotLoader.ToLegacyBytes(expected))) throw Invalid();
    }

    private static byte[] ProjectImage(byte[]? image, PlayerProficiencySnapshot proposed)
    {
        var result = PlayerProficiencySnapshotLoader.ToLegacyBytes(proposed);
        if (result.Length > CharacterSnapshotFileBaseline.MaximumBytes) throw Invalid();
        if (image is not null)
        {
            var original = LegacyNativeProficiencyStorage.FreezeSessionSnapshot(PlayerProficiencySnapshotLoader.Parse(image));
            var offsets = original.Proficiencies.Select((row, index) => (row.Id,
                Offset: sizeof(int) + index * PlayerProficiencySnapshotLoader.LegacyProficiencySize))
                .ToDictionary(row => row.Id, row => row.Offset);
            for (int index = 0; index < proposed.Proficiencies.Count; index++)
                if (offsets.TryGetValue(proposed.Proficiencies[index].Id, out int prior))
                    image.AsSpan(prior + 9, 3).CopyTo(result.AsSpan(sizeof(int) +
                        index * PlayerProficiencySnapshotLoader.LegacyProficiencySize + 9, 3));
        }
        RequireMatchesSnapshot(result, proposed);
        return result;
    }

    private static byte[]? ReadImage(string path)
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
            var result = new byte[checked((int)input.Length)]; input.ReadExactly(result);
            if (input.ReadByte() != -1 || input.Length != result.Length) throw Invalid();
            return result;
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }
}
