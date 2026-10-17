namespace WolfConquer.Core.Data;

public sealed record TitleType(
    int Id,
    int TypeId,
    string Name,
    int Unknown0,
    int Unknown1,
    int Unknown2,
    int Unknown3,
    int Score,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
