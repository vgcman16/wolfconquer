namespace WolfConquer.Core.Data;

public sealed record JiangHuAttribute(
    int Id,
    int AttributeType,
    int Level,
    int Value,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record JiangHuCultivateStatus(
    int Id,
    int DeclaredValueCount,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
