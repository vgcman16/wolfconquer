namespace WolfConquer.Core.Data;

public sealed record HairColorType(
    int HairColorId,
    int ColorIndex,
    int UnlockType,
    int ItemTypeId,
    int Cost,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record HairFaceStorageType(
    int CategoryType,
    int Id,
    int Unknown0,
    string Name,
    int UnlockType,
    int RequiredValue,
    int Unknown1,
    int Cost,
    int Unknown2,
    string LinkedColorIdsRaw,
    int Sort,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record CosmeticAttributeBonus(int AttributeType, int Value);

public sealed record CoatStorageType(
    int ItemTypeId,
    long StorageType,
    IReadOnlyList<int> Parameters,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record CoatStorageAttribute(
    int Id,
    long StorageType,
    int RequiredStorageScore,
    IReadOnlyList<CosmeticAttributeBonus> Bonuses,
    int Reserved0,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record CoatColorRule(
    int ItemTypeId,
    IReadOnlyList<int> Parameters,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
