namespace WolfConquer.Core.Data;

public sealed record SpiritRate(
    int Id,
    int Type,
    int ItemTypeId,
    IReadOnlyList<int> Parameters,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
