using System.Text;

namespace WolfConquer.Core.Data;

internal static class SnapshotFilePersistence
{
    private const int PersistenceAttemptCount = 5;
    private const int RetryDelayMilliseconds = 10;

    internal static byte[]? ReadAllBytesIfExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        for (var attempt = 1; attempt <= PersistenceAttemptCount; attempt++)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            catch (IOException) when (attempt < PersistenceAttemptCount)
            {
                WaitBeforeRetry(attempt);
            }
        }

        throw new InvalidOperationException("Snapshot read retry loop exited unexpectedly.");
    }

    internal static string? ReadAllTextIfExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        for (var attempt = 1; attempt <= PersistenceAttemptCount; attempt++)
        {
            try
            {
                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (IOException) when (attempt < PersistenceAttemptCount)
            {
                WaitBeforeRetry(attempt);
            }
        }

        throw new InvalidOperationException("Snapshot read retry loop exited unexpectedly.");
    }

    internal static void WriteAllBytesAtomically(
        string path,
        ReadOnlySpan<byte> bytes,
        string transactionMarker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionMarker);
        WolfConquer.Core.World.LegacyMailboxPackageMutationFence.RequireFileMutation(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path))
            ?? throw new ArgumentException("Snapshot path must have a parent directory.", nameof(path));
        Directory.CreateDirectory(directory);
        var payload = bytes.ToArray();

        for (var attempt = 1; attempt <= PersistenceAttemptCount; attempt++)
        {
            try
            {
                WriteAttempt(path, payload, transactionMarker);
                return;
            }
            catch (IOException) when (attempt < PersistenceAttemptCount)
            {
                WaitBeforeRetry(attempt);
            }
        }

        throw new InvalidOperationException("Snapshot write retry loop exited unexpectedly.");
    }

    internal static void WriteAllTextAtomically(
        string path,
        string content,
        string transactionMarker)
    {
        ArgumentNullException.ThrowIfNull(content);
        WriteAllBytesAtomically(path, Encoding.UTF8.GetBytes(content), transactionMarker);
    }

    private static void WriteAttempt(string path, byte[] bytes, string transactionMarker)
    {
        var transactionId = Guid.NewGuid().ToString("N");
        var tempPath = path + transactionMarker + transactionId + ".tmp";
        var backupPath = path + transactionMarker + transactionId + ".bak";
        var keepBackup = false;

        try
        {
            File.WriteAllBytes(tempPath, bytes);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        catch
        {
            if (File.Exists(backupPath))
            {
                keepBackup = !TryRestoreBackup(backupPath, path);
            }

            throw;
        }
        finally
        {
            TryDeleteTransactionFile(tempPath);
            if (!keepBackup)
            {
                TryDeleteTransactionFile(backupPath);
            }
        }
    }

    private static bool TryRestoreBackup(string backupPath, string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Replace(backupPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(backupPath, path);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TryDeleteTransactionFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void WaitBeforeRetry(int attempt)
    {
        Thread.Sleep(RetryDelayMilliseconds * attempt);
    }
}
