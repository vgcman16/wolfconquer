namespace WolfConquer.Core.Data;

public sealed record ChristmasNpc(
    int Id,
    string? Name,
    int Kind,
    int Lookface,
    int MapId,
    int X,
    int Y,
    IReadOnlyList<int> Parameters,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
