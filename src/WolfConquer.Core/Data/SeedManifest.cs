namespace WolfConquer.Core.Data;

public sealed record SeedManifest(
    DateTimeOffset ExportedAtUtc,
    string SourcePath,
    IReadOnlyDictionary<string, int> Counts);
