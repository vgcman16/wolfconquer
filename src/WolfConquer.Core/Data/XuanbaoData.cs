namespace WolfConquer.Core.Data;

public sealed record XuanbaoAdditionAttribute(
    int Id,
    int AttributeType,
    int Level,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record XuanbaoComposeAttributeLimit(
    int Id,
    IReadOnlyList<int> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record XuanbaoRandomAttribute(
    int ItemTypeId,
    int AttributeType,
    int MinValue,
    int MaxValue,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
