using System.Globalization;
using System.Text;

internal static partial class Program
{
    private static readonly string[] GeneratedBufferStringScanDefaultTerms =
    [
        "CLuaVM::requirebuffer",
        "ToStringEx",
        "MicroSpotData",
        "basedef.lua",
        "c_require",
    ];

    private const string GeneratedBufferStringScanUsage =
        "Usage: WolfConquer.DataTool generated-buffer-string-scan [--term text] [--context-bytes N] [--max-hits N] <path|label=path> [...]";

    private const int GeneratedBufferStringScanDefaultContextBytes = 48;
    private const int GeneratedBufferStringScanMaxContextBytes = 4096;
    private const int GeneratedBufferStringScanDefaultMaxHits = 80;
    private const int GeneratedBufferStringScanMaxHitsLimit = 10000;
    private const int GeneratedBufferStringScanSummaryOffsetCount = 12;

    static int GeneratedBufferStringScan(string[] args)
    {
        GeneratedBufferStringScanOptions options;
        try
        {
            options = ResolveGeneratedBufferStringScanOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(GeneratedBufferStringScanUsage);
            return 1;
        }

        foreach (var input in options.Inputs)
        {
            if (!File.Exists(input.Path))
            {
                Console.Error.WriteLine($"Input file not found: label={input.Label} path={input.Path}");
                return 1;
            }
        }

        try
        {
            PrintGeneratedBufferStringScan(options);
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Generated buffer string scan failed: {ex.Message}");
            return 1;
        }
    }

    static GeneratedBufferStringScanOptions ResolveGeneratedBufferStringScanOptions(string[] args)
    {
        var terms = new List<string>();
        var inputs = new List<GeneratedBufferStringScanInput>();
        var contextBytes = GeneratedBufferStringScanDefaultContextBytes;
        var maxHits = GeneratedBufferStringScanDefaultMaxHits;

        for (var index = 1; index < args.Length; index++)
        {
            var argument = args[index];
            if (string.Equals(argument, "--term", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("generated-buffer-string-scan --term requires text.");
                }

                AddGeneratedBufferStringScanTerm(terms, args[++index]);
                continue;
            }

            const string termPrefix = "--term=";
            if (argument.StartsWith(termPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddGeneratedBufferStringScanTerm(terms, argument[termPrefix.Length..]);
                continue;
            }

            if (string.Equals(argument, "--context-bytes", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("generated-buffer-string-scan --context-bytes requires a byte count.");
                }

                contextBytes = ParseGeneratedBufferStringScanInteger(
                    args[++index],
                    "--context-bytes",
                    minimum: 0,
                    maximum: GeneratedBufferStringScanMaxContextBytes);
                continue;
            }

            const string contextPrefix = "--context-bytes=";
            if (argument.StartsWith(contextPrefix, StringComparison.OrdinalIgnoreCase))
            {
                contextBytes = ParseGeneratedBufferStringScanInteger(
                    argument[contextPrefix.Length..],
                    "--context-bytes",
                    minimum: 0,
                    maximum: GeneratedBufferStringScanMaxContextBytes);
                continue;
            }

            if (string.Equals(argument, "--max-hits", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                {
                    throw new ArgumentException("generated-buffer-string-scan --max-hits requires a hit count.");
                }

                maxHits = ParseGeneratedBufferStringScanInteger(
                    args[++index],
                    "--max-hits",
                    minimum: 0,
                    maximum: GeneratedBufferStringScanMaxHitsLimit);
                continue;
            }

            const string maxHitsPrefix = "--max-hits=";
            if (argument.StartsWith(maxHitsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                maxHits = ParseGeneratedBufferStringScanInteger(
                    argument[maxHitsPrefix.Length..],
                    "--max-hits",
                    minimum: 0,
                    maximum: GeneratedBufferStringScanMaxHitsLimit);
                continue;
            }

            if (argument.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unknown generated-buffer-string-scan option: {argument}");
            }

            inputs.Add(ParseGeneratedBufferStringScanInput(argument));
        }

        if (inputs.Count == 0)
        {
            throw new ArgumentException("generated-buffer-string-scan requires at least one input file.");
        }

        if (terms.Count == 0)
        {
            foreach (var term in GeneratedBufferStringScanDefaultTerms)
            {
                AddGeneratedBufferStringScanTerm(terms, term);
            }
        }

        return new GeneratedBufferStringScanOptions(inputs, terms, contextBytes, maxHits);
    }

    private static void AddGeneratedBufferStringScanTerm(List<string> terms, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("generated-buffer-string-scan terms must be non-empty.");
        }

        if (value.Any(c => c > sbyte.MaxValue))
        {
            throw new ArgumentException("generated-buffer-string-scan terms must be ASCII so byte and UTF-16LE scans are comparable.");
        }

        if (!terms.Contains(value, StringComparer.Ordinal))
        {
            terms.Add(value);
        }
    }

    private static int ParseGeneratedBufferStringScanInteger(
        string value,
        string optionName,
        int minimum,
        int maximum)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) ||
            parsed < minimum ||
            parsed > maximum)
        {
            throw new ArgumentException(
                $"generated-buffer-string-scan {optionName} must be between {minimum.ToString(CultureInfo.InvariantCulture)} and {maximum.ToString(CultureInfo.InvariantCulture)}.");
        }

