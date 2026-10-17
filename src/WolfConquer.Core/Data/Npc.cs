namespace WolfConquer.Core.Data;

public sealed record Npc(
    int Id,
    int Kind,
    int Lookface,
    int MapId,
    int X,
    int Y,
    int? OptionalFlag,
    int SourceLine = 0,
    string RawLine = "",
    IReadOnlyList<string>? Fields = null);
