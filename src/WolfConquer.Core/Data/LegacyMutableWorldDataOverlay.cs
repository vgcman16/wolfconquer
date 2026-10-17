namespace WolfConquer.Core.Data;

internal static class LegacyMutableWorldDataOverlay
{
    public static IReadOnlyList<InnerPowerRankEntry> ReadInnerPowerRanks(
        SeedManifest manifest,
        IReadOnlyList<InnerPowerRankEntry> exportedRows)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(exportedRows);

        if (string.IsNullOrWhiteSpace(manifest.SourcePath) ||
            !Directory.Exists(manifest.SourcePath))
        {
            return exportedRows;
        }

        var path = Path.Combine(manifest.SourcePath, "InnerPower.txt");
        if (!File.Exists(path))
        {
            return exportedRows;
        }

        try
        {
            return RankingSnapshotParser.ParseInnerPowerRanksFile(path);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException($"Mutable InnerPower data is invalid: {exception.Message}", exception);
        }
    }
}
