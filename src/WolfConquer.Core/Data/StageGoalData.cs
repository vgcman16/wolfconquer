namespace WolfConquer.Core.Data;

public sealed record StageGoalCatalog(
    StageGoalInfo Info,
    IReadOnlyList<StageGoalStage> Stages,
    IReadOnlyList<StageGoalTask> Tasks);

public sealed record StageGoalInfo(int StageAmount);

public sealed record StageGoalStage(
    int StageId,
    string OpenLevelRaw,
    int? OpenLevel,
    string StageAnimation,
    int GoalCount,
    IReadOnlyList<StageGoalReward> Rewards,
    int SourceLine,
    IReadOnlyList<StageGoalRawEntry> Entries);

public sealed record StageGoalTask(
    int StageId,
    int GoalId,
    string Title,
    string Intro,
    int Progress,
    IReadOnlyList<StageGoalReward> Rewards,
    int TaskType,
    string? RequireRaw,
    int? Require,
    int SourceLine,
    IReadOnlyList<StageGoalRawEntry> Entries);

public sealed record StageGoalReward(
    int Position,
    int ItemTypeId,
    int Amount,
    int BoundFlag,
    string Key,
    string RawValue,
    int SourceLine,
    IReadOnlyList<string> Fields);

public sealed record StageGoalRawEntry(string Key, string Value, int SourceLine, string RawLine);
