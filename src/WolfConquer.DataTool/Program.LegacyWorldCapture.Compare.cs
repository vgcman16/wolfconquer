using System.Text.Json;

internal static partial class Program
{
    static int LegacyWorldCaptureCompare(string[] args)
    {
        LegacyWorldCaptureCompareOptions options;
        try
        {
            options = ResolveLegacyWorldCaptureCompareOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var expected = ReadLegacyWorldCaptureFile(options.ExpectedPath);
            var actual = ReadLegacyWorldCaptureFile(options.ActualPath);
            var result = CompareLegacyWorldCaptures(expected, actual, options);

            Console.WriteLine("Legacy world capture compare:");
            Console.WriteLine($"Expected: {options.ExpectedPath}");
            Console.WriteLine($"Actual: {options.ActualPath}");
            Console.WriteLine($"Frame counts: expected={expected.Frames.Count} actual={actual.Frames.Count}");
            Console.WriteLine($"Compared range: {result.ComparedRange}");
            Console.WriteLine($"Compared frames: {result.ComparedFrames}");
            Console.WriteLine($"Ignored indexes: {FormatLegacyWorldCaptureIgnoredIndexes(options.IgnoreIndexes)}");
            Console.WriteLine($"Actual offset: {FormatLegacyWorldCaptureActualOffset(options)}");
            Console.WriteLine($"FirstDiff: {result.FirstDiffIndex}");
            if (result.FirstDiff is not null)
            {
                Console.WriteLine($"ExpectedDiff: {FormatLegacyWorldCaptureDiffFrame(result.FirstDiff.Expected)}");
                Console.WriteLine($"ActualDiff: {FormatLegacyWorldCaptureActualDiffFrame(result.FirstDiff)}");
            }

            Console.WriteLine($"Matched: {result.Matched}");
            return result.Matched ? 0 : 2;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Legacy world capture compare failed: {ex.Message}");
            return 1;
        }
    }

    static LegacyWorldCaptureCompareOptions ResolveLegacyWorldCaptureCompareOptions(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool legacy-world-capture-compare <expectedJson> <actualJson> [--ignore-indexes csv] [--start index] [--count n] [--actual-offset-start expectedIndex] [--actual-offset delta]");
        }

        var ignoreIndexes = new SortedSet<int>();
        int? startIndex = null;
        int? count = null;
        int? actualOffsetStart = null;
        var actualOffset = 0;

        for (var index = 3; index < args.Length; index++)
        {
            var option = args[index];
            if (string.Equals(option, "--ignore-indexes", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length)
                {
                    throw new FormatException("--ignore-indexes requires a comma-separated index list.");
                }

                AddLegacyWorldCaptureIgnoredIndexes(ignoreIndexes, args[index]);
            }
            else if (string.Equals(option, "--start", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out var parsed) || parsed < 0)
                {
                    throw new FormatException("--start requires a non-negative integer.");
                }

                startIndex = parsed;
            }
            else if (string.Equals(option, "--count", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out var parsed) || parsed <= 0)
                {
                    throw new FormatException("--count requires a positive integer.");
                }

