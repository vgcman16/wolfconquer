namespace WolfConquer.Core.Data;

public sealed record ProfessionLevelUp(
    int Id,
    int ProfessionCode,
    int RequiredLevel,
    int RequiredMetempsychosis,
    int RequiredMoney,
    int RequiredProficiency,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record ProfessionLevelBenefit(
    int Id,
    int ProfessionCode,
    int Rank,
    IReadOnlyList<int> BonusValues,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);

public sealed record ProfessionTitleBenefit(
    int Id,
    int ProfessionGroup,
    int Rank,
    IReadOnlyList<int> BonusValues,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
