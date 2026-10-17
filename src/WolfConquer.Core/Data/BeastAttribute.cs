namespace WolfConquer.Core.Data;

public sealed record BeastAttribute(
    int Level,
    IReadOnlyList<int> Parameters,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
