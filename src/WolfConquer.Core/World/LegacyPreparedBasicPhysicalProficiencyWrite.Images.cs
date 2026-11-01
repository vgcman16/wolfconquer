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

    private static byte[]? ReadImage(string path) => LegacySavedProficiencyImage.Read(path);
}
