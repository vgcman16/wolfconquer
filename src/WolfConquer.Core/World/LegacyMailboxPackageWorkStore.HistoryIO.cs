namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private static byte[] ReadHistoryBytes(string path, int maximum)
    {
        EnsureOrdinary(path);
        RequireExactHistoryLeaf(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 || stream.Length > maximum) throw new InvalidDataException("History file length is invalid.");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || stream.Length != bytes.Length) throw new IOException("History file changed during reading.");
        return bytes;
    }

    private static void WriteHistoryAtomically(string path, byte[] bytes, bool createOnly = false)
    {
        EnsureOrdinary(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        EnsureOrdinary(path);
        RequireExactHistoryLeaf(path);
        var temporary = path + ".stage-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                bufferSize: 4096, options: FileOptions.WriteThrough))
            { output.Write(bytes); output.Flush(flushToDisk: true); }
            EnsureOrdinary(path);
            if (createOnly || !File.Exists(path)) File.Move(temporary, path, overwrite: false);
            else File.Replace(temporary, path, null, ignoreMetadataErrors: true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (System.Security.SecurityException) { }
        }
    }

    private static void RequireExactHistoryLeaf(string path)
    {
        var parent = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(parent)) return;
        var matches = Directory.EnumerateFileSystemEntries(parent, Path.GetFileName(path)).Take(2).ToArray();
        if (matches.Length > 1 || matches.Length == 1 && !string.Equals(matches[0], path, StringComparison.Ordinal))
            throw new IOException("History path spelling differs from its closed identity.");
    }
}
