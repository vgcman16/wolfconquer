namespace WolfConquer.Core.Data;

public enum MagicEffectRuneClassificationStatus
{
    Missing,
    UniqueExplicit,
    Ambiguous,
    Malformed,
}

// Detached ingestion evidence, never a catalog/file owner or permission token.
// A missing/ambiguous value is deliberately not represented as false or byte0.
public sealed record MagicEffectRuneFlagDeclaration(
    int Occurrence, string SourceFile, int SourceLine, string Key, string Value,
    string? RawLine, byte? ParsedValue);

public sealed record MagicEffectRuneClassificationAnalysis(
    long SectionId, int SectionCount, MagicEffectRuneClassificationStatus Status,
    byte? UniqueValue, IReadOnlyList<MagicEffectRuneFlagDeclaration> Declarations);
