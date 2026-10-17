namespace WolfConquer.Core.Data;

public sealed record FateExperience(
    int Id,
    int Type,
    int Level,
    long Experience,
    IReadOnlyList<long> Values,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
