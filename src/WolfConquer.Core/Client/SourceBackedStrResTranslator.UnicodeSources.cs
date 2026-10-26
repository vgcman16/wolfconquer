using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace WolfConquer.Core.Client;

public static partial class SourceBackedStrResTranslator
{
    private static readonly Regex ResourceRouteOrPath = new(
        @"://|www\.|[A-Za-z]:[\\/]|\\\\|\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    private static string DecodeUnicodeResourceSource(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 }))
        {
            throw new InvalidDataException("UTF-32 resource sources are not supported.");
        }

        var text = new UnicodeEncoding(false, true, true).GetString(bytes, 2, bytes.Length - 2);
        if (text.Contains((char)0))
        {
            throw new InvalidDataException("Unicode resource source contains NUL text.");
        }

        // UStrRes supplies numeric UI-resource IDs, not arbitrary configuration keys.
        // Retain exact spelling and duplicate rows so the existing conflict gate applies.
        var selected = new StringBuilder(text.Length);
        foreach (var line in SplitLines(text))
        {
            if (TryParseKeyValue(line.Text, out var key, out _, out _, out _) &&
                uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            {
                selected.Append(line.Text).Append(line.LineEnding);
            }
        }

        return selected.ToString();
    }

    private static bool CanEncodeStrRes(string value)
    {
        try
        {
            _ = StrResEncoding.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static bool IsProtectedResourceValue(string value) => ResourceRouteOrPath.IsMatch(value);
}
