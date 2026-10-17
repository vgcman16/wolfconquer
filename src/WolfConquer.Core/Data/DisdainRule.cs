namespace WolfConquer.Core.Data;

public sealed record DisdainRule(
    int Id,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
