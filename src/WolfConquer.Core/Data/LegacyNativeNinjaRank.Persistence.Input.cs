using System.Text;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeNinjaRank
{
    private sealed class PersistenceInput : IDisposable
    {
        internal PersistenceInput(FileStream lease, string[] lines)
        {
            Lease = lease;
            Lines = lines;
        }

        private FileStream Lease { get; }
        internal string[] Lines { get; }
        public void Dispose() => Lease.Dispose();
    }

    private static PersistenceInput ReadPersistenceInput(string path)
    {
        FileStream? lease = null;
        try
        {
            lease = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (lease.Length is < 0 or > MaximumPersistenceBytes)
                throw InvalidPersistence("Ninja rank file exceeds its offline byte bound.");
            var bytes = new byte[checked((int)lease.Length)];
            lease.ReadExactly(bytes);
            if (lease.ReadByte() != -1 || lease.Length != bytes.Length)
                throw InvalidPersistence("Ninja rank file changed while being read.");
            var lines = DecodePersistenceLines(bytes);
            return new PersistenceInput(lease, lines);
        }
        catch (DecoderFallbackException error)
        {
            lease?.Dispose();
            throw InvalidPersistence("Ninja rank file contains invalid encoded text.", error);
        }
        catch
        {
            lease?.Dispose();
            throw;
        }
    }

    private static string[] DecodePersistenceLines(byte[] bytes)
    {
        var (encoding, offset) = PersistenceEncoding(bytes);
        var text = encoding.GetString(bytes, offset, bytes.Length - offset);
        using var reader = new StringReader(text);
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length > MaximumPersistenceLineCharacters || lines.Count >= MaximumPersistenceRows + 1)
                throw InvalidPersistence("Ninja rank line or row bound exceeded.");
            lines.Add(line);
        }
        return lines.ToArray();
    }

    private static (Encoding Encoding, int Offset) PersistenceEncoding(byte[] bytes)
    {
        var span = bytes.AsSpan();
        if (span.StartsWith(new byte[] { 0xff, 0xfe, 0, 0 })) return (new UTF32Encoding(false, true, true), 4);
        if (span.StartsWith(new byte[] { 0, 0, 0xfe, 0xff })) return (new UTF32Encoding(true, true, true), 4);
        if (span.StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return (new UTF8Encoding(false, true), 3);
        if (span.StartsWith(new byte[] { 0xff, 0xfe })) return (new UnicodeEncoding(false, true, true), 2);
        if (span.StartsWith(new byte[] { 0xfe, 0xff })) return (new UnicodeEncoding(true, true, true), 2);
        return (new UTF8Encoding(false, true), 0);
    }

    private static InvalidDataException InvalidPersistence(string message, Exception? inner = null) =>
        new(message, inner);
}
