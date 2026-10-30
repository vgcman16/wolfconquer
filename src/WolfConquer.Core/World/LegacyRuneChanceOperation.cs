namespace WolfConquer.Core.World;

internal sealed record LegacyRuneChanceOperation(uint SelectedUid, LegacyRuneMetadata[] Metadata,
    LegacyRuneChanceProgressRow[] Progress, byte? Roll)
{
    internal void Validate()
    {
        if (SelectedUid == 0 || Progress is null || Progress.Length > 2 || Roll is >= 100)
            throw new InvalidDataException("Invalid recorded Rune chance inputs.");
        LegacyRuneOperation.ValidateMetadata(Metadata);
        uint family = 0; byte level = 0;
        foreach (var row in Progress)
        {
            if (row is null || row.Family is < 401 or > 403 || row.Level is 0 or > 27 ||
                row.Family < family || row.Family == family && row.Level <= level)
                throw new InvalidDataException("Invalid, duplicate or unordered Rune chance progress.");
            family = row.Family; level = row.Level;
        }
    }
}
