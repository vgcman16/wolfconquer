using System.Globalization;
using System.Text;

namespace WolfConquer.Core.Data;

public static class MagicEffectParser
{
    private static readonly Encoding MagicEffectEncoding = CreateMagicEffectEncoding();

    public static IReadOnlyList<MagicEffect> ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Missing input file: {path}", path);
        }

        var text = MagicEffectEncoding.GetString(File.ReadAllBytes(path));
        return Parse(text, Path.GetFileName(path));
    }

    public static IReadOnlyList<MagicEffect> Parse(string text, string sourceFile = "MagicEffect.ini")
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        var effects = new List<MagicEffect>();
        var occurrencesById = new Dictionary<long, int>();
        long? currentId = null;
        var currentOccurrence = 0;
        var currentSourceLine = 0;
        string? currentSourceSection = null;
        List<MagicEffectEntry>? currentEntries = null;
        Dictionary<string, string>? currentAttributes = null;

        using var reader = new StringReader(text);
        var lineNumber = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//", StringComparison.Ordinal) ||
                trimmed.StartsWith(';') || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                FlushCurrentSection();
                var rawSection = trimmed[1..^1].Trim();
                if (!long.TryParse(rawSection, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
                {
                    throw new FormatException($"{sourceFile} line {lineNumber}: invalid MagicEffect section id '{rawSection}'.");
                }

                currentId = id;
                currentOccurrence = occurrencesById.TryGetValue(id, out var previousOccurrence)
                    ? previousOccurrence + 1
                    : 1;
                occurrencesById[id] = currentOccurrence;
                currentSourceLine = lineNumber;
                currentSourceSection = rawSection;
                currentEntries = [];
                currentAttributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator < 0)
            {
                throw new FormatException($"{sourceFile} line {lineNumber}: expected key=value inside a MagicEffect section.");
            }

            if (currentId is null || currentEntries is null || currentAttributes is null)
            {
                throw new FormatException($"{sourceFile} line {lineNumber}: key/value row appears before any section.");
            }

            var key = line[..separator].Trim();
            if (key.Length == 0)
            {
                throw new FormatException($"{sourceFile} line {lineNumber}: MagicEffect key cannot be empty.");
            }

            var value = line[(separator + 1)..].Trim();
            currentEntries.Add(new MagicEffectEntry(key, value, sourceFile, lineNumber, line));
            currentAttributes[key] = value;
        }

        FlushCurrentSection();
        return effects;

        void FlushCurrentSection()
        {
            if (currentId is null || currentEntries is null || currentAttributes is null || currentSourceSection is null)
            {
                return;
            }

            currentAttributes.TryGetValue("Name", out var name);
            currentAttributes.TryGetValue("Desc", out var description);
            currentAttributes.TryGetValue("DescEx", out var descriptionEx);
            effects.Add(new MagicEffect(
                currentId.Value,
                currentOccurrence,
                name,
                description,
                descriptionEx,
                sourceFile,
                currentSourceLine,
                currentSourceSection,
                currentEntries.ToArray(),
                new Dictionary<string, string>(currentAttributes, StringComparer.OrdinalIgnoreCase)));
        }
    }

    private static Encoding CreateMagicEffectEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(
            936,
            EncoderFallback.ExceptionFallback,
            DecoderFallback.ExceptionFallback);
    }
}
