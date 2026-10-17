namespace WolfConquer.Core.Data;

public sealed record TutorType(
    int Id,
    int MinLevel,
    int MaxLevel,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record TutorBattleLimitType(
    int Id,
    int BattlePowerLimit,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
