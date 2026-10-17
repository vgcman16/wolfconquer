namespace WolfConquer.Core.Data;

public sealed record StateCountFileData(
    string SourceFile,
    int DeclaredCount,
    IReadOnlyList<StateCountFileEntry> Entries,
    IReadOnlyList<string> RawLines);

public sealed record StateCountFileEntry(
    int SourceLine,
    string RawLine,
    IReadOnlyList<string> Fields);
