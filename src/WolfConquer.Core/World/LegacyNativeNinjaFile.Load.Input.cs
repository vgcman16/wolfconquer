using System.Text;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinjaFile
{
    private const long MaximumTableBytes = 64L * 1024 * 1024;
    private const int MaximumTableRows = 262_144;
    private const int MaximumTableLine = 1024 * 1024;

    private static Dictionary<uint, T> ReadNinjaTable<T>(FileStream input, Func<string[], (uint Key, T Row)> parse, ref int remaining)
    {
        var rows = new Dictionary<uint, T>();
        // Match File.ReadAllLines' UTF8/BOM handling; ignored source columns may
        // contain opaque text. Required numeric fields retain actual Convert rules.
        using var reader = new StreamReader(input, Encoding.UTF8, detectEncodingFromByteOrderMarks: true,
            bufferSize: 4096, leaveOpen: true);
        while (ReadNinjaTableLine(reader) is { } line)
        {
            if (remaining-- <= 0) throw new InvalidDataException("Native Ninja table row bound exceeded.");
            var fields = line.Split(["@@", " "], StringSplitOptions.RemoveEmptyEntries);
            var parsed = parse(fields);
            rows.Add(parsed.Key, parsed.Row); // Duplicate identity is a real whole-stage refusal.
        }
        return rows;
    }

    private static string? ReadNinjaTableLine(StreamReader reader)
    {
        if (reader.Peek() < 0) return null;
        var line = new StringBuilder();
        for (var character = reader.Read(); character >= 0; character = reader.Read())
        {
            if (character == '\n') break;
            if (character == '\r')
            {
                if (reader.Peek() == '\n') _ = reader.Read();
                break;
            }
            if (line.Length == MaximumTableLine) throw new InvalidDataException("Native Ninja table line bound exceeded.");
            line.Append((char)character);
        }
        return line.ToString();
    }
}
