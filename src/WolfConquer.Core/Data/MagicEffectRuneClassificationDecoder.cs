using System.Globalization;

namespace WolfConquer.Core.Data;

public static class MagicEffectRuneClassificationDecoder
{
    public const string WarningCode = "AmbiguousRuneSkillClassification";
    private const string Flag = "IsRuneSkill";

    public static MagicEffectRuneClassificationAnalysis Parse(string text, long sectionId,
        string sourceFile = "MagicEffect.ini") => Analyze(MagicEffectParser.Parse(text, sourceFile), sectionId);

    public static MagicEffectRuneClassificationAnalysis Analyze(IReadOnlyList<MagicEffect> source, long sectionId)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (sectionId < 0) throw new ArgumentOutOfRangeException(nameof(sectionId));
        var sections = source.Where(row => row is not null && row.Id == sectionId).ToArray();
        var declarations = new List<MagicEffectRuneFlagDeclaration>();
        var occurrences = new HashSet<int>(); bool malformed = false, ambiguous = false;
        foreach (var section in sections)
        {
            if (section.SourceSection != sectionId.ToString(CultureInfo.InvariantCulture) || section.Occurrence < 1 ||
                !occurrences.Add(section.Occurrence)) malformed = true;
            if (section.Entries is null || section.Attributes is null) { malformed = true; continue; }
            if (section.Entries.Any(entry => entry is null)) malformed = true;
            var entries = section.Entries.Where(entry => entry is not null && IsFlag(entry.Key)).ToArray();
            var attributes = section.Attributes.Where(field => IsFlag(field.Key)).ToArray();
            ambiguous |= entries.Length > 1 || attributes.Length > 1;
            if (entries.Length == 0 && attributes.Length != 0 || entries.Length != 0 && attributes.Length == 0) malformed = true;
            foreach (var entry in entries)
            {
                var rawLine = entry.RawLine; var equals = rawLine?.IndexOf('=') ?? -1;
                bool parsedByte = byte.TryParse(entry.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var rawByte);
                bool valid = rawLine is not null && equals > 0 && rawLine[..equals].Trim() == entry.Key &&
                    rawLine[(equals + 1)..].Trim() == entry.Value && parsedByte;
                byte? parsed = valid ? rawByte : null;
                declarations.Add(new(section.Occurrence, entry.SourceFile, entry.SourceLine, entry.Key, entry.Value, rawLine, parsed));
                if (!valid || entries.Length == 1 && attributes.Length == 1 && attributes[0].Value != entry.Value) malformed = true;
            }
        }
        var status = malformed ? MagicEffectRuneClassificationStatus.Malformed :
            sections.Length > 1 || ambiguous ? MagicEffectRuneClassificationStatus.Ambiguous :
            declarations.Count == 1 ? MagicEffectRuneClassificationStatus.UniqueExplicit : MagicEffectRuneClassificationStatus.Missing;
        var uniqueValue = status == MagicEffectRuneClassificationStatus.UniqueExplicit ? declarations[0].ParsedValue : null;
        return new(sectionId, sections.Length, status, uniqueValue, declarations.ToArray());
    }

    public static IReadOnlyList<WorldDataDiagnostic> Warnings(IReadOnlyList<MagicEffect> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var warnings = new List<WorldDataDiagnostic>();
        foreach (var group in source.Where(row => row is not null && row.Id > 0 && row.Id % 100 == 0 && row.Id / 100 <= ushort.MaxValue)
                     .GroupBy(row => row.Id))
        {
            var rows = group.ToArray();
            // Missing ordinary metadata and duplicate ordinary effect sections
            // remain untouched. Only explicit Rune declarations are diagnosed.
            if (!rows.Any(row => row.Entries is not null && row.Entries.Any(entry => entry is not null && IsFlag(entry.Key)) ||
                row.Attributes is not null && row.Attributes.Any(field => IsFlag(field.Key)))) continue;
            var result = Analyze(rows, group.Key);
            if (result.Status is not (MagicEffectRuneClassificationStatus.Ambiguous or MagicEffectRuneClassificationStatus.Malformed)) continue;
            warnings.Add(new(WarningCode, $"WARNING: MagicEffect [{group.Key}] has {result.Status.ToString().ToLowerInvariant()} explicit Rune classification " +
                $"({result.SectionCount} section occurrences, {result.Declarations.Count} raw declarations); classification remains unresolved."));
        }
        return warnings.ToArray();
    }

    private static bool IsFlag(string? key) => string.Equals(key, Flag, StringComparison.OrdinalIgnoreCase);
}
