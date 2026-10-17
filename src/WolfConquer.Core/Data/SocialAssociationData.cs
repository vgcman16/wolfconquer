namespace WolfConquer.Core.Data;

public sealed record SocialAssociationRecord(
    int OwnerId,
    int RelationType,
    int TargetId,
    string TargetName,
    IReadOnlyList<int> Values,
    string Note,
    int TailValue,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
