namespace WolfConquer.Core.Data;

public sealed record ItemTypeSub(
    int ItemTypeId,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
