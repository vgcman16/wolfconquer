using System.Text.Json;

namespace WolfConquer.Core.Runtime;

public sealed partial class WorldPublicHeartbeatPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int MaximumBytes = 4096;

    private static void RequireOrdinaryPath(string path)
    {
        for (var current = new FileInfo(path).FullName; current is not null; current = Path.GetDirectoryName(current))
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Public heartbeat path contains a reparse entry.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        if (Directory.Exists(path)) throw new IOException("Public heartbeat path is a directory.");
    }

    private static FileStream OpenFileLease(string path)
    {
        RequireOrdinaryPath(path);
        if (new DriveInfo(Path.GetPathRoot(path)!).DriveType == DriveType.Network)
            throw new IOException("Public heartbeat requires a local drive.");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        RequireOrdinaryPath(path); RequireOrdinaryPath(path + ".lock");
        return new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    private async Task WriteFileAsync(WorldPublicHeartbeat dto, CancellationToken cancellationToken)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(dto, JsonOptions);
        if (bytes.Length > MaximumBytes) throw new InvalidDataException("Public heartbeat is oversized.");
        RequireOrdinaryPath(options.FilePath);
        string temporary = options.FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        bool created = false, published = false;
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                4096, FileOptions.Asynchronous))
            {
                created = true;
                await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                output.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            RequireOrdinaryPath(options.FilePath);
            PublishCompletedFile(temporary);
            published = true;
        }
        finally
        {
            try { if (created && !published) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
