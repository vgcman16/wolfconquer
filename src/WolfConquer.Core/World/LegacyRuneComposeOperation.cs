namespace WolfConquer.Core.World;

internal sealed record LegacyRuneComposeProgressRow(uint Family, byte Level, uint Progress);
internal sealed record LegacyRuneComposeMaterialMetadata(uint ItemId, ushort MaximumStackSize);

internal sealed record LegacyRuneComposeOperation(uint SelectedUid, uint[] Materials,
    LegacyRuneMetadata[] Metadata, LegacyRuneComposeProgressRow[] Progress, LegacyRuneComposeMaterialMetadata[] MaterialMetadata)
{
    internal void Validate()
    {
        if (SelectedUid == 0 || Materials is null || Materials.Length > 49 ||
            Materials.Any(uid => uid == 0 || uid == SelectedUid) || Materials.Distinct().Count() != Materials.Length ||
            Progress is null || Progress.Length > 128 || MaterialMetadata is null || MaterialMetadata.Length > 49)
            throw new InvalidDataException("Invalid recorded Rune compose inputs.");
        LegacyRuneOperation.ValidateMetadata(Metadata);
        uint family = 0; byte level = 0;
        foreach (var row in Progress)
        {
            if (row is null || row.Family is < 401 or > 410 || row.Level is 0 or > 99 ||
                row.Family < family || row.Family == family && row.Level <= level)
                throw new InvalidDataException("Invalid, duplicate or unordered Rune compose progress input.");
            family = row.Family; level = row.Level;
        }
        uint previous = 0;
        foreach (var row in MaterialMetadata)
        {
            if (row is null || row.ItemId is < 4_010_000 or > 4_100_000 || row.ItemId <= previous)
                throw new InvalidDataException("Invalid, duplicate or unordered Rune compose material cap input.");
            previous = row.ItemId;
        }
    }
}
