using System.Text;

namespace WolfConquer.Core.Client;

public static class ClientTextPatchWriter
{
    public static void WritePatchedTextPreservingUnchangedBytes(
        string path,
        byte[] originalBytes,
        string originalText,
        string updatedText,
        Encoding encoding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(originalBytes);
        ArgumentNullException.ThrowIfNull(originalText);
        ArgumentNullException.ThrowIfNull(updatedText);
        ArgumentNullException.ThrowIfNull(encoding);

        try
        {
            File.WriteAllBytes(path, encoding.GetBytes(updatedText));
            return;
        }
        catch (EncoderFallbackException)
        {
            // Some legacy CP936 files contain byte sequences that decode to characters
            // the strict encoder refuses to write back. Preserve unchanged byte lines.
        }

        var originalLines = SplitTextLines(originalText).ToArray();
        var updatedLines = SplitTextLines(updatedText).ToArray();
        var originalByteLines = SplitByteLines(originalBytes).ToArray();
        if (originalLines.Length != updatedLines.Length ||
            originalLines.Length != originalByteLines.Length)
        {
            throw new InvalidOperationException("Patched client text changed line structure and cannot be byte-preserved.");
        }

        var lenientEncoding = Encoding.GetEncoding(
            encoding.CodePage,
            EncoderFallback.ReplacementFallback,
            DecoderFallback.ExceptionFallback);
        using var output = new MemoryStream(originalBytes.Length + Math.Max(0, updatedText.Length - originalText.Length));
        for (var index = 0; index < updatedLines.Length; index++)
        {
            if (StringComparer.Ordinal.Equals(originalLines[index].Text, updatedLines[index].Text) &&
                StringComparer.Ordinal.Equals(originalLines[index].LineEnding, updatedLines[index].LineEnding))
            {
                output.Write(originalByteLines[index]);
                continue;
            }

            var updatedLineBytes = lenientEncoding.GetBytes(updatedLines[index].Text + updatedLines[index].LineEnding);
            output.Write(updatedLineBytes);
        }

        File.WriteAllBytes(path, output.ToArray());
    }

    private static IEnumerable<ClientTextLine> SplitTextLines(string text)
    {
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\r' && text[index] != '\n')
            {
                continue;
            }

            var endingLength = text[index] == '\r' && index + 1 < text.Length && text[index + 1] == '\n'
                ? 2
                : 1;
            yield return new ClientTextLine(text[start..index], text.Substring(index, endingLength));
            index += endingLength - 1;
            start = index + 1;
        }

        if (start < text.Length)
        {
            yield return new ClientTextLine(text[start..], string.Empty);
        }
    }

    private static IEnumerable<byte[]> SplitByteLines(byte[] bytes)
    {
        var start = 0;
        for (var index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] != '\r' && bytes[index] != '\n')
            {
                continue;
            }

            var endingLength = bytes[index] == '\r' && index + 1 < bytes.Length && bytes[index + 1] == '\n'
                ? 2
                : 1;
            var length = index - start + endingLength;
            var line = new byte[length];
            Buffer.BlockCopy(bytes, start, line, 0, length);
            yield return line;
            index += endingLength - 1;
            start = index + 1;
        }

        if (start < bytes.Length)
        {
            var line = new byte[bytes.Length - start];
            Buffer.BlockCopy(bytes, start, line, 0, line.Length);
            yield return line;
        }
    }
}
