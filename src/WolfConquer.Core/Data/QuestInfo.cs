namespace WolfConquer.Core.Data;

public sealed record QuestInfoCatalog(
    QuestInfoMetadata Metadata,
    IReadOnlyList<QuestTaskType> TaskTypes,
    IReadOnlyList<QuestMission> Missions);

public sealed record QuestInfoMetadata(
    int? ActionBase,
    int? RecommendInterval,
    int? TotalMission);

public sealed record QuestTaskType(
    int Id,
    string Name,
    string? TaskNameColor,
    int Position,
    int SourceLine,
    string RawIdKey,
    string RawNameKey);

public sealed record QuestMission(
    int SectionId,
    int SourceLine,
    int TypeId,
    int MissionId,
    string? Name,
    int? LevelMin,
    int? LevelMax,
    int? Auto,
    int? First,
    string? PrerequisiteRaw,
    IReadOnlyList<int> PrerequisiteMissionIds,
    int? MapId,
    string? Profession,
    int? Sex,
    int? FinishTime,
    string? ActivityBeginTime,
    string? ActivityEndTime,
    int? CompleteFlag,
    string? Prize,
    string? IntentionDescription,
    int? IntentAmount,
    string? Content,
    IReadOnlyList<QuestNpcAnchor> BeginNpcAnchors,
    IReadOnlyList<QuestNpcAnchor> FinishNpcAnchors,
    IReadOnlyList<QuestIntention> Intentions,
    IReadOnlyList<QuestPrizeItemRef> PrizeItems,
    IReadOnlyList<QuestRawEntry> Entries);

public sealed record QuestRawEntry(
    string Key,
    string Value,
    int SourceLine,
    string RawLine);

public sealed record QuestNpcAnchor(
    string Kind,
    string Key,
    int? Variant,
    int? NpcId,
    int? MapId,
    int? X,
    int? Y,
    string? Name,
    string? MapName,
    string RawValue,
    int SourceLine,
    IReadOnlyList<string> Fields);

public sealed record QuestIntention(
    int Position,
    int? Variant,
    string Key,
    string Text,
    int SourceLine);

public sealed record QuestPrizeItemRef(
    int ItemTypeId,
    string RawArguments,
    int Position);
