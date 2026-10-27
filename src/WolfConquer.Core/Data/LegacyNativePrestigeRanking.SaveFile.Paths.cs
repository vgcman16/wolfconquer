using WolfConquer.Core.World;

namespace WolfConquer.Core.Data;

internal partial class LegacyNativePrestigeRanking
{
    private static FileStream? OpenPrestigeSaveRead(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private static PrestigeSavePaths NewPrestigeSavePaths(string destination)
    {
        var prefix = Path.Combine(Path.GetDirectoryName(destination)!,
            "." + Path.GetFileName(destination) + ".prestige-save-" + Guid.NewGuid().ToString("N"));
        return new PrestigeSavePaths(destination, prefix + ".tmp", prefix + ".bak");
    }

    private static void RequirePrestigeSaveRegularPath(string path)
    {
        for (DirectoryInfo? directory = new(Path.GetDirectoryName(path)!); directory is not null; directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Prestige save path traverses a reparse point.");
            }
        }

        try
        {
            if ((File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
            {
                throw new IOException("Prestige save path is not a regular file.");
            }
        }
        catch (FileNotFoundException) { }
    }

    private static bool PrestigeSaveFileAbsent(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return false;
        }
        catch (FileNotFoundException) { return true; }
    }

    private static void DeleteOwnedPrestigeSaveFile(string path, bool ownsFile)
    {
        if (!ownsFile) return;
        try
        {
            LegacyMailboxPackageMutationFence.RequireFileMutation(path);
            RequirePrestigeSaveRegularPath(path);
            File.Delete(path);
        }
        catch (Exception error) when (IsOrdinaryPrestigeSaveFailure(error))
        {
            // Retaining this exact private sidecar cannot mask a primary failure
            // or turn an already byte-verified commit into a rejected save.
        }
    }

    private static void ClosePrestigeSaveRead(FileStream? file)
    {
        try { file?.Dispose(); }
        catch (Exception error) when (IsOrdinaryPrestigeSaveFailure(error)) { }
    }

    private static IOException PrestigeSaveRecoveryFailure(PrestigeSavePaths paths, Exception? cause = null)
        => new($"Prestige save recovery required. Destination: '{paths.Destination}'; " +
            $"stage (possibly consumed): '{paths.Stage}'; backup (possibly absent): '{paths.Backup}'.", cause);

    private static bool IsOrdinaryPrestigeSaveFailure(Exception error)
        => error is IOException or UnauthorizedAccessException or ArgumentException or
            NotSupportedException or System.Security.SecurityException;

}
