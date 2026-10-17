using System.Globalization;
using System.Text.Json;

internal static partial class Program
{
    static int LicenseRouteCaptureCompare(string[] args)
    {
        LicenseRouteCaptureCompareOptions options;
        try
        {
            options = ResolveLicenseRouteCaptureCompareOptions(args);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        try
        {
            var captures = options.CapturePaths
                .Select(ReadLicenseRouteCaptureFile)
                .ToArray();
            var result = CompareLicenseRouteCaptures(captures);

            Console.WriteLine("License route capture compare:");
            Console.WriteLine($"Captures: {captures.Length}");
            foreach (var capture in captures)
            {
                Console.WriteLine(
                    $"Capture: path={capture.Path} firstReadBytes={capture.PacketBytes.Count} " +
                    $"accepted={FormatNullableBool(capture.Accepted)} patched={FormatNullableBool(capture.Patched)} " +
                    $"port={FormatNullableInt(capture.Port)} remote={FormatNullableText(capture.Remote)}");
            }

            Console.WriteLine($"First-read lengths: {string.Join(",", captures.Select(capture => capture.PacketBytes.Count))}");
            Console.WriteLine($"Compared byte span: {FormatLicenseRouteComparedSpan(result.ComparedByteCount)}");
            Console.WriteLine($"Stable byte positions: {result.StableByteOffsets.Count}/{result.ComparedByteCount}");
            Console.WriteLine($"Different byte positions: {result.DifferentByteOffsets.Count}/{result.ComparedByteCount}");
            Console.WriteLine($"Stable byte ranges: {FormatByteOffsetRanges(result.StableByteOffsets)}");
            Console.WriteLine($"Different byte ranges: {FormatByteOffsetRanges(result.DifferentByteOffsets)}");
            Console.WriteLine($"Length mismatch: {result.LengthMismatch}");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"License route capture compare failed: {ex.Message}");
            return 1;
        }
    }

    static LicenseRouteCaptureCompareOptions ResolveLicenseRouteCaptureCompareOptions(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException(
                "Usage: WolfConquer.DataTool license-route-capture-compare <captureJsonPath> <captureJsonPath> [captureJsonPath ...]");
        }

        return new LicenseRouteCaptureCompareOptions(args.Skip(1).Select(Path.GetFullPath).ToArray());
    }

    static LicenseRouteCaptureFile ReadLicenseRouteCaptureFile(string path)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var capture = JsonSerializer.Deserialize<LicenseRouteCaptureJson>(File.ReadAllText(path), options);
        if (capture is null)
        {
            throw new InvalidDataException($"Could not read license route capture JSON: {path}");
        }

        if (string.IsNullOrWhiteSpace(capture.FirstReadHex))
        {
            throw new InvalidDataException($"License route capture has no FirstReadHex: {path}");
        }

        var bytes = ParseHexBytes(capture.FirstReadHex);
        if (capture.FirstReadBytes.HasValue && capture.FirstReadBytes.Value != bytes.Count)
        {
            throw new InvalidDataException(
                $"License route capture FirstReadBytes mismatch in {path}: declared={capture.FirstReadBytes.Value} parsed={bytes.Count}");
        }

        return new LicenseRouteCaptureFile(
            path,
            bytes,
            capture.Accepted,
            capture.Patched,
            capture.Port,
            capture.Remote);
    }

    static IReadOnlyList<byte> ParseHexBytes(string value)
    {
        var hex = new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
        if (hex.Length == 0 || hex.Length % 2 != 0)
        {
            throw new FormatException("Hex byte text must contain an even number of hex digits.");
        }

        var bytes = new byte[hex.Length / 2];
        for (var index = 0; index < bytes.Length; index++)
        {
            var text = hex.Substring(index * 2, 2);
            if (!byte.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[index]))
            {
                throw new FormatException($"Invalid hex byte: {text}");
            }
        }

        return bytes;
    }

    static LicenseRouteCaptureCompareResult CompareLicenseRouteCaptures(IReadOnlyList<LicenseRouteCaptureFile> captures)
    {
        if (captures.Count < 2)
        {
            throw new ArgumentException("At least two license route captures are required.");
        }

        var comparedByteCount = captures.Max(capture => capture.PacketBytes.Count);
        var stableByteOffsets = new List<int>();
        var differentByteOffsets = new List<int>();

        for (var offset = 0; offset < comparedByteCount; offset++)
        {
            var firstCapture = captures[0];
            var presentInAll = captures.All(capture => offset < capture.PacketBytes.Count);
            var equalInAll = presentInAll &&
                captures.All(capture => capture.PacketBytes[offset] == firstCapture.PacketBytes[offset]);

            if (equalInAll)
            {
                stableByteOffsets.Add(offset);
            }
            else
            {
                differentByteOffsets.Add(offset);
            }
        }

        return new LicenseRouteCaptureCompareResult(
            comparedByteCount,
            stableByteOffsets,
            differentByteOffsets,
            captures.Select(capture => capture.PacketBytes.Count).Distinct().Count() > 1);
    }

    static string FormatLicenseRouteComparedSpan(int comparedByteCount)
    {
        return comparedByteCount == 0
            ? "<empty>"
            : $"0-{comparedByteCount - 1}";
    }

    static string FormatByteOffsetRanges(IReadOnlyList<int> offsets)
    {
        if (offsets.Count == 0)
        {
            return "<none>";
        }

        var ranges = new List<string>();
        var start = offsets[0];
        var previous = start;
        for (var index = 1; index < offsets.Count; index++)
        {
            var current = offsets[index];
            if (current == previous + 1)
            {
                previous = current;
                continue;
            }

            ranges.Add(FormatByteOffsetRange(start, previous));
            start = current;
            previous = current;
        }

        ranges.Add(FormatByteOffsetRange(start, previous));
        return string.Join(",", ranges);
    }

    static string FormatByteOffsetRange(int start, int end)
    {
        return start == end
            ? start.ToString(CultureInfo.InvariantCulture)
            : $"{start.ToString(CultureInfo.InvariantCulture)}-{end.ToString(CultureInfo.InvariantCulture)}";
    }

    static string FormatNullableBool(bool? value)
    {
        return value.HasValue ? value.Value.ToString() : "<missing>";
    }

    static string FormatNullableInt(int? value)
    {
        return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "<missing>";
    }

    static string FormatNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "<missing>" : value;
    }
}

internal sealed record LicenseRouteCaptureCompareOptions(IReadOnlyList<string> CapturePaths);

internal sealed record LicenseRouteCaptureFile(
    string Path,
    IReadOnlyList<byte> PacketBytes,
    bool? Accepted,
    bool? Patched,
    int? Port,
    string? Remote);

internal sealed record LicenseRouteCaptureCompareResult(
    int ComparedByteCount,
    IReadOnlyList<int> StableByteOffsets,
    IReadOnlyList<int> DifferentByteOffsets,
    bool LengthMismatch);

internal sealed class LicenseRouteCaptureJson
{
    public int? FirstReadBytes { get; set; }

    public string? FirstReadHex { get; set; }

    public bool? Accepted { get; set; }

    public bool? Patched { get; set; }

    public int? Port { get; set; }

    public string? Remote { get; set; }
}
