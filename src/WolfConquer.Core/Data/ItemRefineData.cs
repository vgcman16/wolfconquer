namespace WolfConquer.Core.Data;

public sealed record ItemRefineEffect(
    int Id,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record ItemRefineEffectEx(
    int Id,
    int ItemTypeId,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record ItemRefineUpgrade(
    int Id,
    int Level,
    int Value,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
