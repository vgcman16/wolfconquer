namespace WolfConquer.Core.Data;

public sealed record MagicTypeOperationEntry(
    int Id,
    int Mode,
    int Group,
    int Level,
    int Flag,
    IReadOnlyList<int> ReferencedMagicTypeIds,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
