namespace WolfConquer.Core.Data;

public sealed record RankingSnapshotData(
    IReadOnlyList<CombatGearRankEntry> CombatGearRanks,
    IReadOnlyList<PrestigeRankEntry> PrestigeRanks,
    IReadOnlyList<InnerPowerRankEntry> InnerPowerRanks,
    IReadOnlyList<NewPlayerRewardRecord> NewPlayerRewards,
    IReadOnlyList<VoteRecord> Votes,
    IReadOnlyList<CouplesPkRecord> CouplesPkRecords);

public sealed record CombatGearRankEntry(
    int CharacterId,
    string CharacterName,
    IReadOnlyList<string> RankingFields,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record PrestigeRankEntry(
    int CharacterId,
    string CharacterName,
    IReadOnlyList<string> RankingFields,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record InnerPowerRankEntry(
    string CharacterName,
    int CharacterId,
    IReadOnlyList<string> RankingFields,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record NewPlayerRewardRecord(
    string IpAddress,
    string DeviceId,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record VoteRecord(
    int CharacterId,
    string IpAddress,
    string Timestamp,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record CouplesPkRecord(
    string PlayerName,
    string PartnerName,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
