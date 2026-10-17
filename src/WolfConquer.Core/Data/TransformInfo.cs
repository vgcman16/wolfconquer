namespace WolfConquer.Core.Data;

public sealed record TransformInfo(
    int TransformId,
    int Level,
    string Name,
    int Lookface,
    int MaxLife,
    string SourceFile,
    int LineNumber,
    string RawLine,
    IReadOnlyList<string> Fields);
