using System.Text;

namespace WolfConquer.Core.Data;

internal static class CharacterSnapshotFileEncoding
{
    private static readonly UTF8Encoding Utf8NoBom = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);
    private static readonly UTF8Encoding Utf8Bom = new(
        encoderShouldEmitUTF8Identifier: true,
        throwOnInvalidBytes: true);
    private static readonly Encoding Windows1252 = CreateWindows1252();

    public static Encoding DefaultWriteEncoding => Utf8NoBom;

    public static string ReadAllText(string path, out Encoding encoding)
    {
        return Decode(File.ReadAllBytes(path), out encoding);
    }

    public static string[] ReadAllLines(string path, out Encoding encoding)
    {
        var text = ReadAllText(path, out encoding);
        using var reader = new StringReader(text);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines.ToArray();
    }

    internal static string Decode(byte[] bytes, out Encoding encoding)
    {
        var utf8Offset = HasUtf8Preamble(bytes) ? Utf8Bom.GetPreamble().Length : 0;
        try
        {
            var text = Utf8NoBom.GetString(bytes, utf8Offset, bytes.Length - utf8Offset);
            encoding = utf8Offset == 0 ? Utf8NoBom : Utf8Bom;
            return text;
        }
        catch (DecoderFallbackException)
        {
            encoding = Windows1252;
            return Windows1252.GetString(bytes);
        }
    }

    private static bool HasUtf8Preamble(ReadOnlySpan<byte> bytes)
    {
        return bytes.StartsWith(Utf8Bom.GetPreamble());
    }

    private static Encoding CreateWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
