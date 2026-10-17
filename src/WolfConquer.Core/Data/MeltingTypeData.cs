namespace WolfConquer.Core.Data;

public sealed record MeltingTypeRule(
    int RuleType,
    int ItemTypeId,
    IReadOnlyList<int> Parameters,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
