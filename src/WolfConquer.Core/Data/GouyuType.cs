namespace WolfConquer.Core.Data;

public sealed record GouyuType(
    int Id,
    int Type,
    int Level,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
