namespace WolfConquer.Core.Data;

public sealed record RuneRankingRecord(
    string CharacterName,
    int Score,
    int CharacterId,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
