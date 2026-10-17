namespace WolfConquer.Core.Data;

public sealed record StaticStatueRecord(
    int RecordId,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
