namespace WolfConquer.Core.Data;

public sealed record CollectionRewardTier(
    int Id,
    int CollectionId,
    int MinInclusive,
    int MaxInclusive,
    int RewardType,
    int RewardValue,
    int RewardAmount,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
