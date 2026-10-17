namespace WolfConquer.Core.Data;

public sealed record SyndicateLevel(
    int Level,
    IReadOnlyList<int> Parameters,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
