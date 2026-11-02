using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Field-specific origin from the journal's actual prepared Character image.
// It is data only; bound Store and query ownership provide authority.
internal sealed class LegacySavedVigorSeed
{
    private readonly string? line;
    internal LegacySavedVigorSeed(byte[] image, CharacterSnapshot expected)
    {
        line = ReadLine(image);
        var parsed = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(image, out _));
        if (parsed.Uid != expected.Uid || !SameValue(parsed, expected)) throw Invalid();
    }

    internal bool Matches(byte[] image) => line == ReadLine(image);

    internal static bool SameValue(CharacterSnapshot first, CharacterSnapshot second)
    {
        var a = LegacyNativeAttributeMapProof.CaptureRaw(first.Attributes);
        var b = LegacyNativeAttributeMapProof.CaptureRaw(second.Attributes);
        var left = a.Fields.Where(pair => pair.Key.Equals("Vigor", StringComparison.OrdinalIgnoreCase)).ToArray();
        var right = b.Fields.Where(pair => pair.Key.Equals("Vigor", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (left.Length > 1 || right.Length > 1 || !a.IsCurrentPlain() || !b.IsCurrentPlain()) throw Invalid();
        return left.Length == right.Length && (left.Length == 0 || left[0].Value == right[0].Value);
    }

    private static string? ReadLine(byte[] image)
    {
        string text = CharacterSnapshotFileEncoding.Decode(image, out var encoding);
        if (!encoding.GetPreamble().Concat(encoding.GetBytes(text)).SequenceEqual(image)) throw Invalid();
        using var reader = new StringReader(text);
        bool active = false, found = false; int sections = 0; string? result = null;
        while (reader.ReadLine() is { } raw)
        {
            var trimmed = raw.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                active = trimmed[1..^1].Trim().Equals("Character", StringComparison.OrdinalIgnoreCase);
                if (active && ++sections != 1) throw Invalid();
                continue;
            }
            if (!active || trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;
            int equals = raw.IndexOf('=');
            if (equals <= 0) throw Invalid();
            if (!raw[..equals].Trim().Equals("Vigor", StringComparison.OrdinalIgnoreCase)) continue;
            if (found) throw Invalid();
            found = true; result = raw;
        }
        if (sections != 1) throw Invalid();
        return result;
    }

    private static InvalidDataException Invalid() => new("boundSavedVigorSeedInvalid");
}
