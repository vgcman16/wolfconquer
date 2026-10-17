namespace WolfConquer.Core.Data;

public sealed record MagicEffect(
    long Id,
    int Occurrence,
    string? Name,
    string? Description,
    string? DescriptionEx,
    string SourceFile,
    int SourceLine,
    string SourceSection,
    IReadOnlyList<MagicEffectEntry> Entries,
    IReadOnlyDictionary<string, string> Attributes);

public sealed record MagicEffectEntry(
    string Key,
    string Value,
    string SourceFile,
    int SourceLine,
    string RawLine);
