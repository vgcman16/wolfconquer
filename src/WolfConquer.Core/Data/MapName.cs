namespace WolfConquer.Core.Data;

public sealed record MapName(
    int MapId,
    string Name,
    string SourceFile,
    int SectionLine,
    int NameLine,
    string SourceSection,
    string RawNameLine);
