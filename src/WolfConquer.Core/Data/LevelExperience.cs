namespace WolfConquer.Core.Data;

public sealed record LevelExperience(
    int Type,
    int Level,
    long Experience,
    int UpLevTime,
    int MentorUpLevTime,
    int SourceLine,
    string RawLine,
    IReadOnlyList<string> Fields);
