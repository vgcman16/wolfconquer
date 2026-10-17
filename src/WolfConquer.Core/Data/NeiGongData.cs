namespace WolfConquer.Core.Data;

public sealed record NeiGongCatalog(
    NeiGongHeader Header,
    IReadOnlyList<NeiGongSecret> Secrets,
    IReadOnlyList<NeiGongType> Types);

public sealed record NeiGongHeader(
    int SecretCount,
    IReadOnlyList<NeiGongAttributeTip> AttributeTips,
    string SourceFile,
    int LineNumber,
    string RawSectionName,
    IReadOnlyDictionary<string, string> Fields);

public sealed record NeiGongAttributeTip(
    int AttributeType,
    string Text);

public sealed record NeiGongSecret(
    int Id,
    string Name,
    string AniSection,
    int NeiGongCount,
    IReadOnlyList<int> AttributeTypes,
    IReadOnlyList<int> AttributeValues,
    string GetWayContent,
    string SourceFile,
    int LineNumber,
    string RawSectionName,
    IReadOnlyDictionary<string, string> Fields);

public sealed record NeiGongType(
    int SecretId,
    int Slot,
    int TypeId,
    string Name,
    string AniSection,
    int MaxLevel,
    IReadOnlyList<int> AttributeTypes,
    IReadOnlyList<int> AttributeValues,
    IReadOnlyList<int> CultureValues,
    string Condition,
    int RequiredLevel,
    int RequiredNeiGongValue,
    int RequiredPrerequisiteNeiGong,
    int RequiredItemTypeId,
    string SourceFile,
    int LineNumber,
    string RawSectionName,
    IReadOnlyDictionary<string, string> Fields);
