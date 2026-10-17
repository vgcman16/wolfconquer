namespace WolfConquer.Core.Data;

public sealed record ClassLevelStat(
    string ClassName,
    int Level,
    int Strength,
    int Agility,
    int Vitality,
    int Spirit,
    string SourceFile,
    int LineNumber,
    string RawKey,
    string RawValue,
    string RawLine,
    IReadOnlyList<string> Fields);
