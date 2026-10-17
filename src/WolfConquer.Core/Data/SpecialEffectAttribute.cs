namespace WolfConquer.Core.Data;

public sealed record SpecialEffectAttribute(
    int Id,
    int Type,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
