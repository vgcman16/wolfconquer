using System.Text.Json;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal sealed partial class LegacyMailboxPackageWorkStore
{
    private void Write(Record row)
    {
        var operation = Guid.ParseExact(row.Operation, "N");
        var path = WorkPath(operation);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(row);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Package work exceeds its bound.");
        SnapshotFileLockCoordinator.ExecuteLocked(path, () =>
        {
            EnsureOrdinary(path);
            var prior = Read(operation);
            if (prior is not null && (!Same(prior, row) || prior.Completed && !row.Completed))
                throw new InvalidOperationException("Package work cannot be replaced or reopened.");
            if (prior is not null && prior.Completed == row.Completed) return true;
            Directory.CreateDirectory(directory);
            EnsureOrdinary(path);
            var temporary = path + ".stage-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, options: FileOptions.WriteThrough))
                { output.Write(bytes); output.Flush(flushToDisk: true); }
                EnsureOrdinary(path);
                if (prior is null) File.Move(temporary, path, overwrite: false);
                else File.Replace(temporary, path, null, ignoreMetadataErrors: true);
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (System.Security.SecurityException) { }
            }
            var saved = Read(operation);
            if (saved is null || !Same(saved, row) || saved.Completed != row.Completed)
                throw new IOException("Package work publication is unproven.");
            return true;
        });
    }

    private static void EnsureOrdinary(string path)
    {
        var file = new FileInfo(Path.GetFullPath(path));
        file.Refresh();
        if (Directory.Exists(path)) throw new IOException("Package work target is not a file.");
        if (file.Exists && (file.Attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Package work link is refused.");
        for (var parent = file.Directory; parent is not null; parent = parent.Parent)
        {
            parent.Refresh();
            if (File.Exists(parent.FullName)) throw new IOException("Package work ancestor is not a directory.");
            if (parent.Exists && (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Package work ancestor link is refused.");
        }
    }
}
