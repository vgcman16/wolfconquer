namespace WolfConquer.Core.Data;

public sealed record MapMetadata(
    int Id,
    string SourceRelativePath,
    string? Name,
    int MapDoc,
    string TypeRaw,
    long Type,
    int? Portal0X,
    int? Portal0Y,
    int RebornMap,
    int RebornPortal,
    int ResLev,
    int LinkMap,
    int LinkX,
    int LinkY,
    string ColorRaw,
    long Color,
    IReadOnlyDictionary<string, string> Attributes);
