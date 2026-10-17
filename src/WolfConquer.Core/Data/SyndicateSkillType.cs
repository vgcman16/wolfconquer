namespace WolfConquer.Core.Data;

public sealed record SyndicateSkillType(
    int Id,
    int SkillType,
    int Level,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
