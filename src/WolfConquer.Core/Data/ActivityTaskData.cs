namespace WolfConquer.Core.Data;

public sealed record ActivityTaskCatalog(
    ActivityTaskMetadata Metadata,
    IReadOnlyList<ActivityTaskDefinition> Tasks,
    string SourceFile);

public sealed record ActivityTaskMetadata(
    int DeclaredCount,
    string SourceSection,
    int SourceLine,
    IReadOnlyList<ActivityTaskRawEntry> Entries);

public sealed record ActivityTaskDefinition(
    int SectionId,
    int Id,
    string Title,
    int Type,
    int Progress,
    int ActiveValue,
    string CloseLevelRaw,
    int CloseLevel,
    string Tip,
    string? OpenLevelRaw,
    int? OpenLevel,
    string? PreviewRaw,
    int? Preview,
    string? Intro,
    string? AnimationSection,
    string? Param,
    string SourceSection,
    int SourceLine,
    IReadOnlyList<ActivityTaskRawEntry> Entries);

public sealed record ActivityTaskRawEntry(string Key, string Value, int SourceLine, string RawLine);
