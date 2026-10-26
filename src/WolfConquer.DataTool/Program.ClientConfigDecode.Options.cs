using System.Globalization;
using WolfConquer.Core.Client;

internal static partial class Program
{
    private const string ClientConfigDecodeUsage =
        "WolfConquer.DataTool client-config-decode --profile d13-default --input <absolute-file> " +
        "<--modulus-file <big-endian-file> | --encoded-words-file <uint32-array-json>> " +
        "--output-new <absolute-fresh-file> --max-transformed-bytes <N> --max-result-bytes <N>";
    private const int ConfigDecodeMaximumBytes = ClientConfigDefaultContents.MaximumEncodedBytes;
    private const int ConfigDecodeWordsMaximumBytes = 4096;

    private sealed record ConfigDecodeOptions(string Input, string PublicData, bool EncodedWords,
        string Output, int MaximumTransformed, int MaximumResult);

    private static ConfigDecodeOptions ParseConfigDecodeOptions(string[] args)
    {
        if (args.Length != 13) throw new ArgumentException("Expected six explicit option/value pairs.");
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 1; index < args.Length; index += 2)
        {
            var key = args[index];
            if (key is not ("--profile" or "--input" or "--modulus-file" or "--encoded-words-file" or
                "--output-new" or "--max-transformed-bytes" or "--max-result-bytes") ||
                string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal) ||
                !fields.TryAdd(key, args[index + 1]))
                throw new ArgumentException("An option is unknown, duplicated or missing its value.");
        }
        if (!fields.TryGetValue("--profile", out var profile) || profile != "d13-default" ||
            !fields.TryGetValue("--input", out var input) || !fields.TryGetValue("--output-new", out var output) ||
            !fields.TryGetValue("--max-transformed-bytes", out var transformed) ||
            !fields.TryGetValue("--max-result-bytes", out var result) ||
            fields.ContainsKey("--modulus-file") == fields.ContainsKey("--encoded-words-file"))
            throw new ArgumentException("Explicit d13-default profile, input, one public-data file, new output and both bounds are required.");
        var words = fields.TryGetValue("--encoded-words-file", out var publicFile);
        publicFile ??= fields["--modulus-file"];
        return new(ConfigDecodeFullPath(input), ConfigDecodeFullPath(publicFile), words,
            ConfigDecodeFullPath(output), ParseConfigDecodeBound(transformed, minimum: 1), ParseConfigDecodeBound(result, minimum: 0));
    }

    private static int ParseConfigDecodeBound(string text, int minimum)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            value < minimum || value > ConfigDecodeMaximumBytes)
            throw new ArgumentException("Explicit byte bounds must be decimal, within 0..67108864; the transformed bound must be positive.");
        return value;
    }
}
