namespace WolfConquer.Core.Data;

public sealed record RuneLevelExperience(
    int Id,
    int RuneType,
    int Level,
    long Experience,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record RuneStorageAttribute(
    int Id,
    int RequiredStorageScore,
    IReadOnlyList<RuneStorageAttributeBonus> Bonuses,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record RuneStorageAttributeBonus(
    int AttributeType,
    int Value);
