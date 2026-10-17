namespace WolfConquer.Core.Data;

public sealed record SobNpc(
    int Id,
    string Name,
    int Kind,
    int Lookface,
    int MapId,
    int X,
    int Y,
    int Life,
    int MaxLife,
    int Unknown9,
    int? Unknown10,
    string? LocalizedName,
    int SourceLine,
    string RawLine,
    IReadOnlyList<string> Fields);
