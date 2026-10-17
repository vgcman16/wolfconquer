namespace WolfConquer.Core.Data;

public sealed record SubProfessionCatalog(
    IReadOnlyList<SubProfessionEntry> Entries,
    string SourceFile);

public sealed record SubProfessionEntry(
    int Id,
    string Title,
    string Intro,
    int RequiredLevel,
    int RequiredMetempsychosis,
    IReadOnlyList<int> RequiredProfessions,
    string Promotion,
    int Suit,
    int LevelLimit,
    int? RequiredJobLevel,
    int? EffectProportion,
    string? UpLevelEffect,
    string? UpStepEffect,
    string? UpStepScreenEffect,
    string? UpEffect,
    string? SubSpecial,
    int? SuitPower,
    int? BigClass,
    SubProfessionNpcRoute? AccessionRoute,
    SubProfessionNpcRoute? StepUpRoute,
    string? AnimationSection,
    string SourceFile,
    int LineNumber,
    string RawSectionName,
    IReadOnlyList<SubProfessionRawEntry> Entries);

public sealed record SubProfessionRawEntry(
    string Key,
    string Value,
    string SourceFile,
    int LineNumber,
    string RawLine);

public sealed record SubProfessionNpcRoute(
    int MapId,
    int NpcId,
    int X,
    int Y);