                count = parsed;
            }
            else if (string.Equals(option, "--actual-offset-start", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out var parsed) || parsed < 0)
                {
                    throw new FormatException("--actual-offset-start requires a non-negative integer.");
                }

                actualOffsetStart = parsed;
            }
            else if (string.Equals(option, "--actual-offset", StringComparison.OrdinalIgnoreCase))
            {
                index++;
                if (index >= args.Length || !int.TryParse(args[index], out actualOffset))
                {
                    throw new FormatException("--actual-offset requires an integer.");
                }
            }
            else
            {
                throw new ArgumentException($"Unknown legacy-world-capture-compare option: {option}");
            }
        }

        if (actualOffset != 0 && actualOffsetStart is null)
        {
            throw new FormatException("--actual-offset requires --actual-offset-start.");
        }

        return new LegacyWorldCaptureCompareOptions(
            Path.GetFullPath(args[1]),
            Path.GetFullPath(args[2]),
            ignoreIndexes,
            startIndex,
            count,
            actualOffsetStart,
            actualOffset);
    }

    static void AddLegacyWorldCaptureIgnoredIndexes(SortedSet<int> ignoreIndexes, string value)
    {
        foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(part, out var parsed) || parsed < 0)
            {
                throw new FormatException("--ignore-indexes requires non-negative integer indexes.");
            }

            ignoreIndexes.Add(parsed);
        }
    }

    static LegacyWorldCaptureFile ReadLegacyWorldCaptureFile(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var capture = JsonSerializer.Deserialize<LegacyWorldCaptureFile>(File.ReadAllText(path), options);
        if (capture is null)
        {
            throw new InvalidDataException($"Could not read legacy world capture JSON: {path}");
        }

        if (capture.Frames is null)
        {
            throw new InvalidDataException($"Legacy world capture JSON has no frames array: {path}");
        }

        return capture;
    }

    static LegacyWorldCaptureCompareResult CompareLegacyWorldCaptures(
        LegacyWorldCaptureFile expected,
        LegacyWorldCaptureFile actual,
        LegacyWorldCaptureCompareOptions options)
    {
        var start = options.StartIndex ?? 0;
        var endExclusive = options.Count.HasValue
            ? checked(start + options.Count.Value)
            : int.MaxValue;
        var expectedFrames = expected.Frames
            .Where(frame => frame.Index >= start && frame.Index < endExclusive)
            .OrderBy(frame => frame.Index)
            .ToArray();
        var actualByIndex = actual.Frames
            .GroupBy(frame => frame.Index)
            .ToDictionary(group => group.Key, group => group.First());
        var compared = 0;

        foreach (var expectedFrame in expectedFrames)
        {
            if (options.IgnoreIndexes.Contains(expectedFrame.Index))
            {
                continue;
            }

            compared++;
            var actualIndex = MapLegacyWorldCaptureActualIndex(expectedFrame.Index, options);
            actualByIndex.TryGetValue(actualIndex, out var actualFrame);
            if (!LegacyWorldCaptureFramesMatch(expectedFrame, actualFrame))
            {
                return new LegacyWorldCaptureCompareResult(
                    false,
                    expectedFrame.Index,
                    compared,
                    FormatLegacyWorldCaptureComparedRange(expectedFrames),
                    new LegacyWorldCaptureFrameDiff(expectedFrame, actualIndex, actualFrame));
            }
        }

        return new LegacyWorldCaptureCompareResult(
            true,
            -1,
            compared,
            FormatLegacyWorldCaptureComparedRange(expectedFrames),
            null);
    }

    static int MapLegacyWorldCaptureActualIndex(int expectedIndex, LegacyWorldCaptureCompareOptions options)
    {
        if (options.ActualOffsetStart.HasValue && expectedIndex >= options.ActualOffsetStart.Value)
        {
            return checked(expectedIndex + options.ActualOffset);
        }

        return expectedIndex;
    }

    static bool LegacyWorldCaptureFramesMatch(LegacyWorldCaptureFrame expected, LegacyWorldCaptureFrame? actual)
    {
        return actual is not null &&
            expected.Opcode == actual.Opcode &&
            expected.Length == actual.Length &&
            string.Equals(expected.FirstHex, actual.FirstHex, StringComparison.OrdinalIgnoreCase);
    }

    static string FormatLegacyWorldCaptureComparedRange(IReadOnlyList<LegacyWorldCaptureFrame> frames)
    {
        return frames.Count == 0
            ? "<empty>"
            : $"{frames[0].Index}..{frames[^1].Index}";
    }

    static string FormatLegacyWorldCaptureIgnoredIndexes(IReadOnlySet<int> ignoreIndexes)
    {
        return ignoreIndexes.Count == 0
            ? "<none>"
            : string.Join(",", ignoreIndexes.Order());
    }

    static string FormatLegacyWorldCaptureActualOffset(LegacyWorldCaptureCompareOptions options)
    {
        return options.ActualOffsetStart.HasValue
            ? $"start={options.ActualOffsetStart.Value} delta={options.ActualOffset}"
            : "<none>";
    }

    static string FormatLegacyWorldCaptureDiffFrame(LegacyWorldCaptureFrame? frame)
    {
        return frame is null
            ? "<missing>"
            : $"index={frame.Index} opcode={frame.Opcode} length={frame.Length} firstHex={frame.FirstHex}";
    }

    static string FormatLegacyWorldCaptureActualDiffFrame(LegacyWorldCaptureFrameDiff diff)
    {
        return diff.Actual is null
            ? $"index={diff.ActualIndex} <missing>"
            : FormatLegacyWorldCaptureDiffFrame(diff.Actual);
    }
}

internal sealed record LegacyWorldCaptureCompareOptions(
    string ExpectedPath,
    string ActualPath,
    IReadOnlySet<int> IgnoreIndexes,
    int? StartIndex,
    int? Count,
    int? ActualOffsetStart,
    int ActualOffset);

internal sealed record LegacyWorldCaptureCompareResult(
    bool Matched,
    int FirstDiffIndex,
    int ComparedFrames,
    string ComparedRange,
    LegacyWorldCaptureFrameDiff? FirstDiff);

internal sealed record LegacyWorldCaptureFrameDiff(
    LegacyWorldCaptureFrame Expected,
    int ActualIndex,
    LegacyWorldCaptureFrame? Actual);
