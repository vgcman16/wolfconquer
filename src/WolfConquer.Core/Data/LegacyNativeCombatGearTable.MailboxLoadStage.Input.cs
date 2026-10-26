using System.Text;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativeCombatGearTable
{
    internal sealed partial class MailboxLoadStage
    {
        private const long MaximumInputBytes = 64L * 1024 * 1024;
        private const int MaximumRows = 262_144;
        private const int MaximumLineCharacters = 1024 * 1024;
        private sealed record InputStamp(long Length, DateTime CreatedUtc, DateTime WrittenUtc, FileAttributes Attributes);
        private sealed record LoadInput(string Path, byte[] Bytes, InputStamp Stamp);

        private static LoadInput ReadInput(string path, long budget, InputStamp? expected = null)
        {
            if (string.IsNullOrWhiteSpace(path) || !System.IO.Path.IsPathFullyQualified(path))
                throw new ArgumentException("CombatGear input requires an explicit absolute file path.");
            var full = System.IO.Path.GetFullPath(path);
            var before = ReadStamp(full);
            if (before.Length < 0 || before.Length > MaximumInputBytes || before.Length > budget ||
                expected is not null && before != expected)
                throw new InvalidDataException("CombatGear input is oversized or changed.");
            using var stream = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length != before.Length) throw new IOException("CombatGear input changed before reading.");
            var bytes = new byte[checked((int)before.Length)];
            stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || stream.Length != before.Length || ReadStamp(full) != before)
                throw new IOException("CombatGear input changed while reading.");
            return new(full, bytes, before);
        }

        private static InputStamp ReadStamp(string path)
        {
            var file = new FileInfo(path);
            file.Refresh();
            if (!file.Exists || (file.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                throw new IOException("CombatGear input is missing or not an ordinary file.");
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
            {
                parent.Refresh();
                if (!parent.Exists || (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("CombatGear input ancestor is missing or linked.");
            }
            return new(file.Length, file.CreationTimeUtc, file.LastWriteTimeUtc, file.Attributes);
        }

        private static bool InputCurrent(LoadInput input)
        {
            var actual = ReadInput(input.Path, input.Bytes.Length, input.Stamp);
            return actual.Bytes.AsSpan().SequenceEqual(input.Bytes);
        }

        private static void RequireCurrentInput(LoadInput input)
        {
            if (!InputCurrent(input)) throw new IOException("CombatGear input changed during parsing.");
        }

        private static string[] ReadLines(LoadInput input)
        {
            var (encoding, offset) = InputEncoding(input.Bytes);
            var text = encoding.GetString(input.Bytes, offset, input.Bytes.Length - offset);
            using var reader = new StringReader(text);
            var lines = new List<string>();
            while (reader.ReadLine() is { } line)
            {
                if (line.Length > MaximumLineCharacters || lines.Count >= MaximumRows + 1)
                    throw new InvalidDataException("CombatGear line or row budget exceeded.");
                lines.Add(line);
            }
            return lines.ToArray();
        }

        // File.ReadAllLines/OpenText recognize these BOMs. Keep those valid
        // encodings while rejecting malformed bytes instead of replacing them.
        private static (Encoding Encoding, int Offset) InputEncoding(byte[] bytes)
        {
            if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe, 0, 0 })) return (new UTF32Encoding(false, true, true), 4);
            if (bytes.AsSpan().StartsWith(new byte[] { 0, 0, 0xfe, 0xff })) return (new UTF32Encoding(true, true, true), 4);
            if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf })) return (new UTF8Encoding(false, true), 3);
            if (bytes.AsSpan().StartsWith(new byte[] { 0xff, 0xfe })) return (new UnicodeEncoding(false, true, true), 2);
            if (bytes.AsSpan().StartsWith(new byte[] { 0xfe, 0xff })) return (new UnicodeEncoding(true, true, true), 2);
            return (new UTF8Encoding(false, true), 0);
        }
    }
}