        return parsed;
    }

    private static GeneratedBufferStringScanInput ParseGeneratedBufferStringScanInput(string value)
    {
        var separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex > 0 && separatorIndex < value.Length - 1)
        {
            var label = value[..separatorIndex].Trim();
            if (label.Length == 0 || label.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException("generated-buffer-string-scan labels must be non-empty and contain no whitespace.");
            }

            return new GeneratedBufferStringScanInput(label, Path.GetFullPath(value[(separatorIndex + 1)..]));
        }

        var path = Path.GetFullPath(value);
        var inferredLabel = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(inferredLabel))
        {
            inferredLabel = "input";
        }

        return new GeneratedBufferStringScanInput(inferredLabel, path);
    }

    private static void PrintGeneratedBufferStringScan(GeneratedBufferStringScanOptions options)
    {
        var patterns = CreateGeneratedBufferStringScanPatterns(options.Terms);
        var displayedHits = 0;
        var totalHits = 0;

        Console.WriteLine("Generated buffer string scan:");
        Console.WriteLine($"Terms: {string.Join(", ", options.Terms)}");
        Console.WriteLine("Encodings: ascii, utf-16le");
        Console.WriteLine($"Context bytes: {options.ContextBytes.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Max hits: {options.MaxHits.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Inputs: {options.Inputs.Count.ToString(CultureInfo.InvariantCulture)}");

        foreach (var input in options.Inputs)
        {
            var bytes = File.ReadAllBytes(input.Path);
            Console.WriteLine(
                $"File: label={input.Label} path={input.Path} length={bytes.Length.ToString(CultureInfo.InvariantCulture)}");

            foreach (var pattern in patterns)
            {
                var offsets = FindBytePatternOffsets(bytes, pattern.Bytes);
                totalHits += offsets.Count;
                Console.WriteLine(
                    $"Match: label={input.Label} term={pattern.Term} encoding={pattern.EncodingName} " +
                    $"count={offsets.Count.ToString(CultureInfo.InvariantCulture)} " +
                    $"offsets={FormatGeneratedBufferStringScanOffsets(offsets)}");

                foreach (var offset in offsets)
                {
                    if (displayedHits >= options.MaxHits)
                    {
                        continue;
                    }

                    PrintGeneratedBufferStringScanHit(bytes, input, pattern, offset, options.ContextBytes);
                    displayedHits++;
                }
            }
        }

        Console.WriteLine(
            $"Displayed hits: {displayedHits.ToString(CultureInfo.InvariantCulture)} of {totalHits.ToString(CultureInfo.InvariantCulture)} " +
            $"(max {options.MaxHits.ToString(CultureInfo.InvariantCulture)})");
        if (totalHits == 0)
        {
            Console.WriteLine("No generated-buffer string hits found.");
        }
    }

    private static IReadOnlyList<GeneratedBufferStringScanPattern> CreateGeneratedBufferStringScanPatterns(
        IReadOnlyList<string> terms)
    {
        var patterns = new List<GeneratedBufferStringScanPattern>(terms.Count * 2);
        foreach (var term in terms)
        {
            patterns.Add(new GeneratedBufferStringScanPattern(term, "ascii", Encoding.ASCII.GetBytes(term)));
            patterns.Add(new GeneratedBufferStringScanPattern(term, "utf-16le", Encoding.Unicode.GetBytes(term)));
        }

        return patterns;
    }

    private static string FormatGeneratedBufferStringScanOffsets(IReadOnlyList<long> offsets)
    {
        if (offsets.Count == 0)
        {
            return "<none>";
        }

        var shownOffsets = offsets.Take(GeneratedBufferStringScanSummaryOffsetCount).Select(FormatScanHex);
        var formatted = string.Join(",", shownOffsets);
        return offsets.Count <= GeneratedBufferStringScanSummaryOffsetCount
            ? formatted
            : formatted + $" (+{(offsets.Count - GeneratedBufferStringScanSummaryOffsetCount).ToString(CultureInfo.InvariantCulture)} more)";
    }

    private static void PrintGeneratedBufferStringScanHit(
        byte[] bytes,
        GeneratedBufferStringScanInput input,
        GeneratedBufferStringScanPattern pattern,
        long offset,
        int contextBytes)
    {
        var hitOffset = checked((int)offset);
        var startOffset = Math.Max(0, hitOffset - contextBytes);
        var hitEndOffset = Math.Min(bytes.Length, hitOffset + pattern.Bytes.Length);
        var endOffset = Math.Min(bytes.Length, hitEndOffset + contextBytes);
        var context = bytes.AsSpan(startOffset, endOffset - startOffset).ToArray();
        var text = pattern.EncodingName == "utf-16le"
            ? FormatGeneratedBufferStringScanUtf16Text(bytes, startOffset, endOffset, hitOffset)
            : FormatGeneratedBufferStringScanAsciiText(context);

        Console.WriteLine(
            $"Hit: label={input.Label} term={pattern.Term} encoding={pattern.EncodingName} " +
            $"offset={FormatScanHex(offset)} contextStart={FormatScanHex(startOffset)} " +
            $"contextBytes={context.Length.ToString(CultureInfo.InvariantCulture)} " +
            $"bytes={FormatScanBytes(context)} text=\"{text}\"");
    }

    private static string FormatGeneratedBufferStringScanAsciiText(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length);
        foreach (var value in bytes)
        {
            AppendGeneratedBufferStringScanChar(builder, (char)value);
        }

        return builder.ToString();
    }

    private static string FormatGeneratedBufferStringScanUtf16Text(
        byte[] bytes,
        int contextStart,
        int contextEnd,
        int hitOffset)
    {
        var textStart = contextStart;
        if (((hitOffset - textStart) & 1) != 0)
        {
            textStart++;
        }

        var textLength = contextEnd - textStart;
        if ((textLength & 1) != 0)
        {
            textLength--;
        }

        if (textLength <= 0)
        {
            return string.Empty;
        }

        var decoded = Encoding.Unicode.GetString(bytes, textStart, textLength);
        var builder = new StringBuilder(decoded.Length);
        foreach (var value in decoded)
        {
            AppendGeneratedBufferStringScanChar(builder, value);
        }

        return builder.ToString();
    }

    private static void AppendGeneratedBufferStringScanChar(StringBuilder builder, char value)
    {
        switch (value)
        {
            case '\\':
                builder.Append(@"\\");
                break;
            case '"':
                builder.Append("\\\"");
                break;
            case '\0':
                builder.Append(@"\0");
                break;
            case '\r':
                builder.Append(@"\r");
                break;
            case '\n':
                builder.Append(@"\n");
                break;
            case '\t':
                builder.Append(@"\t");
                break;
            default:
                builder.Append(char.IsControl(value) || char.IsSurrogate(value) ? '.' : value);
                break;
        }
    }
}

internal sealed record GeneratedBufferStringScanOptions(
    IReadOnlyList<GeneratedBufferStringScanInput> Inputs,
    IReadOnlyList<string> Terms,
    int ContextBytes,
    int MaxHits);

internal sealed record GeneratedBufferStringScanInput(string Label, string Path);

internal sealed record GeneratedBufferStringScanPattern(string Term, string EncodingName, byte[] Bytes);
