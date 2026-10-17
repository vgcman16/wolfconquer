namespace WolfConquer.Core.Data;

public sealed record SyndicateFormationType(
    int Id,
    int Type,
    int Level,
    IReadOnlyList<long> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
