using System.Text;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    private static Encoding ReadPrestigeLoadEncoding(FileStream stream)
    {
        Span<byte> prefix = stackalloc byte[4];
        var read = 0;
        while (read < prefix.Length)
        {
            var count = stream.Read(prefix[read..]);
            if (count == 0)
            {
                break;
            }

            read += count;
        }

        var bytes = prefix[..read];
        var (encoding, preambleLength) = SelectPrestigeLoadEncoding(bytes);
        stream.Position = preambleLength;
        return encoding;
    }

    private static (Encoding Encoding, int PreambleLength) SelectPrestigeLoadEncoding(ReadOnlySpan<byte> bytes)
    {
        // Check UTF-32 before UTF-16 because their little-endian BOMs share FF FE.
        if (bytes.StartsWith(new byte[] { 0xff, 0xfe, 0x00, 0x00 }))
        {
            return (new UTF32Encoding(false, false, true), 4);
        }

        if (bytes.StartsWith(new byte[] { 0x00, 0x00, 0xfe, 0xff }))
        {
            return (new UTF32Encoding(true, false, true), 4);
        }

        if (bytes.StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
        {
            return (new UTF8Encoding(false, true), 3);
        }

        if (bytes.StartsWith(new byte[] { 0xff, 0xfe }))
        {
            return (new UnicodeEncoding(false, false, true), 2);
        }

        if (bytes.StartsWith(new byte[] { 0xfe, 0xff }))
        {
            return (new UnicodeEncoding(true, false, true), 2);
        }

        return (new UTF8Encoding(false, true), 0);
    }
}
