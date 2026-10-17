namespace WolfConquer.Core.Data;

public sealed record CombatGear(
    int Id,
    int GearTypeId,
    int Level,
    int UpExperience,
    int Life,
    int Attack,
    int Defense,
    int UnknownStat0,
    int MagicDefense,
    IReadOnlyList<int> MagicTypeIds,
    int Score,
    int Unknown0,
    int Unknown1,
    int Unknown2,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
