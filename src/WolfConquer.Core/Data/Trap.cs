namespace WolfConquer.Core.Data;

public sealed record Trap(
    int MapId,
    int X,
    int Y,
    int TrapTypeId,
    int? OptionalFlag,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
