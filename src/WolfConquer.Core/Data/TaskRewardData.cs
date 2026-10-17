namespace WolfConquer.Core.Data;

public sealed record TaskRewardTypeEntry(
    int Id,
    IReadOnlyList<int> Parameters,
    IReadOnlyList<int> RewardValues,
    IReadOnlyList<int> RewardWeights,
    int Extra0,
    int Extra1,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record TaskRewardTypeIniEntry(
    int Id,
    IReadOnlyList<int> Parameters,
    IReadOnlyList<int> RewardValues,
    IReadOnlyList<int> RewardWeights,
    int Extra0,
    int Extra1,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
