namespace WolfConquer.Core.Data;

public sealed record Furniture(
    int Id,
    int Kind,
    int Lookface,
    int MapId,
    int X,
    int Y,
    string Name,
    int ItemTypeId,
    int Price,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
